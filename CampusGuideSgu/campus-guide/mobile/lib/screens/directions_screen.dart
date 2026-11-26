import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:geolocator/geolocator.dart';
import 'package:http/http.dart' as http;
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import '../widgets/point_card.dart';
import '../widgets/type_icon.dart';

/// UC04 – Xem đường đi bộ đến điểm (OSRM – OpenStreetMap), tính lại khi lệch đường > 30 m.
class DirectionsScreen extends StatefulWidget {
  final Point point;
  const DirectionsScreen({super.key, required this.point});

  @override
  State<DirectionsScreen> createState() => _DirectionsScreenState();
}

class _DirectionsScreenState extends State<DirectionsScreen> {
  final _map = MapController();
  List<LatLng> _route = [];
  double? _meters;
  double? _seconds;
  bool _straight = false;
  bool _arrived = false;
  bool _routing = false;
  String? _error;
  StreamSubscription<Position>? _sub;

  LatLng get _dest => LatLng(widget.point.lat, widget.point.lng);

  @override
  void initState() {
    super.initState();
    _start();
  }

  Future<void> _start() async {
    final app = context.read<AppState>();
    if (!await app.ensureLocationPermission()) {
      setState(() => _error = app.t('locationDenied')); // E1: chỉ hiển thị vị trí điểm đến
      return;
    }
    await app.startLocation();
    Position pos;
    try {
      // Simulator chưa đặt vị trí có thể không trả về → giới hạn 10 giây
      pos = app.position ??
          await Geolocator.getCurrentPosition(
            locationSettings: const LocationSettings(timeLimit: Duration(seconds: 10)),
          );
    } catch (_) {
      if (mounted) setState(() => _error = app.t('locationDenied'));
      return;
    }
    if (!mounted) return;
    await _computeRoute(LatLng(pos.latitude, pos.longitude));
    if (!mounted) return;
    _sub = app.positionStream.listen(_onMove);
  }

  Future<void> _computeRoute(LatLng from) async {
    if (_routing) return;
    _routing = true;
    try {
      final url = Uri.parse('https://routing.openstreetmap.de/routed-foot/route/v1/foot/'
          '${from.longitude},${from.latitude};${_dest.longitude},${_dest.latitude}?overview=full&geometries=geojson');
      final res = await http.get(url).timeout(const Duration(seconds: 8));
      final j = jsonDecode(res.body);
      final r = j['routes'][0];
      final coords = (r['geometry']['coordinates'] as List).map((c) => LatLng((c[1] as num).toDouble(), (c[0] as num).toDouble())).toList();
      if (!mounted) return; // người dùng đã thoát màn hình trước khi OSRM trả về
      setState(() {
        _route = [from, ...coords, _dest];
        _meters = (r['distance'] as num).toDouble();
        _seconds = (r['duration'] as num).toDouble();
        _straight = false;
      });
    } catch (_) {
      // E2: dịch vụ bản đồ lỗi / mất mạng → đường thẳng + khoảng cách chim bay
      final d = Geolocator.distanceBetween(from.latitude, from.longitude, _dest.latitude, _dest.longitude);
      if (!mounted) return;
      setState(() {
        _route = [from, _dest];
        _meters = d;
        _seconds = d / 1.2;
        _straight = true;
      });
    } finally {
      _routing = false;
    }
    if (mounted) _fit();
  }

  void _fit() {
    if (_route.length < 2) return;
    try {
      _map.fitCamera(CameraFit.coordinates(coordinates: _route, padding: const EdgeInsets.all(60)));
    } catch (_) {}
  }

  void _onMove(Position p) {
    final me = LatLng(p.latitude, p.longitude);
    final toDest = Geolocator.distanceBetween(p.latitude, p.longitude, _dest.latitude, _dest.longitude);
    if (toDest <= widget.point.radius) {
      if (!_arrived) setState(() => _arrived = true);
      return;
    }
    // A1: lệch đường quá 30 m → tính lại
    if (!_straight && _route.length > 1 && _distanceToRoute(me) > 30) {
      _computeRoute(me);
    } else {
      setState(() {});
    }
  }

  double _distanceToRoute(LatLng me) {
    double best = double.infinity;
    for (final pt in _route) {
      final d = Geolocator.distanceBetween(me.latitude, me.longitude, pt.latitude, pt.longitude);
      if (d < best) best = d;
    }
    return best;
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final me = app.position == null ? null : LatLng(app.position!.latitude, app.position!.longitude);
    return Scaffold(
      appBar: AppBar(title: Text('${app.t('directions')}: ${widget.point.name}')),
      body: Stack(children: [
        FlutterMap(
          mapController: _map,
          options: MapOptions(initialCenter: _dest, initialZoom: 18),
          children: [
            TileLayer(urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png', userAgentPackageName: 'vn.edu.sgu.campus_guide', maxZoom: 20),
            PolylineLayer(polylines: [
              if (_route.length > 1)
                Polyline(
                  points: _route,
                  strokeWidth: 5,
                  color: _straight ? Colors.grey : Theme.of(context).colorScheme.primary,
                  pattern: _straight ? StrokePattern.dashed(segments: const [10, 8]) : const StrokePattern.solid(),
                ),
            ]),
            CircleLayer(circles: [
              CircleMarker(point: _dest, radius: widget.point.radius.toDouble(), useRadiusInMeter: true, color: Colors.green.withOpacity(0.15)),
            ]),
            MarkerLayer(markers: [
              Marker(point: _dest, width: 44, height: 44, child: TypeIcon(type: widget.point.typeIcon, selected: true)),
              if (me != null)
                Marker(
                  point: me,
                  width: 22,
                  height: 22,
                  child: Container(decoration: BoxDecoration(color: Colors.blue, shape: BoxShape.circle, border: Border.all(color: Colors.white, width: 3))),
                ),
            ]),
          ],
        ),
        Positioned(
          left: 12,
          right: 12,
          bottom: 16,
          child: Card(
            color: _arrived ? Colors.green.shade50 : null,
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: _error != null
                  ? Text(_error!)
                  : _arrived
                      ? Row(children: [
                          const Icon(Icons.flag, color: Colors.green),
                          const SizedBox(width: 8),
                          Text(app.t('arrived'), style: Theme.of(context).textTheme.titleMedium),
                        ])
                      : _meters == null
                          ? const LinearProgressIndicator()
                          : Row(children: [
                              const Icon(Icons.directions_walk),
                              const SizedBox(width: 8),
                              Text('${formatDistance(_meters)} · ~${(_seconds! / 60).ceil()} ${app.t('min')} ${app.t('walk')}',
                                  style: Theme.of(context).textTheme.titleMedium),
                              if (_straight) const Text('  (≈)'),
                            ]),
            ),
          ),
        ),
      ]),
    );
  }
}
