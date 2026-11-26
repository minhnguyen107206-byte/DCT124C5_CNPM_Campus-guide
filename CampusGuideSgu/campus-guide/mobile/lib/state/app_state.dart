import 'dart:async';
import 'dart:convert';
import 'dart:ui' show PlatformDispatcher;

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';

import '../models.dart';
import '../services/api.dart';
import '../services/offline_store.dart';
import '../strings.dart';

/// Trạng thái chung của app: ngôn ngữ, danh sách điểm, vị trí, thông báo, lịch sử.
class AppState extends ChangeNotifier {
  late SharedPreferences _prefs;
  late Api api;
  final offline = OfflineStore();

  String deviceId = '';
  String? lang; // null = chưa chọn (UC01)
  List<Language> languages = [];
  List<Point> points = [];
  bool loadingPoints = false;
  bool offlineMode = false;
  String? error;

  // ---- vị trí (UC02, UC04, UC06, UC10) ----
  Position? position;
  bool locationGranted = false;
  StreamSubscription<Position>? _posSub;
  final _positionCtrl = StreamController<Position>.broadcast();
  Stream<Position> get positionStream => _positionCtrl.stream;

  // ---- tự động thuyết minh (UC06) ----
  bool autoNarration = false;
  bool tourRunning = false; // đang ở màn đi tour → tour tự phát, geofence chung tạm nghỉ (tránh phát 2 lần)
  final Map<int, DateTime> _lastAutoPlayed = {};
  void Function(Point p)? onGeofenceEnter; // đăng ký từ màn hình chính

  // ---- thông báo (UC10) ----
  List<Notice> notices = [];
  final Set<int> _seenNotices = {};
  void Function(Notice n)? onNotice;
  Timer? _noticeTimer;

  // ---- lịch sử, đánh giá chờ gửi (UC11) ----
  List<HistoryItem> history = [];
  final Map<int, int> myRatings = {};

  String t(String key) => tr(lang ?? 'vi', key);

  Future<void> init() async {
    _prefs = await SharedPreferences.getInstance();
    api = Api(_prefs.getString('baseUrl') ?? Api.defaultBaseUrl());
    deviceId = _prefs.getString('deviceId') ?? const Uuid().v4();
    await _prefs.setString('deviceId', deviceId);
    lang = _prefs.getString('lang');
    autoNarration = _prefs.getBool('auto') ?? false;
    _seenNotices.addAll((_prefs.getStringList('seenNotices') ?? []).map(int.parse));
    history = (_prefs.getStringList('history') ?? []).map((s) => HistoryItem.fromJson(jsonDecode(s))).toList();
    final r = _prefs.getString('myRatings');
    if (r != null) (jsonDecode(r) as Map).forEach((k, v) => myRatings[int.parse(k)] = v);
    if (lang != null) afterLanguageReady(); // không chặn giao diện khi máy chủ chưa chạy
  }

  // ======================= UC01 =======================
  Future<void> loadLanguages() async {
    try {
      languages = await api.languages();
      error = null;
    } catch (_) {
      // E1: mất mạng lần đầu → danh sách mặc định
      languages = [
        Language(code: 'vi', name: 'Tiếng Việt', flag: '🇻🇳'),
        Language(code: 'en', name: 'English', flag: '🇬🇧', isFallback: true),
      ];
    }
    notifyListeners();
  }

  /// A1: chọn sẵn ngôn ngữ của điện thoại nếu có trong danh sách
  String suggestedLanguage() {
    final sys = PlatformDispatcher.instance.locale.languageCode;
    return languages.any((l) => l.code == sys)
        ? sys
        : languages.any((l) => l.code.startsWith(sys))
            ? languages.firstWhere((l) => l.code.startsWith(sys)).code
            : (languages.isNotEmpty ? languages.first.code : 'vi');
  }

  Future<void> setLanguage(String code) async {
    final first = lang == null;
    lang = code;
    await _prefs.setString('lang', code);
    notifyListeners();
    if (first) {
      await afterLanguageReady();
    } else {
      await loadPoints();
      api.registerDevice(deviceId, code).catchError((_) {});
    }
  }

  Future<void> afterLanguageReady() async {
    api.registerDevice(deviceId, lang!).catchError((_) {});
    await loadPoints();
    await startLocation();
    _flushPendingRatings();
    await refreshNotices();
    _noticeTimer?.cancel();
    _noticeTimer = Timer.periodic(const Duration(minutes: 2), (_) => refreshNotices());
  }

  // ======================= UC02 =======================
  Future<void> loadPoints() async {
    loadingPoints = true;
    notifyListeners();
    try {
      points = await api.points(lang!);
      offlineMode = false;
      error = null;
    } catch (_) {
      // E2: dùng dữ liệu các tour đã tải
      points = await offline.allPoints();
      offlineMode = true;
      error = points.isEmpty ? t('noNetwork') : null;
    }
    loadingPoints = false;
    notifyListeners();
  }

  double? distanceTo(Point p) => position == null
      ? null
      : Geolocator.distanceBetween(position!.latitude, position!.longitude, p.lat, p.lng);

  List<Point> sortedByDistance(List<Point> list) {
    if (position == null) return list;
    final copy = [...list];
    copy.sort((a, b) => distanceTo(a)!.compareTo(distanceTo(b)!));
    return copy;
  }

  // ======================= vị trí =======================
  Future<bool> ensureLocationPermission() async {
    if (!await Geolocator.isLocationServiceEnabled()) return false;
    var perm = await Geolocator.checkPermission();
    if (perm == LocationPermission.denied) perm = await Geolocator.requestPermission();
    locationGranted = perm == LocationPermission.always || perm == LocationPermission.whileInUse;
    notifyListeners();
    return locationGranted;
  }

