// Thêm quyền camera, vị trí, cho phép HTTP (máy chủ chạy thử) vào project Android/iOS.
// Chạy SAU khi đã tạo thư mục android/ và ios/ bằng:  flutter create --org vn.edu.sgu --project-name campus_guide .
// Cách chạy:  dart run tool/patch_platforms.dart
import 'dart:io';

void main() {
  _android();
  _ios();
  stdout.writeln('Xong. Chạy: flutter pub get && flutter run');
}

void _android() {
  final f = File('android/app/src/main/AndroidManifest.xml');
  if (!f.existsSync()) {
    stdout.writeln('Chưa có android/ – bỏ qua.');
    return;
  }
  var s = f.readAsStringSync();
  const perms = [
    'android.permission.INTERNET',
    'android.permission.CAMERA',
    'android.permission.ACCESS_FINE_LOCATION',
    'android.permission.ACCESS_COARSE_LOCATION',
  ];
  for (final p in perms) {
    if (!s.contains(p)) {
      s = s.replaceFirst('<application', '<uses-permission android:name="$p"/>\n    <application');
    }
  }
  // Android 11+: flutter_tts cần khai báo dịch vụ đọc văn bản (TTS) mới tìm được giọng đọc của máy
  if (!s.contains('android.intent.action.TTS_SERVICE')) {
    const q = '<queries>\n        <intent>\n            <action android:name="android.intent.action.TTS_SERVICE" />\n        </intent>\n    </queries>\n    ';
    s = s.replaceFirst('<application', '$q<application');
  }
  if (!s.contains('usesCleartextTraffic')) {
    s = s.replaceFirst('<application', '<application\n        android:usesCleartextTraffic="true"');
  }
  f.writeAsStringSync(s);
  stdout.writeln('Đã cập nhật AndroidManifest.xml');
}

void _ios() {
  final f = File('ios/Runner/Info.plist');
  if (!f.existsSync()) {
    stdout.writeln('Chưa có ios/ – bỏ qua (chỉ máy Mac build được iOS).');
    return;
  }
  var s = f.readAsStringSync();
  final add = StringBuffer();
  void key(String k, String v) {
    if (!s.contains('<key>$k</key>')) add.writeln('\t<key>$k</key>\n\t<string>$v</string>');
  }

  key('NSCameraUsageDescription', 'Quét mã QR tại các điểm để nghe thuyết minh');
  key('NSLocationWhenInUseUsageDescription', 'Hiển thị vị trí của bạn, chỉ đường và tự động thuyết minh khi đến gần một điểm');
  // iOS 14+: iPhone thật gọi máy chủ qua IP LAN cần quyền "Mạng cục bộ"
  key('NSLocalNetworkUsageDescription', 'Kết nối tới máy chủ CampusGuide trong cùng mạng Wi-Fi');
  if (!s.contains('NSAppTransportSecurity')) {
    add.writeln('\t<key>NSAppTransportSecurity</key>\n\t<dict>\n\t\t<key>NSAllowsArbitraryLoads</key>\n\t\t<true/>\n\t</dict>');
  }
  final idx = s.lastIndexOf('</dict>');
  s = s.substring(0, idx) + add.toString() + s.substring(idx);
  f.writeAsStringSync(s);
  stdout.writeln('Đã cập nhật Info.plist');
}
