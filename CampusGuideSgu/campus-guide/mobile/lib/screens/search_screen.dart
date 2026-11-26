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
  int _reqId = 0;

  static const _quickTagsVi = ['Phòng Đào tạo', 'Nộp học phí', 'Thư viện', 'Căng tin', 'Nhà xe', 'Hội trường'];
  static const _quickTagsEn = ['Academic Affairs', 'Tuition', 'Library', 'Canteen', 'Parking', 'Hall'];

  void _onChanged(String q) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () => _search(q));
  }

  Future<void> _search(String q, {bool saveRecent = false}) async {
    final app = context.read<AppState>();
    final req = ++_reqId;
    if (q.trim().length < 2) {
      setState(() {
        _results = null;
        _loading = false;
      }); // E1: chờ nhập thêm
      return;
    }
    setState(() => _loading = true);
    List<Point> res;
    try {
      res = await app.api.search(q, app.lang!);
    } catch (_) {
      // A1: mất mạng → tìm trong dữ liệu offline
      final key = normalizeVi(q);
      res = app.points.where((p) => normalizeVi('${p.name} ${p.title} ${p.keywords ?? ''} ${p.text}').contains(key)).toList();
    }
    if (!mounted || req != _reqId) return;
    if (saveRecent && res.isNotEmpty) {
      app.addRecentSearch(q);
    }
    setState(() {
      _results = res;
      _loading = false;
    });
  }

  void _pickKeyword(String kw) {
    _ctrl.text = kw;
    _ctrl.selection = TextSelection.fromPosition(TextPosition(offset: kw.length));
    _search(kw, saveRecent: true);
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
    final quickTags = app.lang == 'vi' ? _quickTagsVi : _quickTagsEn;
    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _ctrl,
          autofocus: true,
          decoration: InputDecoration(
            hintText: app.t('search'),
            border: InputBorder.none,
            suffixIcon: _ctrl.text.isNotEmpty
                ? IconButton(
                    icon: const Icon(Icons.clear),
                    onPressed: () {
                      _ctrl.clear();
                      _search('');
                    },
                  )
                : null,
          ),
          onChanged: _onChanged,
          onSubmitted: (q) => _search(q, saveRecent: true),
        ),
      ),
      body: _loading
          ? const LinearProgressIndicator()
          : _results == null
              ? ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    if (app.recentSearches.isNotEmpty) ...[
                      Text(app.t('recentSearches'), style: Theme.of(context).textTheme.titleSmall),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: app.recentSearches
                            .map((r) => InputChip(
                                  avatar: const Icon(Icons.history, size: 16),
                                  label: Text(r),
                                  onPressed: () => _pickKeyword(r),
                                  onDeleted: () => app.removeRecentSearch(r),
                                ))
                            .toList(),
                      ),
                      const SizedBox(height: 20),
                    ],
                    Text(app.t('quickSearch'), style: Theme.of(context).textTheme.titleSmall),
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: quickTags
                          .map((tag) => ActionChip(
                                avatar: const Icon(Icons.bolt, size: 16),
                                label: Text(tag),
                                onPressed: () => _pickKeyword(tag),
                              ))
                          .toList(),
                    ),
                  ],
                )
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
        onTap: () {
          if (_ctrl.text.trim().length >= 2) app.addRecentSearch(_ctrl.text);
          Navigator.pushReplacement(context, MaterialPageRoute(builder: (_) => PointDetailScreen(point: p)));
        },
      );
}
