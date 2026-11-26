import 'dart:async';
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:just_audio/just_audio.dart';

import '../models.dart';
import 'app_state.dart';

/// UC07 – Điều khiển audio. Chỉ phát một bài tại một thời điểm; khi đổi bài hoặc phát xong
/// thì ghi lượt nghe (POST /api/listens) và thêm vào lịch sử.
///
/// Máy chủ không còn sinh file audio (đã bỏ Google Cloud): điểm không có file audio thì app
/// đọc văn bản thuyết minh bằng giọng có sẵn của điện thoại (flutter_tts). Điểm có file audio
/// (VD: gói offline cũ) vẫn phát bằng just_audio như trước.
class AudioController extends ChangeNotifier {
  final AppState app;
  final AudioPlayer player = AudioPlayer();
  final FlutterTts tts = FlutterTts();

  Point? current;
  String source = 'DanhSach';
  String? error;
  double speed = 1.0;
  bool _logged = false;
  final _completed = StreamController<Point>.broadcast();
  Stream<Point> get onCompleted => _completed.stream;

  // ---- đọc bằng giọng của điện thoại ----
  bool usingTts = false; // bài hiện tại đang đọc bằng giọng của máy
  bool _ttsSpeaking = false;
  bool _ttsPaused = false;
  double ttsProgress = 0; // 0..1, theo vị trí từ đang đọc
  String _ttsText = '';
  int _ttsBaseOffset = 0; // vị trí ký tự bắt đầu của lượt speak hiện tại
  int _ttsOffset = 0; // vị trí ký tự hiện tại đã đọc đến
  int ttsStart = 0; // vị trí đầu từ đang đọc (tô sáng Karaoke)
  int ttsEnd = 0; // vị trí cuối từ đang đọc
  int _playToken = 0; // chống race condition khi cancelHandler bắn trễ lúc chuyển bài
  int _activeSpeakToken = 0;
  final Stopwatch _ttsWatch = Stopwatch(); // đo thời gian nghe để ghi lượt nghe

  String get ttsSpokenText => _ttsText;

  AudioController(this.app) {
    player.playerStateStream.listen((s) {
      if (s.processingState == ProcessingState.completed && current != null && !usingTts) {
        _finish();
        player.pause();
        player.seek(Duration.zero);
      }
      notifyListeners();
    });
    _initTts();
  }

  /// Mã vùng cho giọng đọc của máy theo mã ngôn ngữ nội dung.
  static String ttsLocale(String lang) => switch (lang) {
        'vi' => 'vi-VN',
        'en' => 'en-US',
        'ja' => 'ja-JP',
        'ko' => 'ko-KR',
        'zh-CN' || 'zh' => 'zh-CN',
        'fr' => 'fr-FR',
        'de' => 'de-DE',
        'es' => 'es-ES',
        'ru' => 'ru-RU',
        'th' => 'th-TH',
        'id' => 'id-ID',
        _ => lang,
      };

