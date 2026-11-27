import 'dart:io';

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
          leading: _thumb(h.image),
          title: Text(h.name),
          subtitle: Text('${_fmt(h.time)} · ${h.lang}'),
          trailing: stars == null
              ? null
              : Row(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.star, color: Colors.amber, size: 18), Text(' $stars')]),
          onTap: () async {
            final p = await _resolve(app, h);
            if (!context.mounted) return;
            if (p != null) {
              Navigator.push(context, MaterialPageRoute(builder: (_) => PointDetailScreen(point: p)));
            } else {
              ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(app.t('offlinePointMissing'))));
            }
          },
        );
      },
    );
  }

  static Widget _thumb(String? img) {
    const fallback = CircleAvatar(child: Icon(Icons.history));
    if (img == null || img.isEmpty) return fallback;
    final child = img.startsWith('http://') || img.startsWith('https://')
        ? Image.network(img, width: 48, height: 48, fit: BoxFit.cover, errorBuilder: (_, __, ___) => fallback)
        : Image.file(File(img), width: 48, height: 48, fit: BoxFit.cover, errorBuilder: (_, __, ___) => fallback);
    return ClipRRect(borderRadius: BorderRadius.circular(8), child: child);
  }

  static String _fmt(DateTime t) =>
      '${t.day.toString().padLeft(2, '0')}/${t.month.toString().padLeft(2, '0')} ${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  Future<Point?> _resolve(AppState app, HistoryItem h) async {
    final local = app.points.where((p) => p.id == h.pointId);
    if (local.isNotEmpty) return local.first;
    final offlinePts = await app.offline.allPoints();
    final offHit = offlinePts.where((p) => p.id == h.pointId);
    if (offHit.isNotEmpty) return offHit.first;
    try {
      return await app.api.point(h.pointId, app.lang!);
    } catch (_) {
      return null;
    }
  }
}
