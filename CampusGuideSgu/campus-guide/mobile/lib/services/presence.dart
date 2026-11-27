import 'dart:async';
import 'dart:io' show Platform;

import 'package:flutter/widgets.dart';
import 'package:uuid/uuid.dart';

import '../state/app_state.dart';
import '../state/audio_controller.dart';

/// Báo cho máy chủ biết thiết bị đang mở app (dashboard "Trực tiếp" ở trang Admin).
/// Ping mỗi 5 giây kèm điểm đang nghe; app chuyển nền / đóng thì báo rời để số trên dashboard giảm ngay.
/// Số liệu được đếm ở backend: Services/PresenceService.cs.
class PresenceReporter with WidgetsBindingObserver {
  final AppState app;
  final AudioController audio;
  final String _pageId = const Uuid().v4();
  Timer? _timer;
  bool _lastListening = false;
  int? _lastPoint;
  bool _inFlight = false;

  PresenceReporter(this.app, this.audio);

  void start() {
    WidgetsBinding.instance.addObserver(this);
    audio.addListener(_onAudioChanged);
    _resume();
  }

  void _resume() {
    _timer?.cancel();
    _ping();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) => _ping());
  }

  // Bắt đầu / dừng nghe hoặc đổi điểm → báo ngay, không chờ 5 giây
  void _onAudioChanged() {
    if (audio.playing != _lastListening || audio.current?.id != _lastPoint) _ping();
  }

  void _ping() {
    final lang = app.lang;
    if (lang == null || app.offlineMode || _inFlight) return;
    _inFlight = true;
    _lastListening = audio.playing;
    _lastPoint = audio.current?.id;
    app.api
        .presencePing(app.deviceId, _pageId, audio.current?.id, lang, audio.playing,
            Platform.isIOS ? 'iPhone (app)' : Platform.isAndroid ? 'Android (app)' : 'App')
        .catchError((_) {})
        .whenComplete(() => _inFlight = false);
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _resume();
    } else if (state == AppLifecycleState.paused || state == AppLifecycleState.detached || state == AppLifecycleState.hidden) {
      _timer?.cancel();
      _timer = null;
      app.api.presenceLeave(app.deviceId, _pageId).catchError((_) {});
    }
  }
}
