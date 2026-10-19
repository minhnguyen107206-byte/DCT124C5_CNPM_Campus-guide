import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';

/// UC11 – Đánh giá điểm 1–5 sao kèm nhận xét.
Future<void> showRatingDialog(BuildContext context, Point p) async {
  final app = context.read<AppState>();
  int stars = app.myRatings[p.id] ?? 0;
  final ctrl = TextEditingController();
  String? err;
  await showDialog(
    context: context,
    builder: (ctx) => StatefulBuilder(
      builder: (ctx, setState) => AlertDialog(
        title: Text('${app.t('rate')}: ${p.name}'),
        content: Column(mainAxisSize: MainAxisSize.min, children: [
          Row(mainAxisAlignment: MainAxisAlignment.center, children: [
            for (int i = 1; i <= 5; i++)
              IconButton(
                iconSize: 36,
                icon: Icon(i <= stars ? Icons.star : Icons.star_border, color: Colors.amber),
                onPressed: () => setState(() => stars = i),
              ),
          ]),
          TextField(controller: ctrl, maxLength: 500, maxLines: 3, decoration: InputDecoration(hintText: app.t('comment'))),
          if (err != null) Text(err!, style: const TextStyle(color: Colors.red)),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('×')),
          FilledButton(
            onPressed: () async {
              final msg = await app.rate(p.id, stars, ctrl.text.trim().isEmpty ? null : ctrl.text.trim());
              if (msg == null || msg == app.t('savedOffline')) {
                if (ctx.mounted) Navigator.pop(ctx);
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg ?? app.t('thanks'))));
                }
              } else {
                setState(() => err = msg);
              }
            },
            child: Text(app.t('send')),
          ),
        ],
      ),
    ),
  );
}
