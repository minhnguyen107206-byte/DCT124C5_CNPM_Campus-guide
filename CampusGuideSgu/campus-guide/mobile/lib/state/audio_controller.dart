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
  double ttsProgress = 0; // 0..1, theo vị trí từ đang đọc
  String _ttsText = '';
  final Stopwatch _ttsWatch = Stopwatch(); // đo thời gian nghe để ghi lượt nghe

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
      _ttsSpeaking = true;
      _ttsWatch.start();
      notifyListeners();
    });
    tts.setContinueHandler(() {
      _ttsSpeaking = true;
      _ttsWatch.start();
      notifyListeners();
    });
    tts.setPauseHandler(() {
      _ttsSpeaking = false;
      _ttsWatch.stop();
      notifyListeners();
    });
    tts.setCancelHandler(() {
      _ttsSpeaking = false;
      _ttsWatch.stop();
      notifyListeners();
    });
    tts.setCompletionHandler(() {
      _ttsSpeaking = false;
      _ttsWatch.stop();
      ttsProgress = 1;
      if (current != null && usingTts) _finish();
      notifyListeners();
    });
    tts.setProgressHandler((String text, int start, int end, String word) {
      if (_ttsText.isEmpty) return;
      ttsProgress = (end / _ttsText.length).clamp(0.0, 1.0).toDouble();
      notifyListeners();
    });
    tts.setErrorHandler((dynamic message) {
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
    await tts.stop();
    if (player.playing) await player.pause();

    current = p;
    this.source = source;
    error = null;
    _logged = false;
    usingTts = p.localAudio == null && p.audioUrl == null;
    ttsProgress = 0;
    _ttsWatch.reset();
    notifyListeners();
    try {
      if (usingTts) {
        await _speak(p);
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

  Future<void> _speak(Point p) async {
    _ttsText = [p.title, p.text].where((s) => s.trim().isNotEmpty).join('. ');
    if (_ttsText.isEmpty) throw Exception('no text');
    await tts.setLanguage(ttsLocale(p.lang));
    await tts.setSpeechRate(0.5 * speed); // 0.5 = tốc độ bình thường của flutter_tts
    await tts.speak(_ttsText);
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
      await tts.pause(); // iOS và Android 8+; đọc tiếp từ chỗ dừng khi bấm phát lại
    } else if (current != null) {
      if (ttsProgress >= 1) ttsProgress = 0;
      await _speak(current!);
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
      await tts.stop();
      ttsProgress = 0;
      if (current != null) await _speak(current!);
      notifyListeners();
      return;
    }
    await player.seek(Duration.zero);
    player.play();
  }

  Future<void> setSpeed(double s) async {
    speed = s;
    if (usingTts) {
      await tts.setSpeechRate(0.5 * s); // áp dụng từ lần đọc tiếp theo
    } else {
      await player.setSpeed(s);
    }
    notifyListeners();
  }

  Future<void> stop() async {
    _log();
    await tts.stop();
    await player.stop();
    current = null;
    usingTts = false;
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
