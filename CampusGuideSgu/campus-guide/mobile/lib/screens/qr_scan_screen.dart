import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../services/api.dart';
import '../state/app_state.dart';
import '../state/audio_controller.dart';
import 'point_detail_screen.dart';

/// UC05 – Quét QR tại điểm để nghe thuyết minh.
/// QR chứa liên kết http://<máy chủ>/q/{MaQR}; app lấy phần mã cuối cùng.
/// Nếu không gọi được máy chủ đang cài trong app (VD iPhone thật vẫn để localhost), app thử máy chủ ghi trong
/// mã QR; được thì tự lưu địa chỉ đó làm máy chủ (khỏi phải gõ IP trong Cài đặt).
class QrScanScreen extends StatefulWidget {
  const QrScanScreen({super.key});

  @override
  State<QrScanScreen> createState() => _QrScanScreenState();
}

class _QrScanScreenState extends State<QrScanScreen> {
  final _ctrl = MobileScannerController(detectionSpeed: DetectionSpeed.noDuplicates);
  bool _busy = false;

  Future<void> _onDetect(BarcodeCapture cap) async {
    if (_busy || cap.barcodes.isEmpty) return;
    final raw = cap.barcodes.first.rawValue;
    if (raw == null) return;
    _busy = true;
    final app = context.read<AppState>();
    final audio = context.read<AudioController>();
    final code = _codeOf(raw);
    Point? p;
    String? error;
    String? switchedTo;
    try {
      p = await app.api.pointByQr(code, app.lang!);
    } on ApiException catch (e) {
      error = e.status == 404 ? app.t('invalidQr') : e.message; // E1
    } catch (_) {
      // Không gọi được máy chủ đang cài → thử máy chủ ghi trong mã QR
      final origin = _originOf(raw);
      if (origin != null && origin != app.api.baseUrl) {
        try {
          p = await Api(origin).pointByQr(code, app.lang!);
          await app.setBaseUrl(origin);
          switchedTo = origin;
        } on ApiException catch (e) {
          error = e.status == 404 ? app.t('invalidQr') : e.message;
        } catch (_) {}
      }
      if (p == null && error == null) {
        // mất mạng → tìm trong gói offline
        final local = await app.offline.allPoints();
        final hit = local.where((x) => x.qrCode == code);
        if (hit.isNotEmpty) {
          p = hit.first;
        } else {
          error = app.t('needOffline'); // E2
        }
      }
    }
    if (!mounted) return;
    if (switchedTo != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('${app.t('server')}: $switchedTo')));
    }
    if (p == null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error ?? app.t('invalidQr'))));
      await Future.delayed(const Duration(seconds: 2));
      _busy = false;
      return;
    }
    if (p.isFallback) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(app.t('fallbackLang')))); // A1
    }
    audio.playPoint(p, source: 'QR');
    Navigator.pushReplacement(context, MaterialPageRoute(builder: (_) => PointDetailScreen(point: p!, source: 'QR')));
  }

  /// Mã điểm = phần cuối của đường dẫn (bỏ ?lang=..., dấu / cuối); QR chỉ chứa mã trần cũng được.
  static String _codeOf(String raw) {
    final s = raw.trim();
    final u = Uri.tryParse(s);
    if (u != null && u.hasScheme && u.pathSegments.isNotEmpty) {
      final segs = u.pathSegments.where((x) => x.isNotEmpty).toList();
      if (segs.isNotEmpty) return segs.last;
    }
    final parts = s.split('?').first.split('/').where((x) => x.isNotEmpty).toList();
    return parts.isEmpty ? s : parts.last;
  }

  /// http://192.168.1.10:5000/q/CG0001 → http://192.168.1.10:5000 (chỉ nhận liên kết dạng /q/...).
  static String? _originOf(String raw) {
    final u = Uri.tryParse(raw.trim());
    if (u == null || !(u.scheme == 'http' || u.scheme == 'https') || u.host.isEmpty) return null;
    if (u.host == 'localhost' || u.host == '127.0.0.1') return null;
    if (u.pathSegments.length < 2 || u.pathSegments[u.pathSegments.length - 2] != 'q') return null;
    return u.origin;
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final app = context.read<AppState>();
    return Scaffold(
      appBar: AppBar(title: Text(app.t('scanQr')), actions: [
        IconButton(icon: const Icon(Icons.flash_on), onPressed: _ctrl.toggleTorch),
      ]),
      body: Stack(alignment: Alignment.center, children: [
        MobileScanner(controller: _ctrl, onDetect: _onDetect),
        Container(
          width: 250,
          height: 250,
          decoration: BoxDecoration(border: Border.all(color: Colors.white, width: 3), borderRadius: BorderRadius.circular(16)),
        ),
      ]),
    );
  }
}
