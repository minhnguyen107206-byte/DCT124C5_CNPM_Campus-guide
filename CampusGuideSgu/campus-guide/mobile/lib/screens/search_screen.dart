import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';
import '../widgets/point_card.dart';
import '../widgets/point_image.dart';
import 'point_detail_screen.dart';

/// Bỏ dấu tiếng Việt để tìm kiếm khi offline (giống AppApiController.Normalize ở server).
String normalizeVi(String s) {
  const from = 'àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ';
  const to = 'aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd';
  final lower = s.toLowerCase();
  final sb = StringBuffer();
  for (final ch in lower.split('')) {
    final i = from.indexOf(ch);
    sb.write(i >= 0 ? to[i] : ch);
  }
  return sb.toString().trim();
}

/// UC03 – Tìm kiếm điểm theo tên hoặc chức năng.
class SearchScreen extends StatefulWidget {
  const SearchScreen({super.key});

  @override
  State<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends State<SearchScreen> {
  final _ctrl = TextEditingController();
  Timer? _debounce;
  List<Point>? _results;
  bool _loading = false;

  void _onChanged(String q) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () => _search(q));
  }

  Future<void> _search(String q) async {
    final app = context.read<AppState>();
    if (q.trim().length < 2) {
      setState(() => _results = null); // E1: chờ nhập thêm
      return;
    }
    setState(() => _loading = true);
    List<Point> res;
    try {
      res = await app.api.search(q, app.lang!);
    } catch (_) {
      // A1: mất mạng → tìm trong dữ liệu offline
      final key = normalizeVi(q);
      res = app.points.where((p) => normalizeVi('${p.name} ${p.keywords ?? ''} ${p.text}').contains(key)).toList();
    }
    if (!mounted) return;
    setState(() {
      _results = res;
      _loading = false;
    });
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final popular = [...app.points]..sort((a, b) => (b.ratingCount).compareTo(a.ratingCount));
    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _ctrl,
          autofocus: true,
          decoration: InputDecoration(hintText: app.t('search'), border: InputBorder.none),
          onChanged: _onChanged,
          onSubmitted: _search,
        ),
      ),
      body: _loading
          ? const LinearProgressIndicator()
          : _results == null
              ? const SizedBox.shrink()
              : _results!.isEmpty
                  ? ListView(children: [
                      // E2: không có kết quả → gợi ý điểm phổ biến
                      ListTile(leading: const Icon(Icons.search_off), title: Text(app.t('noResult'))),
                      ListTile(title: Text(app.t('popular'), style: Theme.of(context).textTheme.titleSmall)),
                      for (final p in popular.take(5)) _tile(context, app, p),
                    ])
                  : ListView(children: [for (final p in _results!) _tile(context, app, p)]),
    );
  }

  Widget _tile(BuildContext context, AppState app, Point p) => ListTile(
        leading: ClipRRect(borderRadius: BorderRadius.circular(8), child: PointImage(point: p, size: 48)),
        title: Text(p.name),
        subtitle: Text([p.type, if (app.distanceTo(p) != null) formatDistance(app.distanceTo(p))].join(' · ')),
        onTap: () => Navigator.pushReplacement(context, MaterialPageRoute(builder: (_) => PointDetailScreen(point: p))),
      );
}
