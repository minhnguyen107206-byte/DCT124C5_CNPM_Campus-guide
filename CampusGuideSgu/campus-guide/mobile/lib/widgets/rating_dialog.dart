import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models.dart';
import '../state/app_state.dart';

/// UC11 – Đánh giá điểm 1–5 sao kèm nhận xét.
Future<void> showRatingDialog(BuildContext context, Point p) async {
  final msg = await showDialog<String>(
    context: context,
    builder: (_) => _RatingDialog(point: p),
  );
  if (msg != null && context.mounted) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }
}

class _RatingDialog extends StatefulWidget {
  final Point point;
  const _RatingDialog({required this.point});

  @override
  State<_RatingDialog> createState() => _RatingDialogState();
}

class _RatingDialogState extends State<_RatingDialog> {
  late int _stars;
  final _ctrl = TextEditingController();
  String? _err;
  bool _sending = false;

  @override
  void initState() {
    super.initState();
    _stars = context.read<AppState>().myRatings[widget.point.id] ?? 0;
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_sending) return;
    final app = context.read<AppState>();
    setState(() {
      _sending = true;
      _err = null;
    });
    final comment = _ctrl.text.trim().isEmpty ? null : _ctrl.text.trim();
    final msg = await app.rate(widget.point.id, _stars, comment);
    if (!mounted) return;
    if (msg == null || msg == app.t('savedOffline')) {
      Navigator.pop(context, msg ?? app.t('thanks'));
    } else {
      setState(() {
        _err = msg;
        _sending = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final app = context.read<AppState>();
    return AlertDialog(
      title: Text('${app.t('rate')}: ${widget.point.name}'),
      content: Column(mainAxisSize: MainAxisSize.min, children: [
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          for (int i = 1; i <= 5; i++)
            IconButton(
              iconSize: 36,
              icon: Icon(i <= _stars ? Icons.star : Icons.star_border, color: Colors.amber),
              onPressed: _sending ? null : () => setState(() => _stars = i),
            ),
        ]),
        TextField(
          controller: _ctrl,
          enabled: !_sending,
          maxLength: 500,
          maxLines: 3,
          decoration: InputDecoration(hintText: app.t('comment')),
        ),
        if (_err != null) Text(_err!, style: const TextStyle(color: Colors.red)),
      ]),
      actions: [
        TextButton(onPressed: _sending ? null : () => Navigator.pop(context), child: const Text('×')),
        FilledButton(
          onPressed: _sending ? null : _submit,
          child: _sending
              ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : Text(app.t('send')),
        ),
      ],
    );
  }
}
