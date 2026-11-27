import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import '../state/audio_controller.dart';
import '../widgets/mini_player.dart';
import 'history_screen.dart';
import 'list_screen.dart';
import 'map_screen.dart';
import 'notices_screen.dart';
import 'qr_scan_screen.dart';
import 'settings_screen.dart';
import 'tours_screen.dart';

/// Khung chính: Bản đồ · Danh sách · Tour · Lịch sử, nút quét QR và trình phát thu nhỏ.
class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _tab = 0;
  StreamSubscription<Point>? _queuedAudioSub;
  final List<Notice> _noticeQueue = [];
  bool _showingNotice = false;

  @override
  void initState() {
    super.initState();
    final app = context.read<AppState>();
    final audio = context.read<AudioController>();

    // UC06 – vào bán kính một điểm khi bật tự động thuyết minh
    app.onGeofenceEnter = (Point p) {
      if (!mounted) return;
      HapticFeedback.vibrate();
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text('${app.t('nearPoint')} ${p.name}'),
        duration: const Duration(seconds: 4),
      ));
      if (!p.hasAudio) return;
      if (audio.playing && audio.current?.id != p.id) {
        // A2: đang phát điểm khác → chỉ giữ 1 listener chờ phát xong (tránh chồng dialog / leak)
        _queuedAudioSub?.cancel();
        _queuedAudioSub = audio.onCompleted.listen((_) {
          _queuedAudioSub?.cancel();
          _queuedAudioSub = null;
          if (mounted) _askPlay(p);
        });
      } else {
        audio.playPoint(p, source: 'GPS');
      }
    };

    // UC10 – thông báo khu vực / khẩn (hiển thị tuần tự, không chồng dialog)
    app.onNotice = (Notice n) {
      if (!mounted) return;
      _noticeQueue.add(n);
      _drainNotices();
    };
  }

  Future<void> _drainNotices() async {
    if (_showingNotice || _noticeQueue.isEmpty || !mounted) return;
    _showingNotice = true;
    while (_noticeQueue.isNotEmpty && mounted) {
      final n = _noticeQueue.removeAt(0);
      await showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          icon: Icon(n.urgent ? Icons.warning_amber : Icons.campaign, color: n.urgent ? Colors.red : null),
          title: Text(n.title),
          content: Text(n.body),
          actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('OK'))],
        ),
      );
    }
    _showingNotice = false;
  }

  @override
  void dispose() {
    _queuedAudioSub?.cancel();
    super.dispose();
  }

  void _askPlay(Point p) {
    final app = context.read<AppState>();
    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: Text('${app.t('listen')}: ${p.name}?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: Text(app.t('skip'))),
          FilledButton(
            onPressed: () {
              Navigator.pop(context);
              context.read<AudioController>().playPoint(p, source: 'GPS');
            },
            child: Text(app.t('listen')),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final pages = const [MapScreen(), ListScreen(), ToursScreen(), HistoryScreen()];
    return Scaffold(
      appBar: AppBar(
        title: Row(children: [
          const Icon(Icons.travel_explore),
          const SizedBox(width: 8),
          const Text('CampusGuide'),
          if (app.offlineMode)
            const Padding(padding: EdgeInsets.only(left: 8), child: Chip(label: Text('Offline'), visualDensity: VisualDensity.compact)),
        ]),
        actions: [
          IconButton(
            tooltip: app.t('notices'),
            icon: Badge(
              isLabelVisible: app.notices.isNotEmpty,
              label: Text('${app.notices.length}'),
              child: const Icon(Icons.notifications_outlined),
            ),
            onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const NoticesScreen())),
          ),
          IconButton(
            tooltip: app.t('settings'),
            icon: const Icon(Icons.settings_outlined),
            onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const SettingsScreen())),
          ),
        ],
      ),
      body: Column(children: [
        Expanded(child: IndexedStack(index: _tab, children: pages)),
        const MiniPlayer(),
      ]),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const QrScanScreen())),
        icon: const Icon(Icons.qr_code_scanner),
        label: Text(app.t('scanQr')),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _tab,
        onDestinationSelected: (i) => setState(() => _tab = i),
        destinations: [
          NavigationDestination(icon: const Icon(Icons.map_outlined), label: app.t('map')),
          NavigationDestination(icon: const Icon(Icons.list_alt), label: app.t('list')),
          NavigationDestination(icon: const Icon(Icons.route_outlined), label: app.t('tours')),
          NavigationDestination(icon: const Icon(Icons.history), label: app.t('history')),
        ],
      ),
    );
  }
}
