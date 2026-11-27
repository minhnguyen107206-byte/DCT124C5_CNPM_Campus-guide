// Các lớp dữ liệu nhận từ API (xem backend/Controllers/Api/AppApiController.cs)

class Language {
  final String code;
  final String name;
  final String? flag;
  final bool isFallback;

  Language({required this.code, required this.name, this.flag, this.isFallback = false});

  factory Language.fromJson(Map<String, dynamic> j) => Language(
        code: j['code'],
        name: j['name'],
        flag: j['flag'],
        isFallback: j['isFallback'] ?? false,
      );
}

class Point {
  final int id;
  final String name;
  final String type;
  final String typeIcon;
  final double lat;
  final double lng;
  final int radius;
  final String? floor;
  final String? hours;
  final String? image;
  final String? qrCode;
  final String? keywords;
  final String title;
  final String text;
  final String lang;
  final bool isFallback;
  final String? audioUrl;
  final int audioSeconds;
  final double? rating;
  final int ratingCount;

  /// Đường dẫn file cục bộ khi điểm lấy từ gói offline
  final String? localAudio;
  final String? localImage;

  Point({
    required this.id,
    required this.name,
    required this.type,
    required this.typeIcon,
    required this.lat,
    required this.lng,
    required this.radius,
    this.floor,
    this.hours,
    this.image,
    this.qrCode,
    this.keywords,
    required this.title,
    required this.text,
    required this.lang,
    required this.isFallback,
    this.audioUrl,
    this.audioSeconds = 0,
    this.rating,
    this.ratingCount = 0,
    this.localAudio,
    this.localImage,
  });

  /// Nghe được: có file audio, hoặc có văn bản để đọc bằng giọng của điện thoại.
  bool get hasAudio => audioUrl != null || localAudio != null || text.trim().isNotEmpty;

  Point copyWith({double? rating, int? ratingCount}) => Point(
        id: id,
        name: name,
        type: type,
        typeIcon: typeIcon,
        lat: lat,
        lng: lng,
        radius: radius,
        floor: floor,
        hours: hours,
        image: image,
        qrCode: qrCode,
        keywords: keywords,
        title: title,
        text: text,
        lang: lang,
        isFallback: isFallback,
        audioUrl: audioUrl,
        audioSeconds: audioSeconds,
        rating: rating ?? this.rating,
        ratingCount: ratingCount ?? this.ratingCount,
        localAudio: localAudio,
        localImage: localImage,
      );

  factory Point.fromJson(Map<String, dynamic> j, {String? baseDir}) {
    String? local(String? rel) => (baseDir != null && rel != null) ? '$baseDir/$rel' : null;
    return Point(
      id: j['id'],
      name: j['name'] ?? '',
      type: j['type'] ?? '',
      typeIcon: j['typeIcon'] ?? 'place',
      lat: (j['lat'] as num).toDouble(),
      lng: (j['lng'] as num).toDouble(),
      radius: j['radius'] ?? 30,
      floor: j['floor'],
      hours: j['hours'],
      image: baseDir == null ? j['image'] : null,
      qrCode: j['qrCode'],
      keywords: j['keywords'],
      title: j['title'] ?? j['name'] ?? '',
      text: j['text'] ?? '',
      lang: j['lang'] ?? 'vi',
      isFallback: j['isFallback'] ?? false,
      audioUrl: baseDir == null ? j['audioUrl'] : null,
      audioSeconds: j['audioSeconds'] ?? 0,
      rating: (j['rating'] as num?)?.toDouble(),
      ratingCount: j['ratingCount'] ?? 0,
      localAudio: local(j['audioUrl']),
      localImage: local(j['image']),
    );
  }
}

class Tour {
  final int id;
  final String name;
  final String? description;
  final String? cover;
  final int pointCount;
  final int distance;
  final int minutes;
  final int? packageVersion;
  final int? packageSize;
  final List<Point> points;

  Tour({
    required this.id,
    required this.name,
    this.description,
    this.cover,
    this.pointCount = 0,
    this.distance = 0,
    this.minutes = 0,
    this.packageVersion,
    this.packageSize,
    this.points = const [],
  });

  factory Tour.fromJson(Map<String, dynamic> j) => Tour(
        id: j['id'] ?? j['tourId'],
        name: j['name'] ?? '',
        description: j['description'],
        cover: j['cover'],
        pointCount: j['pointCount'] ?? (j['points'] as List?)?.length ?? 0,
        distance: j['distance'] ?? 0,
        minutes: j['minutes'] ?? 0,
        packageVersion: j['packageVersion'],
        packageSize: j['packageSize'],
        points: ((j['points'] as List?) ?? []).map((p) => Point.fromJson(p)).toList(),
      );
}

class Notice {
  final int id;
  final String title;
  final String body;
  final double lat;
  final double lng;
  final int radius;
  final bool urgent;
  final DateTime endAt;

  Notice({
    required this.id,
    required this.title,
    required this.body,
    required this.lat,
    required this.lng,
    required this.radius,
    required this.urgent,
    required this.endAt,
  });

  factory Notice.fromJson(Map<String, dynamic> j) => Notice(
        id: j['id'],
        title: j['title'] ?? '',
        body: j['body'] ?? '',
        lat: (j['lat'] as num).toDouble(),
        lng: (j['lng'] as num).toDouble(),
        radius: j['radius'] ?? 100,
        urgent: j['urgent'] ?? false,
        endAt: DateTime.tryParse(j['endAt'] ?? '') ?? DateTime.now(),
      );
}

class HistoryItem {
  final int pointId;
  final String name;
  final String? image;
  final String lang;
  final DateTime time;

  HistoryItem({required this.pointId, required this.name, this.image, required this.lang, required this.time});

  Map<String, dynamic> toJson() =>
      {'pointId': pointId, 'name': name, 'image': image, 'lang': lang, 'time': time.toIso8601String()};

  factory HistoryItem.fromJson(Map<String, dynamic> j) => HistoryItem(
        pointId: j['pointId'],
        name: j['name'],
        image: j['image'],
        lang: j['lang'],
        time: DateTime.parse(j['time']),
      );
}
