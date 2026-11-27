import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../strings.dart';

/// UC01 – Chọn ngôn ngữ (lần đầu mở app hoặc từ Cài đặt).
class LanguageScreen extends StatefulWidget {
  final bool firstRun;
  const LanguageScreen({super.key, this.firstRun = false});

  @override
  State<LanguageScreen> createState() => _LanguageScreenState();
}

class _LanguageScreenState extends State<LanguageScreen> {
  String? _selected;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    final app = context.read<AppState>();
    app.loadLanguages().then((_) {
      if (!mounted) return;
      setState(() {
        _selected = app.lang ?? app.suggestedLanguage();
        _loading = false;
      });
    });
  }

  @override
  Widget build(BuildContext context) {
    final app = context.watch<AppState>();
    final uiLang = _selected ?? 'vi';
    return Scaffold(
      appBar: widget.firstRun ? null : AppBar(title: Text(tr(uiLang, 'language'))),
      body: SafeArea(
        child: _loading
            ? const Center(child: CircularProgressIndicator())
            : Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (widget.firstRun) ...[
                    const SizedBox(height: 32),
                    Icon(Icons.travel_explore, size: 64, color: Theme.of(context).colorScheme.primary),
                    const SizedBox(height: 12),
                    Text('CampusGuide', textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineMedium),
                    const SizedBox(height: 4),
                    Text(tr(uiLang, 'chooseLang'), textAlign: TextAlign.center),
                    const SizedBox(height: 16),
                  ],
                  Expanded(
                    child: ListView(
                      children: app.languages
                          .map((l) => RadioListTile<String>(
                                value: l.code,
                                groupValue: _selected,
                                onChanged: (v) => setState(() => _selected = v),
                                title: Text('${l.flag ?? ''}  ${l.name}', style: const TextStyle(fontSize: 18)),
                              ))
                          .toList(),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.all(16),
                    child: FilledButton(
                      onPressed: _selected == null
                          ? null
                          : () async {
                              await app.setLanguage(_selected!);
                              if (!widget.firstRun && context.mounted) Navigator.pop(context);
                            },
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Text(tr(uiLang, 'confirm'), style: const TextStyle(fontSize: 16)),
                      ),
                    ),
                  ),
                ],
              ),
      ),
    );
  }
}
