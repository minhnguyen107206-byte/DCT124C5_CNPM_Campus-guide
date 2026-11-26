import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models.dart';

class ApiException implements Exception {
  final String message;
  final int? status;
  ApiException(this.message, [this.status]);
  @override
  String toString() => message;
}

/// Gọi REST API của backend ASP.NET Core.
class Api {
  /// Android Emulator gọi máy tính qua 10.0.2.2; iOS Simulator dùng localhost.
  /// Có thể đổi trong màn hình Cài đặt (khi chạy trên điện thoại thật dùng IP LAN của máy chạy backend).
  static String defaultBaseUrl() {
    const fromEnv = String.fromEnvironment('API_BASE_URL');
    if (fromEnv.isNotEmpty) return fromEnv;
    return Platform.isAndroid ? 'http://10.0.2.2:5000' : 'http://localhost:5000';
  }

  String baseUrl;
  final http.Client _client = http.Client();
  static const _timeout = Duration(seconds: 12);

  Api(this.baseUrl);

  Uri _u(String path, [Map<String, String>? q]) => Uri.parse('$baseUrl$path').replace(queryParameters: q);

  Future<dynamic> _get(String path, [Map<String, String>? q]) async {
    final res = await _client.get(_u(path, q)).timeout(_timeout);
    return _decode(res);
  }

  Future<dynamic> _post(String path, Map<String, dynamic> body) async {
    final res = await _client
        .post(_u(path), headers: {'Content-Type': 'application/json'}, body: jsonEncode(body))
        .timeout(_timeout);
    return _decode(res);
  }

  dynamic _decode(http.Response res) {
    final text = utf8.decode(res.bodyBytes);
    if (res.statusCode >= 200 && res.statusCode < 300) return text.isEmpty ? null : jsonDecode(text);
    String msg = 'Lỗi máy chủ (${res.statusCode})';
    try {
      final j = jsonDecode(text);
      if (j is Map && j['message'] != null) msg = j['message'];
    } catch (_) {}
    throw ApiException(msg, res.statusCode);
  }

  // UC01
  Future<List<Language>> languages() async =>
      (await _get('/api/languages') as List).map((e) => Language.fromJson(e)).toList();

  // UC02
  Future<List<Point>> points(String lang) async =>
      (await _get('/api/points', {'lang': lang}) as List).map((e) => Point.fromJson(e)).toList();

  Future<Point> point(int id, String lang) async => Point.fromJson(await _get('/api/points/$id', {'lang': lang}));

  // UC03
  Future<List<Point>> search(String q, String lang) async =>
      (await _get('/api/points/search', {'q': q, 'lang': lang}) as List).map((e) => Point.fromJson(e)).toList();

  // UC05
  Future<Point> pointByQr(String code, String lang) async =>
      Point.fromJson(await _get('/api/points/qr/${Uri.encodeComponent(code)}', {'lang': lang}));

  // UC08
  Future<List<Tour>> tours(String lang) async =>
      (await _get('/api/tours', {'lang': lang}) as List).map((e) => Tour.fromJson(e)).toList();

  Future<Tour> tour(int id, String lang) async => Tour.fromJson(await _get('/api/tours/$id', {'lang': lang}));

  // UC09
  Future<Map<String, dynamic>> packageInfo(int tourId, String lang) async =>
      Map<String, dynamic>.from(await _get('/api/tours/$tourId/package', {'lang': lang}));

  Future<List<int>> download(String url, void Function(double)? onProgress) async {
    final req = http.Request('GET', Uri.parse(url));
    final res = await _client.send(req).timeout(_timeout);
    if (res.statusCode != 200) throw ApiException('Tải gói lỗi (${res.statusCode})', res.statusCode);
    final total = res.contentLength ?? 0;
    final bytes = <int>[];
    await for (final chunk in res.stream.timeout(const Duration(seconds: 20))) {
      bytes.addAll(chunk);
      if (total > 0) onProgress?.call((bytes.length / total).clamp(0.0, 1.0));
    }
    return bytes;
  }

  // UC10
  Future<void> registerDevice(String deviceId, String lang, {String? fcmToken}) => _post('/api/devices', {
        'deviceId': deviceId,
        'fcmToken': fcmToken,
        'lang': lang,
        'platform': Platform.isAndroid ? 'Android' : 'iOS',
      });

  Future<List<Notice>> notices(String lang) async =>
      (await _get('/api/notices/active', {'lang': lang}) as List).map((e) => Notice.fromJson(e)).toList();

  // UC05/06/07
  Future<void> logListen(String deviceId, int pointId, String lang, String source, int seconds) =>
      _post('/api/listens', {'deviceId': deviceId, 'pointId': pointId, 'lang': lang, 'source': source, 'seconds': seconds});

  // UC11
  Future<Map<String, dynamic>> rate(String deviceId, int pointId, int stars, String? comment) async =>
      Map<String, dynamic>.from(await _post('/api/ratings',
          {'deviceId': deviceId, 'pointId': pointId, 'stars': stars, 'comment': comment}));

  // Dashboard "Trực tiếp": báo đang mở app / đang nghe (PresenceController)
  Future<void> presencePing(String deviceId, String pageId, int? pointId, String lang, bool listening, String platform) =>
      _post('/api/presence/ping', {
        'sessionId': deviceId,
        'pageId': pageId,
        'source': 'app',
        'pointId': pointId,
        'lang': lang,
        'listening': listening,
        'platform': platform,
      });

  Future<void> presenceLeave(String deviceId, String pageId) async {
    await _client
        .post(_u('/api/presence/leave', {'sid': deviceId, 'page': pageId}))
        .timeout(const Duration(seconds: 5));
  }
}
