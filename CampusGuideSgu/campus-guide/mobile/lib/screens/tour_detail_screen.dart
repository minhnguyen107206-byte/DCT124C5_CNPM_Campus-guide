import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../services/api.dart';
import '../services/offline_store.dart';
import '../state/app_state.dart';
import '../widgets/type_icon.dart';
import 'tour_run_screen.dart';

/// UC08 – Chi tiết tour và lộ trình; UC09 – tải / cập nhật / xóa gói offline.
class TourDetailScreen extends StatefulWidget {
  final int tourId;
  final Tour summary;
  const TourDetailScreen({super.key, required this.tourId, required this.summary});

  @override
  State<TourDetailScreen> createState() => _TourDetailScreenState();
}

class _TourDetailScreenState extends State<TourDetailScreen> {
  Tour? _tour;
  OfflineTour? _offline;
  double? _progress;
  bool _fromOffline = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final app = context.read<AppState>();
    _offline = await app.offline.get(widget.tourId);
    try {
      _tour = await app.api.tour(widget.tourId, app.lang!);
      _fromOffline = false;
    } catch (_) {
      _tour = _offline?.tour;
      _fromOffline = true;
    }
    if (mounted) setState(() {});
  }

  Future<void> _download() async {
    final app = context.read<AppState>();
    final size = widget.summary.packageSize;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(app.t('download')),
        content: Text('${size != null ? '${(size / 1024 / 1024).toStringAsFixed(1)} MB. ' : ''}${app.t('mobileData')}'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('×')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(app.t('download'))),
        ],
      ),
    );
    if (ok != true) return;
    setState(() => _progress = 0);
    try {
      _offline = await app.offline.install(app.api, widget.tourId, app.lang!, onProgress: (v) {
        if (mounted) setState(() => _progress = v);
      });
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(app.t('downloaded'))));
    } on ApiException catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); // E1, E3
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e'))); // E2: hết bộ nhớ...
    }
    if (mounted) setState(() => _progress = null);
  }

  Future<void> _remove() async {
    await context.read<AppState>().offline.remove(widget.tourId);
    setState(() => _offline = null);
  }

  void _start() {
    final app = context.read<AppState>();
    final tour = _offline?.tour ?? _tour!;
    final saved = app.tourProgress(tour.id);
    if (saved != null && saved > 0 && saved < tour.points.length) {
      showDialog(
        context: context,
        builder: (_) => AlertDialog(
          title: Text(app.t('continueTour')),
          actions: [
            TextButton(onPressed: () { Navigator.pop(context); _go(tour, 0); }, child: Text(app.t('start'))),
            FilledButton(onPressed: () { Navigator.pop(context); _go(tour, saved); }, child: Text(app.t('confirm'))),
          ],
        ),
      );
    } else {
      _go(tour, 0);
    }
  }

  void _go(Tour tour, int index) =>
      Navigator.push(context, MaterialPageRoute(builder: (_) => TourRunScreen(tour: tour, startIndex: index)));

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final t = _tour;
    final hasUpdate = _offline != null && (widget.summary.packageVersion ?? 0) > _offline!.version;
    return Scaffold(
      appBar: AppBar(title: Text(widget.summary.name)),
      body: t == null
          ? Center(child: _fromOffline ? Text(app.t('noNetwork')) : const CircularProgressIndicator())
          : ListView(children: [
              SizedBox(
                height: 230,
                child: FlutterMap(
                  options: MapOptions(
                    initialCameraFit: t.points.length > 1
                        ? CameraFit.coordinates(coordinates: t.points.map((p) => LatLng(p.lat, p.lng)).toList(), padding: const EdgeInsets.all(40))
                        : null,
                    initialCenter: t.points.isNotEmpty ? LatLng(t.points.first.lat, t.points.first.lng) : const LatLng(10.7598, 106.6823),
                    initialZoom: 17,
                  ),
                  children: [
                    TileLayer(urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png', userAgentPackageName: 'vn.edu.sgu.campus_guide'),
                    PolylineLayer(polylines: [
                      Polyline(points: t.points.map((p) => LatLng(p.lat, p.lng)).toList(), strokeWidth: 4, color: Theme.of(context).colorScheme.primary),
                    ]),
                    MarkerLayer(markers: [
                      for (final p in t.points) Marker(point: LatLng(p.lat, p.lng), width: 36, height: 36, child: TypeIcon(type: p.typeIcon)),
                    ]),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(16),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(t.name, style: Theme.of(context).textTheme.headlineSmall),
                  Text('${t.points.length} ${app.t('points')} · ${t.distance} m · ~${t.minutes} ${app.t('min')}'),
                  if (t.description != null) Padding(padding: const EdgeInsets.only(top: 8), child: Text(t.description!)),
                  const SizedBox(height: 12),
                  FilledButton.icon(onPressed: t.points.isEmpty ? null : _start, icon: const Icon(Icons.play_arrow), label: Text(app.t('start'))),
                  const SizedBox(height: 8),
                  if (_progress != null)
                    Column(children: [LinearProgressIndicator(value: _progress), Text('${app.t('downloading')} ${(_progress! * 100).round()}%')])
                  else if (_offline == null || hasUpdate)
                    OutlinedButton.icon(
                      onPressed: _fromOffline ? null : _download,
                      icon: const Icon(Icons.download),
                      label: Text(hasUpdate ? app.t('update') : app.t('download')),
                    )
                  else
                    Row(children: [
                      Chip(avatar: const Icon(Icons.offline_pin, size: 18), label: Text('${app.t('downloaded')} v${_offline!.version}')),
                      const Spacer(),
                      TextButton.icon(onPressed: _remove, icon: const Icon(Icons.delete_outline), label: Text(app.t('deleteDownload'))),
                    ]),
                ]),
              ),
              for (var i = 0; i < t.points.length; i++)
                ListTile(
                  leading: CircleAvatar(child: Text('${i + 1}')),
                  title: Text(t.points[i].name),
                  subtitle: Text(t.points[i].type),
                ),
            ]),
    );
  }
}
