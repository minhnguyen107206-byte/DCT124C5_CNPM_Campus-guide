import 'package:audio_session/audio_session.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'screens/home_shell.dart';
import 'screens/language_screen.dart';
import 'services/presence.dart';
import 'state/app_state.dart';
import 'state/audio_controller.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Thuyết minh là âm thanh chính của app: iOS không tắt tiếng khi gạt nút im lặng.
  final session = await AudioSession.instance;
  await session.configure(AudioSessionConfiguration(
    avAudioSessionCategory: AVAudioSessionCategory.playback,
    avAudioSessionMode: AVAudioSessionMode.spokenAudio,
  ));
  final app = AppState();
  await app.init();
  final audio = AudioController(app);
  PresenceReporter(app, audio).start(); // đếm trên dashboard "Trực tiếp"
  runApp(MultiProvider(
    providers: [
      ChangeNotifierProvider.value(value: app),
      ChangeNotifierProvider.value(value: audio),
    ],
    child: const CampusGuideApp(),
  ));
}

class CampusGuideApp extends StatelessWidget {
  const CampusGuideApp({super.key});

  @override
  Widget build(BuildContext context) {
    final lang = context.select<AppState, String?>((s) => s.lang);
    return MaterialApp(
      title: 'CampusGuide',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF2F5597)),
        useMaterial3: true,
      ),
      home: lang == null ? const LanguageScreen(firstRun: true) : const HomeShell(),
    );
  }
}
