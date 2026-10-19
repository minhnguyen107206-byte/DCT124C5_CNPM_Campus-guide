import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import 'point_detail_screen.dart';

/// UC11 – Lịch sử đã nghe (lưu trên máy) và đánh giá điểm.
class HistoryScreen extends StatelessWidget {
  const HistoryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    if (app.history.isEmpty) {
      return Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.headphones_outlined, size: 56, color: Colors.grey),
        const SizedBox(height: 8),
        Text(app.t('emptyHistory')),
      ]));
    }
    return ListView.separated(
      padding: const EdgeInsets.only(bottom: 90),
      itemCount: app.history.length,
      separatorBuilder: (_, __) => const Divider(height: 1),
      itemBuilder: (_, i) {
        final h = app.history[i];
        final stars = app.myRatings[h.pointId];
        return ListTile(
          leading: const CircleAvatar(child: Icon(Icons.history)),
          title: Text(h.name),
          subtitle: Text('${_fmt(h.time)} · ${h.lang}'),
          trailing: stars == null
              ? null
              : Row(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.star, color: Colors.amber, size: 18), Text(' $stars')]),
          onTap: () async {
            final p = await _resolve(app, h);
            if (p != null && context.mounted) {
              Navigator.push(context, MaterialPageRoute(builder: (_) => PointDetailScreen(point: p)));
            }
          },
        );
      },
    );
  }

  static String _fmt(DateTime t) =>
      '${t.day.toString().padLeft(2, '0')}/${t.month.toString().padLeft(2, '0')} ${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  Future<Point?> _resolve(AppState app, HistoryItem h) async {
    final local = app.points.where((p) => p.id == h.pointId);
    if (local.isNotEmpty) return local.first;
    try {
      return await app.api.point(h.pointId, app.lang!);
    } catch (_) {
      return null;
    }
  }
}
