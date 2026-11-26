import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../widgets/point_card.dart';
import '../widgets/point_image.dart';
import 'point_detail_screen.dart';
import 'search_screen.dart';

/// UC02 – A1: xem dạng danh sách, sắp xếp theo khoảng cách; ô tìm kiếm (UC03).
class ListScreen extends StatelessWidget {
  const ListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final list = app.sortedByDistance(app.points);
    return RefreshIndicator(
      onRefresh: app.loadPoints,
      child: ListView(
        padding: const EdgeInsets.only(bottom: 90),
        children: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: InkWell(
              borderRadius: BorderRadius.circular(28),
              onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const SearchScreen())),
              child: IgnorePointer(
                child: SearchBar(hintText: app.t('search'), leading: const Icon(Icons.search), elevation: const WidgetStatePropertyAll(1)),
              ),
            ),
          ),
          if (app.error != null) ListTile(title: Text(app.error!), trailing: TextButton(onPressed: app.loadPoints, child: Text(app.t('retry')))),
          for (final p in list)
            ListTile(
              leading: ClipRRect(borderRadius: BorderRadius.circular(8), child: PointImage(point: p, size: 52)),
              title: Text(p.name),
              subtitle: Text([p.type, if (p.floor != null) p.floor!, if (app.distanceTo(p) != null) formatDistance(app.distanceTo(p))].join(' · ')),
              trailing: p.rating != null ? Row(mainAxisSize: MainAxisSize.min, children: [
                const Icon(Icons.star, size: 16, color: Colors.amber), Text(' ${p.rating}'),
              ]) : null,
              onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => PointDetailScreen(point: p))),
            ),
        ],
      ),
    );
  }
}
