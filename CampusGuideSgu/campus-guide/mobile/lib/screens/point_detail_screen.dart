import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import '../state/audio_controller.dart';
import '../widgets/mini_player.dart';
import '../widgets/point_card.dart';
import '../widgets/point_image.dart';
import '../widgets/rating_dialog.dart';
import 'directions_screen.dart';

/// UC02 bước 9 – Trang chi tiết điểm: nghe thuyết minh (UC07), chỉ đường (UC04), đánh giá (UC11).
class PointDetailScreen extends StatelessWidget {
  final Point point;
  final String source;
  const PointDetailScreen({super.key, required this.point, this.source = 'DanhSach'});

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final audio = context.read<AudioController>();
    final p = app.points.firstWhere((x) => x.id == point.id, orElse: () => point);
    return Scaffold(
      appBar: AppBar(title: Text(p.name)),
      bottomNavigationBar: const MiniPlayer(),
      body: ListView(children: [
        PointImage(point: p, size: MediaQuery.of(context).size.width, height: 210),
        Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(p.title, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 4),
            Wrap(spacing: 8, runSpacing: 4, children: [
              Chip(label: Text(p.type), visualDensity: VisualDensity.compact),
              if (p.floor != null) Chip(label: Text('${app.t('floor')}: ${p.floor}'), visualDensity: VisualDensity.compact),
              if (app.distanceTo(p) != null) Chip(avatar: const Icon(Icons.near_me, size: 16), label: Text(formatDistance(app.distanceTo(p))), visualDensity: VisualDensity.compact),
              if (p.rating != null) Chip(avatar: const Icon(Icons.star, size: 16, color: Colors.amber), label: Text('${p.rating} (${p.ratingCount})'), visualDensity: VisualDensity.compact),
            ]),
            if (p.hours != null) Padding(padding: const EdgeInsets.only(top: 6), child: Text('${app.t('hours')}: ${p.hours}')),
            if (p.isFallback)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(app.t('fallbackLang'), style: TextStyle(color: Colors.orange.shade800, fontStyle: FontStyle.italic)),
              ),
            const SizedBox(height: 16),
            Row(children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed: p.hasAudio ? () => audio.playPoint(p, source: source) : null,
                  icon: const Icon(Icons.headphones),
                  label: Text(app.t('listen')),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => DirectionsScreen(point: p))),
                  icon: const Icon(Icons.directions_walk),
                  label: Text(app.t('directions')),
                ),
              ),
            ]),
            const SizedBox(height: 16),
            Text(p.text, style: const TextStyle(fontSize: 16, height: 1.5)),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: () => showRatingDialog(context, p),
              icon: const Icon(Icons.star_outline),
              label: Text(app.t('rate')),
            ),
          ]),
        ),
      ]),
    );
  }
}