  Future<void> _initTts() async {
    try {
      await tts.awaitSpeakCompletion(false);
      if (Platform.isIOS) {
        // Đọc cả khi iPhone gạt nút im lặng, hạ nhỏ nhạc của app khác trong lúc đọc
        await tts.setSharedInstance(true);
        await tts.setIosAudioCategory(
          IosTextToSpeechAudioCategory.playback,
          [IosTextToSpeechAudioCategoryOptions.duckOthers],
          IosTextToSpeechAudioMode.spokenAudio,
        );
      }
    } catch (_) {}

    tts.setStartHandler(() {
      if (_activeSpeakToken != _playToken) return;
      _ttsSpeaking = true;
      _ttsPaused = false;
      _ttsWatch.start();
      notifyListeners();
    });
    tts.setContinueHandler(() {
      if (_activeSpeakToken != _playToken) return;
      _ttsSpeaking = true;
      _ttsPaused = false;
      _ttsWatch.start();
      notifyListeners();
    });
    tts.setPauseHandler(() {
      if (_activeSpeakToken != _playToken) return;
      _ttsSpeaking = false;
      _ttsPaused = true;
      _ttsWatch.stop();
      notifyListeners();
    });
    tts.setCancelHandler(() {
      if (_activeSpeakToken != _playToken) return;
      _ttsSpeaking = false;
      _ttsWatch.stop();
      notifyListeners();
    });
    tts.setCompletionHandler(() {
      if (_activeSpeakToken != _playToken) return;
      _ttsSpeaking = false;
      _ttsPaused = false;
      _ttsWatch.stop();
      _ttsOffset = _ttsText.length;
      ttsStart = _ttsText.length;
      ttsEnd = _ttsText.length;
      ttsProgress = 1;
      if (current != null && usingTts) _finish();
      notifyListeners();
    });
    tts.setProgressHandler((String text, int start, int end, String word) {
      if (_activeSpeakToken != _playToken || _ttsText.isEmpty || !usingTts) return;
      final absStart = (_ttsBaseOffset + start).clamp(0, _ttsText.length);
      final absEnd = (_ttsBaseOffset + end).clamp(absStart, _ttsText.length);
      ttsStart = absStart;
      ttsEnd = absEnd;
      _ttsOffset = absEnd;
      ttsProgress = (_ttsOffset / _ttsText.length).clamp(0.0, 1.0).toDouble();
      notifyListeners();
    });
    tts.setErrorHandler((dynamic message) {
      if (_activeSpeakToken != _playToken) return;
      _ttsSpeaking = false;
      _ttsWatch.stop();
      error = app.t('cannotPlay');
      notifyListeners();
    });
  }

  bool get playing => usingTts ? _ttsSpeaking : player.playing;

  /// Phát thuyết minh của một điểm. [source]: QR / GPS / Tour / DanhSach.
  Future<void> playPoint(Point p, {String source = 'DanhSach'}) async {
    if (current != null && current!.id != p.id) _log();
    _playToken++;
    await tts.stop();
    if (player.playing) await player.pause();

    current = p;
    this.source = source;
    error = null;
    _logged = false;
    usingTts = p.localAudio == null && p.audioUrl == null;
    _ttsSpeaking = false;
    _ttsPaused = false;
    ttsProgress = 0;
    _ttsBaseOffset = 0;
    _ttsOffset = 0;
    ttsStart = 0;
    ttsEnd = 0;
    _ttsWatch.reset();
    notifyListeners();
    try {
      if (usingTts) {
        _ttsText = [p.title, p.text].where((s) => s.trim().isNotEmpty).join('. ');
        if (_ttsText.isEmpty) throw Exception('no text');
        await _speakFrom(p, 0);
      } else {
        if (p.localAudio != null) {
          await player.setFilePath(p.localAudio!);
        } else {
          await player.setUrl(p.audioUrl!);
        }
        await player.setSpeed(speed);
        player.play();
      }
    } catch (_) {
      error = app.t('cannotPlay'); // E1: hiển thị văn bản thay thế
      notifyListeners();
    }
  }

  Future<void> _speakFrom(Point p, int offset) async {
    if (_ttsText.isEmpty) {
      _ttsText = [p.title, p.text].where((s) => s.trim().isNotEmpty).join('. ');
    }
    if (_ttsText.isEmpty) throw Exception('no text');
    final clamped = offset.clamp(0, _ttsText.length);
    var rawRem = _ttsText.substring(clamped);
    var trimmedRem = rawRem.trimLeft();
    if (trimmedRem.isEmpty) {
      _ttsBaseOffset = 0;
      _ttsOffset = 0;
      ttsStart = 0;
      ttsEnd = 0;
      ttsProgress = 0;
      trimmedRem = _ttsText;
    } else {
      _ttsBaseOffset = clamped + (rawRem.length - trimmedRem.length);
    }
    final token = ++_playToken;
    await tts.stop();
    if (token != _playToken) return;
    _activeSpeakToken = token;
    _ttsPaused = false;
    await tts.setLanguage(ttsLocale(p.lang));
    await tts.setSpeechRate((0.5 * speed).clamp(0.1, 1.0)); // 0.5 = tốc độ bình thường của flutter_tts
    await tts.speak(trimmedRem);
  }

