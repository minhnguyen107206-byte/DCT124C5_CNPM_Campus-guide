import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import 'point_image.dart';

String formatDistance(double? m) => m == null ? '' : (m < 1000 ? '${m.round()} m' : '${(m / 1000).toStringAsFixed(1)} km');

/// Thẻ tóm tắt khi bấm vào ghim (UC02 bước 7).
class PointCard extends StatelessWidget {
  final Point point;
  final double? distance;
  final VoidCallback onDetail;
  const PointCard({super.key, required this.point, this.distance, required this.onDetail});

  @override
  Widget build(BuildContext context) {
    final app = context.read<AppState>();
    return Card(
      elevation: 4,
      child: InkWell(
        onTap: onDetail,
        child: Padding(
          padding: const EdgeInsets.all(10),
          child: Row(children: [
            ClipRRect(borderRadius: BorderRadius.circular(8), child: PointImage(point: point, size: 72)),
            const SizedBox(width: 12),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(point.name, style: Theme.of(context).textTheme.titleMedium, maxLines: 1, overflow: TextOverflow.ellipsis),
                Text([point.type, if (distance != null) formatDistance(distance)].join(' · '),
                    style: Theme.of(context).textTheme.bodySmall),
                if (point.hours != null) Text('${app.t('hours')}: ${point.hours}', style: Theme.of(context).textTheme.bodySmall),
              ]),
            ),
            FilledButton.tonal(onPressed: onDetail, child: Text(app.t('detail'))),
          ]),
        ),
      ),
    );
  }
}
