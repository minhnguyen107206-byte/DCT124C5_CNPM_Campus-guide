import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../services/offline_store.dart';
import '../state/app_state.dart';
import 'tour_detail_screen.dart';

/// UC08 – Danh sách tour; khi mất mạng chỉ hiện tour đã tải (E1).
class ToursScreen extends StatefulWidget {
  const ToursScreen({super.key});

  @override
  State<ToursScreen> createState() => _ToursScreenState();
}

class _ToursScreenState extends State<ToursScreen> {
  List<Tour> _tours = [];
  Map<int, OfflineTour> _offline = {};
  bool _loading = true;
  bool _noNet = false;
  String? _lang;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final lang = context.watch<AppState>().lang;
    if (lang != _lang) {
      _lang = lang;
      WidgetsBinding.instance.addPostFrameCallback((_) => _load());
    }
  }

  Future<void> _load() async {
    final app = context.read<AppState>();
    setState(() => _loading = true);
    final off = await app.offline.list();
    _offline = {for (final o in off) o.tourId: o};
    try {
      _tours = await app.api.tours(app.lang!);
      _noNet = false;
    } catch (_) {
      _tours = off.map((o) => o.tour).toList();
      _noNet = true;
    }
    if (mounted) setState(() => _loading = false);
  }

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    if (_loading) return const Center(child: CircularProgressIndicator());
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(padding: const EdgeInsets.fromLTRB(12, 12, 12, 90), children: [
        if (_noNet) ListTile(leading: const Icon(Icons.wifi_off), title: Text(app.t('noNetwork'))),
        for (final t in _tours)
          Card(
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: () async {
                await Navigator.push(context, MaterialPageRoute(builder: (_) => TourDetailScreen(tourId: t.id, summary: t)));
                _load();
              },
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                if (t.cover != null) Image.network(t.cover!, height: 140, width: double.infinity, fit: BoxFit.cover, errorBuilder: (_, __, ___) => const SizedBox.shrink()),
                ListTile(
                  leading: const Icon(Icons.route),
                  title: Text(t.name, style: const TextStyle(fontWeight: FontWeight.w600)),
                  subtitle: Text('${t.pointCount} ${app.t('points')} · ${t.distance} m · ~${t.minutes} ${app.t('min')}'),
                  trailing: _offline.containsKey(t.id)
                      ? Chip(
                          label: Text((t.packageVersion ?? 0) > _offline[t.id]!.version ? app.t('update') : app.t('downloaded')),
                          visualDensity: VisualDensity.compact,
                        )
                      : null,
                ),
              ]),
            ),
          ),
      ]),
    );
  }
}