  Future<void> startLocation() async {
    if (_posSub != null) return;
    if (!await ensureLocationPermission()) return; // UC02 E1
    _posSub = Geolocator.getPositionStream(
      locationSettings: const LocationSettings(accuracy: LocationAccuracy.high, distanceFilter: 3),
    ).listen(_onPosition, onError: (_) {});
  }

  void _onPosition(Position p) {
    position = p;
    _positionCtrl.add(p);
    _checkGeofence(p);
    _checkNotices(p);
    notifyListeners();
  }

  // ======================= UC06 – geofence =======================
  Future<void> setAutoNarration(bool on) async {
    if (on && !await ensureLocationPermission()) {
      autoNarration = false; // E1
    } else {
      autoNarration = on;
      if (on) await startLocation();
    }
    await _prefs.setBool('auto', autoNarration);
    notifyListeners();
  }

  void _checkGeofence(Position pos) {
    if (!autoNarration || tourRunning || pos.accuracy > 50) return; // bỏ qua lần đo sai số > 50 m
    Point? best;
    double bestD = double.infinity;
    for (final p in points) {
      final d = Geolocator.distanceBetween(pos.latitude, pos.longitude, p.lat, p.lng);
      if (d <= p.radius && d < bestD) {
        best = p; // A1: nhiều điểm giao nhau → chọn điểm gần nhất
        bestD = d;
      }
    }
    if (best == null) return;
    final last = _lastAutoPlayed[best.id];
    if (last != null && DateTime.now().difference(last).inMinutes < 30) return; // không phát lại trong 30 phút
    _lastAutoPlayed[best.id] = DateTime.now();
    onGeofenceEnter?.call(best);
  }

  // ======================= UC10 – thông báo theo khu vực =======================
  Future<void> refreshNotices() async {
    if (lang == null) return;
    try {
      notices = await api.notices(lang!);
      for (final n in notices.where((n) => n.urgent && !_seenNotices.contains(n.id))) {
        _markSeen(n.id);
        onNotice?.call(n); // A1: thông báo khẩn hiện ngay, không cần vị trí
      }
      if (position != null) _checkNotices(position!);
      notifyListeners();
    } catch (_) {}
  }

  void _checkNotices(Position pos) {
    for (final n in notices) {
      if (_seenNotices.contains(n.id)) continue;
      final d = Geolocator.distanceBetween(pos.latitude, pos.longitude, n.lat, n.lng);
      if (d <= n.radius) {
        _markSeen(n.id);
        onNotice?.call(n);
      }
    }
  }

  void _markSeen(int id) {
    _seenNotices.add(id);
    _prefs.setStringList('seenNotices', _seenNotices.map((e) => e.toString()).toList());
  }

  // ======================= UC11 – lịch sử, đánh giá =======================
  Future<void> addHistory(Point p) async {
    history.removeWhere((h) => h.pointId == p.id);
    history.insert(0, HistoryItem(pointId: p.id, name: p.name, image: p.image, lang: p.lang, time: DateTime.now()));
    if (history.length > 100) history = history.sublist(0, 100);
    await _prefs.setStringList('history', history.map((h) => jsonEncode(h.toJson())).toList());
    notifyListeners();
  }

  /// Trả về null nếu gửi thành công, ngược lại là thông báo (đã lưu tạm khi mất mạng).
  Future<String?> rate(int pointId, int stars, String? comment) async {
    if (stars < 1) return t('chooseStars');
    if ((comment?.length ?? 0) > 500) return 'Max 500';
    myRatings[pointId] = stars;
    await _prefs.setString('myRatings', jsonEncode(myRatings.map((k, v) => MapEntry(k.toString(), v))));
    try {
      await api.rate(deviceId, pointId, stars, comment);
      notifyListeners();
      return null;
    } on ApiException catch (e) {
      return e.message;
    } catch (_) {
      // A1: mất mạng → lưu tạm
      final pending = _prefs.getStringList('pendingRatings') ?? [];
      pending.add(jsonEncode({'pointId': pointId, 'stars': stars, 'comment': comment}));
      await _prefs.setStringList('pendingRatings', pending);
      return t('savedOffline');
    }
  }

  Future<void> _flushPendingRatings() async {
    final pending = _prefs.getStringList('pendingRatings') ?? [];
    if (pending.isEmpty) return;
    final left = <String>[];
    for (final s in pending) {
      final j = jsonDecode(s);
      try {
        await api.rate(deviceId, j['pointId'], j['stars'], j['comment']);
      } catch (_) {
        left.add(s);
      }
    }
    await _prefs.setStringList('pendingRatings', left);
  }

  // ======================= Cài đặt =======================
  Future<void> setBaseUrl(String url) async {
    var u = url.trim();
    if (u.endsWith('/')) u = u.substring(0, u.length - 1);
    api.baseUrl = u;
    await _prefs.setString('baseUrl', u);
    await loadPoints();
  }

  // Tiến độ tour (UC08 A2)
  Future<void> saveTourProgress(int tourId, int index) => _prefs.setString('tourProgress', '$tourId:$index');
  Future<void> clearTourProgress() => _prefs.remove('tourProgress');
  int? tourProgress(int tourId) {
    final s = _prefs.getString('tourProgress');
    if (s == null) return null;
    final parts = s.split(':');
    return int.parse(parts[0]) == tourId ? int.parse(parts[1]) : null;
  }

  @override
  void dispose() {
    _posSub?.cancel();
    _noticeTimer?.cancel();
    _positionCtrl.close();
    super.dispose();
  }
}
