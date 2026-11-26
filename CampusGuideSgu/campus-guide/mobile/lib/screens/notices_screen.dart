import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import '../widgets/point_card.dart';

/// UC10 – Danh sách thông báo đang hiệu lực; bấm vào để xem chi tiết & vùng ảnh hưởng trên bản đồ.
class NoticesScreen extends StatelessWidget {
  const NoticesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    return Scaffold(
      appBar: AppBar(title: Text(app.t('notices'))),
      body: RefreshIndicator(
        onRefresh: app.refreshNotices,
        child: app.notices.isEmpty
            ? ListView(children: [Padding(padding: const EdgeInsets.all(32), child: Center(child: Text(app.t('noNotices'))))])
            : ListView(children: [
                for (final n in app.notices)
                  Card(
                    margin: const EdgeInsets.fromLTRB(12, 8, 12, 0),
                    child: ListTile(
                      leading: Icon(n.urgent ? Icons.warning_amber : Icons.campaign, color: n.urgent ? Colors.red : null),
                      title: Text(n.title),
                      subtitle: Text(n.body, maxLines: 2, overflow: TextOverflow.ellipsis),
                      trailing: app.position == null
                          ? const Icon(Icons.chevron_right)
                          : Text(formatDistance(_dist(app, n.lat, n.lng))),
                      onTap: () => _showDetail(context, app, n),
                    ),
                  ),
              ]),
      ),
    );
  }

  void _showDetail(BuildContext context, AppState app, Notice n) {
    final end = n.endAt.toLocal();
    final endText =
        '${end.day.toString().padLeft(2, '0')}/${end.month.toString().padLeft(2, '0')}/${end.year} '
        '${end.hour.toString().padLeft(2, '0')}:${end.minute.toString().padLeft(2, '0')}';
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Icon(n.urgent ? Icons.warning_amber : Icons.campaign, color: n.urgent ? Colors.red : Theme.of(context).colorScheme.primary),
              const SizedBox(width: 8),
              Expanded(child: Text(n.title, style: Theme.of(context).textTheme.titleLarge)),
            ]),
            const SizedBox(height: 8),
            Text(n.body, style: const TextStyle(fontSize: 15, height: 1.4)),
            const SizedBox(height: 8),
            Text('${app.t('validUntil')}: $endText · R=${n.radius}m', style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 12),
            SizedBox(
              height: 220,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: FlutterMap(
                  options: MapOptions(initialCenter: LatLng(n.lat, n.lng), initialZoom: 17),
                  children: [
                    TileLayer(
                      urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                      userAgentPackageName: 'vn.edu.sgu.campus_guide',
                      maxZoom: 20,
                    ),
                    CircleLayer(circles: [
                      CircleMarker(
                        point: LatLng(n.lat, n.lng),
                        radius: n.radius.toDouble(),
                        useRadiusInMeter: true,
                        color: Colors.red.withValues(alpha: 0.2),
                        borderColor: Colors.red,
                        borderStrokeWidth: 2,
                      ),
                    ]),
                    MarkerLayer(markers: [
                      Marker(
                        point: LatLng(n.lat, n.lng),
                        width: 32,
                        height: 32,
                        child: Icon(n.urgent ? Icons.warning_amber : Icons.campaign, color: Colors.red),
                      ),
                    ]),
                  ],
                ),
              ),
            ),
          ]),
        ),
      ),
    );
  }

  double? _dist(AppState app, double lat, double lng) {
    final pos = app.position;
    return pos == null ? null : Geolocator.distanceBetween(pos.latitude, pos.longitude, lat, lng);
  }
}