  Future<void> toggle() async {
    if (!usingTts) {
      if (player.playing) {
        await player.pause();
      } else {
        await player.play();
      }
      return;
    }
    if (_ttsSpeaking) {
      // Dừng và giữ lại _ttsOffset để đọc tiếp đúng chỗ (ổn định trên cả iOS lẫn Android)
      _playToken++;
      _ttsSpeaking = false;
      _ttsPaused = true;
      _ttsWatch.stop();
      await tts.stop();
      notifyListeners();
    } else if (current != null) {
      final resumeAt = (_ttsPaused && _ttsOffset > 0 && _ttsOffset < _ttsText.length) ? _ttsOffset : 0;
      if (resumeAt == 0) {
        ttsProgress = 0;
        _ttsOffset = 0;
        ttsStart = 0;
        ttsEnd = 0;
      }
      await _speakFrom(current!, resumeAt);
    }
  }

  /// Tua chỉ dùng được với file audio; đọc bằng giọng của máy thì bỏ qua.
  Future<void> seekBy(int seconds) async {
    if (usingTts) return;
    final d = player.duration ?? Duration.zero;
    var pos = player.position + Duration(seconds: seconds);
    if (pos < Duration.zero) pos = Duration.zero;
    if (pos > d) pos = d;
    await player.seek(pos);
  }

  Future<void> seekTo(Duration d) async {
    if (usingTts) return;
    await player.seek(d);
  }

  Future<void> replay() async {
    if (usingTts) {
      ttsProgress = 0;
      _ttsBaseOffset = 0;
      _ttsOffset = 0;
      ttsStart = 0;
      ttsEnd = 0;
      _ttsPaused = false;
      if (current != null) await _speakFrom(current!, 0);
      notifyListeners();
      return;
    }
    await player.seek(Duration.zero);
    player.play();
  }

  Future<void> setSpeed(double s) async {
    speed = s;
    if (usingTts) {
      if (_ttsSpeaking && current != null) {
        await _speakFrom(current!, _ttsOffset); // áp dụng tốc độ mới ngay từ vị trí hiện tại
      } else {
        await tts.setSpeechRate((0.5 * s).clamp(0.1, 1.0));
      }
    } else {
      await player.setSpeed(s);
    }
    notifyListeners();
  }

  Future<void> stop() async {
    _log();
    _playToken++;
    await tts.stop();
    await player.stop();
    current = null;
    usingTts = false;
    _ttsSpeaking = false;
    _ttsPaused = false;
    ttsStart = 0;
    ttsEnd = 0;
    _ttsOffset = 0;
    notifyListeners();
  }

  void _finish() {
    final p = current;
    if (p == null) return;
    _log(full: true);
    _completed.add(p);
  }

  void _log({bool full = false}) {
    final p = current;
    if (p == null || _logged) return;
    final int secs;
    if (usingTts) {
      secs = full && p.audioSeconds > 0 ? p.audioSeconds : _ttsWatch.elapsed.inSeconds;
    } else {
      secs = full ? (player.duration?.inSeconds ?? p.audioSeconds) : player.position.inSeconds;
    }
    if (secs < 2 && !full) return; // bỏ qua lượt mở rồi tắt ngay
    _logged = true;
    app.addHistory(p);
    app.api.logListen(app.deviceId, p.id, p.lang, source, secs).catchError((_) {});
  }

  @override
  void dispose() {
    tts.stop();
    player.dispose();
    _completed.close();
    super.dispose();
  }
}
