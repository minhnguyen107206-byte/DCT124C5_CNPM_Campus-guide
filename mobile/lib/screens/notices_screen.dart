import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../widgets/point_card.dart';

/// UC10 – Danh sách thông báo đang hiệu lực (vẫn xem được khi không cấp quyền thông báo – E1).
class NoticesScreen extends StatelessWidget {
  const NoticesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    return Scaffold(
      appBar: AppBar(title: Text(app.t('notices'))),
      body: RefreshIndicator(
        onRefresh: app.refreshNotices,
        child: app.notices.isEmpty
            ? ListView(children: [Padding(padding: const EdgeInsets.all(32), child: Center(child: Text(app.t('noNotices'))))])
            : ListView(children: [
                for (final n in app.notices)
                  Card(
                    margin: const EdgeInsets.fromLTRB(12, 8, 12, 0),
                    child: ListTile(
                      leading: Icon(n.urgent ? Icons.warning_amber : Icons.campaign, color: n.urgent ? Colors.red : null),
                      title: Text(n.title),
                      subtitle: Text(n.body),
                      trailing: app.position == null
                          ? null
                          : Text(formatDistance(_dist(app, n.lat, n.lng))),
                    ),
                  ),
              ]),
      ),
    );
  }

  double? _dist(AppState app, double lat, double lng) {
    final pos = app.position;
    return pos == null ? null : Geolocator.distanceBetween(pos.latitude, pos.longitude, lat, lng);
  }
}
