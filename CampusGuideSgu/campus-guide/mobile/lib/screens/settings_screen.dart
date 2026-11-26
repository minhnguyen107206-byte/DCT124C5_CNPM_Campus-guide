import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import 'language_screen.dart';

/// Cài đặt: đổi ngôn ngữ (UC01), bật tự động thuyết minh (UC06), địa chỉ máy chủ (phục vụ chạy thử).
class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  late final TextEditingController _url;

  @override
  void initState() {
    super.initState();
    _url = TextEditingController(text: context.read<AppState>().api.baseUrl);
  }

  @override
  void dispose() {
    _url.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final current = app.languages.where((l) => l.code == app.lang);
    return Scaffold(
      appBar: AppBar(title: Text(app.t('settings'))),
      body: ListView(children: [
        ListTile(
          leading: const Icon(Icons.language),
          title: Text(app.t('language')),
          subtitle: Text(current.isNotEmpty ? '${current.first.flag ?? ''} ${current.first.name}' : app.lang ?? ''),
          onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const LanguageScreen())),
        ),
        SwitchListTile(
          secondary: const Icon(Icons.hearing),
          title: Text(app.t('autoNarration')),
          value: app.autoNarration,
          onChanged: (v) => app.setAutoNarration(v),
        ),
        const Divider(),
        Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(app.t('server'), style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 8),
            TextField(controller: _url, decoration: const InputDecoration(border: OutlineInputBorder(), hintText: 'http://192.168.1.10:5000')),
            const SizedBox(height: 8),
            FilledButton(
              onPressed: () async {
                await app.setBaseUrl(_url.text);
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(app.error ?? 'OK – ${app.points.length} ${app.t('points')}')));
                }
              },
              child: Text(app.t('save')),
            ),
            const SizedBox(height: 16),
            Text('Device ID: ${app.deviceId}', style: Theme.of(context).textTheme.bodySmall),
          ]),
        ),
      ]),
    );
  }
}
