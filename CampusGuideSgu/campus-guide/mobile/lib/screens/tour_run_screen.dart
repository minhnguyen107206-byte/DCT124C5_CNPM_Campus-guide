import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import '../state/audio_controller.dart';
import '../widgets/mini_player.dart';
import '../widgets/point_card.dart';
import '../widgets/rating_dialog.dart';
import '../widgets/type_icon.dart';
import 'directions_screen.dart';
import 'qr_scan_screen.dart';

/// UC08 – Đi theo tour: dẫn đến từng điểm, tự phát thuyết minh khi đến nơi (GPS) hoặc khi quét QR.
class TourRunScreen extends StatefulWidget {
  final Tour tour;
  final int startIndex;
  const TourRunScreen({super.key, required this.tour, this.startIndex = 0});

  @override
  State<TourRunScreen> createState() => _TourRunScreenState();
}

class _TourRunScreenState extends State<TourRunScreen> {
  late int _i;
  final Set<int> _played = {};
  StreamSubscription<Position>? _sub;
  final _map = MapController();
  late final AppState _app;

  Point get _p => widget.tour.points[_i];

  @override
  void initState() {
    super.initState();
    _i = widget.startIndex;
    _app = context.read<AppState>();
    _app.tourRunning = true;
    _app.startLocation();
    _sub = _app.positionStream.listen(_onMove);
    // Đang đứng sẵn trong bán kính điểm đầu thì phát luôn, không chờ GPS báo vị trí mới
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _app.position != null) _onMove(_app.position!);
    });
  }

  void _onMove(Position pos) {
    final d = Geolocator.distanceBetween(pos.latitude, pos.longitude, _p.lat, _p.lng);
    if (d <= _p.radius && !_played.contains(_p.id)) _play();
    if (mounted) setState(() {});
  }

  void _play() {
    _played.add(_p.id);
    context.read<AudioController>().playPoint(_p, source: 'Tour');
  }

  void _next() {
    final app = context.read<AppState>();
    if (_i >= widget.tour.points.length - 1) {
      app.clearTourProgress();
      _finish();
      return;
    }
    setState(() => _i++);
    app.saveTourProgress(widget.tour.id, _i);
    if (app.position != null) _onMove(app.position!);
    try {
      _map.move(LatLng(_p.lat, _p.lng), 18);
    } catch (_) {}
  }

  void _finish() {
    final app = context.read<AppState>();
    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        icon: const Icon(Icons.emoji_events, color: Colors.amber, size: 48),
        title: Text(app.t('finish')),
        content: Text(widget.tour.name),
        actions: [
          TextButton(
            onPressed: () {
              Navigator.pop(context);
              showRatingDialog(context, _p);
            },
            child: Text(app.t('rate')),
          ),
          FilledButton(
            onPressed: () {
              Navigator.pop(context);
              Navigator.pop(context);
            },
            child: const Text('OK'),
          ),
        ],
      ),
    );
  }

  @override
  void dispose() {
    _app.tourRunning = false;
    _sub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final pts = widget.tour.points;
    final me = app.position == null ? null : LatLng(app.position!.latitude, app.position!.longitude);
    final dist = app.distanceTo(_p);
    return Scaffold(
      appBar: AppBar(title: Text('${widget.tour.name} · ${_i + 1}/${pts.length}')),
      bottomNavigationBar: const MiniPlayer(),
      body: Column(children: [
        Expanded(
          child: FlutterMap(
            mapController: _map,
            options: MapOptions(initialCenter: LatLng(_p.lat, _p.lng), initialZoom: 18),
            children: [
              TileLayer(urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png', userAgentPackageName: 'vn.edu.sgu.campus_guide', maxZoom: 20),
              PolylineLayer(polylines: [
                Polyline(points: pts.map((p) => LatLng(p.lat, p.lng)).toList(), strokeWidth: 3, color: Colors.grey),
                if (me != null) Polyline(points: [me, LatLng(_p.lat, _p.lng)], strokeWidth: 4, color: Theme.of(context).colorScheme.primary),
              ]),
              CircleLayer(circles: [
                CircleMarker(point: LatLng(_p.lat, _p.lng), radius: _p.radius.toDouble(), useRadiusInMeter: true, color: Colors.green.withValues(alpha: 0.15)),
              ]),
              MarkerLayer(markers: [
                for (var k = 0; k < pts.length; k++)
                  Marker(
                    point: LatLng(pts[k].lat, pts[k].lng),
                    width: 40,
                    height: 40,
                    child: Opacity(opacity: k == _i ? 1 : 0.5, child: TypeIcon(type: pts[k].typeIcon, selected: k == _i)),
                  ),
                if (me != null)
                  Marker(
                    point: me,
                    width: 20,
                    height: 20,
                    child: Container(decoration: BoxDecoration(color: Colors.blue, shape: BoxShape.circle, border: Border.all(color: Colors.white, width: 3))),
                  ),
              ]),
            ],
          ),
        ),
        Card(
          margin: const EdgeInsets.all(12),
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('${_i + 1}. ${_p.name}', style: Theme.of(context).textTheme.titleLarge),
              if (dist != null) Text(dist <= _p.radius ? app.t('arrived') : '${formatDistance(dist)} · ${app.t('walk')}'),
              const SizedBox(height: 10),
              Wrap(spacing: 8, runSpacing: 8, children: [
                FilledButton.icon(onPressed: _p.hasAudio ? _play : null, icon: const Icon(Icons.headphones), label: Text(app.t('listen'))),
                OutlinedButton.icon(
                  onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => DirectionsScreen(point: _p))),
                  icon: const Icon(Icons.directions_walk),
                  label: Text(app.t('directions')),
                ),
                OutlinedButton.icon(
                  onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const QrScanScreen())),
                  icon: const Icon(Icons.qr_code_scanner),
                  label: Text(app.t('scanQr')),
                ),
              ]),
              const Divider(),
              Row(children: [
                TextButton(onPressed: _next, child: Text(app.t('skip'))), // A1
                const Spacer(),
                FilledButton.tonalIcon(
                  onPressed: _next,
                  icon: Icon(_i == pts.length - 1 ? Icons.flag : Icons.arrow_forward),
                  label: Text(_i == pts.length - 1 ? app.t('finish') : app.t('next')),
                ),
              ]),
            ]),
          ),
        ),
      ]),
    );
  }
}
