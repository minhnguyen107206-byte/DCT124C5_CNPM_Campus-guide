import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import '../widgets/point_card.dart';
import '../widgets/type_icon.dart';
import 'point_detail_screen.dart';

/// UC02 – Bản đồ các điểm, lọc theo loại; bật/tắt tự động thuyết minh (UC06); vùng thông báo (UC10).
class MapScreen extends StatefulWidget {
  const MapScreen({super.key});

  @override
  State<MapScreen> createState() => _MapScreenState();
}

class _MapScreenState extends State<MapScreen> {
  static const _center = LatLng(10.7598, 106.6823); // tâm khuôn viên 273 An Dương Vương
  final _map = MapController();
  String? _type;
  Point? _selected;

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final types = app.points.map((p) => p.type).toSet().toList();
    final activeType = types.contains(_type) ? _type : null;
    final shown = app.points.where((p) => activeType == null || p.type == activeType).toList();
    final me = app.position == null ? null : LatLng(app.position!.latitude, app.position!.longitude);

    return Stack(children: [
      FlutterMap(
        mapController: _map,
        options: MapOptions(
          initialCenter: shown.isNotEmpty ? LatLng(shown.first.lat, shown.first.lng) : _center,
          initialZoom: 18,
          onTap: (_, __) => setState(() => _selected = null),
        ),
        children: [
          TileLayer(
            urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
            userAgentPackageName: 'vn.edu.sgu.campus_guide',
            maxZoom: 20,
          ),
          CircleLayer(circles: [
            for (final n in app.notices)
              CircleMarker(
                point: LatLng(n.lat, n.lng),
                radius: n.radius.toDouble(),
                useRadiusInMeter: true,
                color: Colors.red.withValues(alpha: 0.12),
                borderColor: Colors.red,
                borderStrokeWidth: 1,
              ),
            if (app.autoNarration)
              for (final p in shown)
                CircleMarker(
                  point: LatLng(p.lat, p.lng),
                  radius: p.radius.toDouble(),
                  useRadiusInMeter: true,
                  color: Colors.blue.withValues(alpha: 0.08),
                  borderColor: Colors.blue.withValues(alpha: 0.4),
                  borderStrokeWidth: 1,
                ),
          ]),
          MarkerLayer(markers: [
            for (final p in shown)
              Marker(
                point: LatLng(p.lat, p.lng),
                width: 44,
                height: 44,
                child: GestureDetector(
                  onTap: () => setState(() => _selected = p),
                  child: TypeIcon(type: p.typeIcon, selected: _selected?.id == p.id),
                ),
              ),
            if (me != null)
              Marker(
                point: me,
                width: 22,
                height: 22,
                child: Container(
                  decoration: BoxDecoration(
                    color: Colors.blue,
                    shape: BoxShape.circle,
                    border: Border.all(color: Colors.white, width: 3),
                    boxShadow: const [BoxShadow(blurRadius: 4, color: Colors.black26)],
                  ),
                ),
              ),
          ]),
          const RichAttributionWidget(attributions: [TextSourceAttribution('© OpenStreetMap')]),
        ],
      ),
      // bộ lọc loại điểm
      Positioned(
        top: 8,
        left: 8,
        right: 8,
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(children: [
            Padding(
              padding: const EdgeInsets.only(right: 6),
              child: ChoiceChip(label: Text(app.t('all')), selected: activeType == null, onSelected: (_) => setState(() => _type = null)),
            ),
            for (final t in types)
              Padding(
                padding: const EdgeInsets.only(right: 6),
                child: ChoiceChip(label: Text(t), selected: activeType == t, onSelected: (_) => setState(() => _type = t)),
              ),
          ]),
        ),
      ),
      // tự động thuyết minh + điểm gần nhất + tâm trường + về vị trí của tôi
      Positioned(
        right: 12,
        bottom: _selected == null ? 88 : 216,
        child: Column(children: [
          FloatingActionButton.small(
            heroTag: 'auto',
            tooltip: app.t('autoNarration'),
            backgroundColor: app.autoNarration ? Theme.of(context).colorScheme.primary : null,
            foregroundColor: app.autoNarration ? Colors.white : null,
            onPressed: () async {
              await app.setAutoNarration(!app.autoNarration);
              if (!app.locationGranted && context.mounted) {
                ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(app.t('locationDenied'))));
              }
            },
            child: const Icon(Icons.hearing),
          ),
          const SizedBox(height: 8),
          FloatingActionButton.small(
            heroTag: 'nearest',
            tooltip: app.t('nearestPoint'),
            onPressed: shown.isEmpty
                ? null
                : () {
                    final nearest = app.sortedByDistance(shown).first;
                    _map.move(LatLng(nearest.lat, nearest.lng), 19);
                    setState(() => _selected = nearest);
                  },
            child: const Icon(Icons.near_me),
          ),
          const SizedBox(height: 8),
          FloatingActionButton.small(
            heroTag: 'campus',
            tooltip: app.t('campusCenter'),
            onPressed: () => _map.move(_center, 18),
            child: const Icon(Icons.school),
          ),
          const SizedBox(height: 8),
          FloatingActionButton.small(
            heroTag: 'me',
            onPressed: me == null ? null : () => _map.move(me, 18),
            child: const Icon(Icons.my_location),
          ),
        ]),
      ),
      if (app.loadingPoints) const Center(child: CircularProgressIndicator()),
      if (app.error != null)
        Center(
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                Text(app.error!),
                TextButton(onPressed: app.loadPoints, child: Text(app.t('retry'))),
              ]),
            ),
          ),
        ),
      if (_selected != null)
        Positioned(
          left: 8,
          right: 8,
          bottom: 76,
          child: PointCard(
            point: _selected!,
            distance: app.distanceTo(_selected!),
            onDetail: () => Navigator.push(context, MaterialPageRoute(builder: (_) => PointDetailScreen(point: _selected!))),
          ),
        ),
    ]);
  }
}
