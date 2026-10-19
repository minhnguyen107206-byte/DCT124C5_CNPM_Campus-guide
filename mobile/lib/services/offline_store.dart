import 'dart:convert';
import 'dart:io';

import 'package:archive/archive.dart';
import 'package:crypto/crypto.dart';
import 'package:path_provider/path_provider.dart';

import '../models.dart';
import 'api.dart';

class OfflineTour {
  final int tourId;
  final String lang;
  final int version;
  final Tour tour;
  OfflineTour(this.tourId, this.lang, this.version, this.tour);
}

/// UC09 – Tải gói offline (zip: tour.json + audio + ảnh), kiểm tra SHA-256, giải nén vào bộ nhớ máy.
class OfflineStore {
  Future<Directory> _root() async {
    final dir = Directory('${(await getApplicationDocumentsDirectory()).path}/tours');
    if (!await dir.exists()) await dir.create(recursive: true);
    return dir;
  }

  Future<Directory> _dirFor(int tourId) async => Directory('${(await _root()).path}/$tourId');

  /// Danh sách tour đã tải trên máy.
  Future<List<OfflineTour>> list() async {
    final root = await _root();
    final out = <OfflineTour>[];
    await for (final e in root.list()) {
      if (e is! Directory) continue;
      final t = await _load(e);
      if (t != null) out.add(t);
    }
    return out;
  }

  Future<OfflineTour?> get(int tourId) async {
    final d = await _dirFor(tourId);
    return await d.exists() ? _load(d) : null;
  }

  Future<OfflineTour?> _load(Directory d) async {
    final f = File('${d.path}/tour.json');
    final meta = File('${d.path}/meta.json');
    if (!await f.exists() || !await meta.exists()) return null;
    final j = jsonDecode(await f.readAsString());
    final m = jsonDecode(await meta.readAsString());
    final pts = (j['points'] as List)
        .map((p) => Point.fromJson(Map<String, dynamic>.from(p['point']), baseDir: d.path))
        .toList();
    final tour = Tour(
      id: j['tourId'],
      name: j['name'] ?? '',
      description: j['description'],
      distance: j['distance'] ?? 0,
      minutes: j['minutes'] ?? 0,
      pointCount: pts.length,
      points: pts,
    );
    return OfflineTour(tour.id, m['lang'], m['version'], tour);
  }

  /// Tải và cài gói. Ném ApiException khi lỗi; [onProgress] 0..1.
  Future<OfflineTour> install(Api api, int tourId, String lang, {void Function(double)? onProgress}) async {
    final info = await api.packageInfo(tourId, lang);
    final bytes = await api.download(info['url'], onProgress);
    final sum = sha256.convert(bytes).toString();
    if (sum != (info['checksum'] as String).toLowerCase()) {
      throw ApiException('Gói tải về bị lỗi (sai checksum), vui lòng tải lại');
    }
    final d = await _dirFor(tourId);
    if (await d.exists()) await d.delete(recursive: true);
    await d.create(recursive: true);
    final archive = ZipDecoder().decodeBytes(bytes);
    for (final file in archive) {
      if (file.isFile) {
        final out = File('${d.path}/${file.name}').absolute;
        // chống zip-slip: file giải nén phải nằm trong thư mục tour
        if (!out.path.startsWith(d.absolute.path + Platform.pathSeparator) || file.name.contains('..')) continue;
        await out.parent.create(recursive: true);
        await out.writeAsBytes(file.content as List<int>);
      }
    }
    await File('${d.path}/meta.json').writeAsString(jsonEncode({'lang': info['lang'], 'version': info['version']}));
    return (await _load(d))!;
  }

  Future<void> remove(int tourId) async {
    final d = await _dirFor(tourId);
    if (await d.exists()) await d.delete(recursive: true);
  }

  /// Tất cả điểm có trong các gói đã tải (dùng khi mất mạng – UC02 E2, UC03 A1).
  Future<List<Point>> allPoints() async {
    final seen = <int>{};
    final out = <Point>[];
    for (final t in await list()) {
      for (final p in t.tour.points) {
        if (seen.add(p.id)) out.add(p);
      }
    }
    return out;
  }
}
