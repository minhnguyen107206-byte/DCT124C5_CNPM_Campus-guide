import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../state/audio_controller.dart';
import '../widgets/point_image.dart';
import '../widgets/rating_dialog.dart';

String _fmt(Duration d) => '${d.inMinutes.toString().padLeft(2, '0')}:${(d.inSeconds % 60).toString().padLeft(2, '0')}';

/// UC07 – Trình phát đầy đủ: phát, tạm dừng, tua ±10 giây, kéo thanh, nghe lại, đổi tốc độ.
class PlayerScreen extends StatelessWidget {
  const PlayerScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final app = context.read<AppState>();
    final audio = context.watch<AudioController>();
    final p = audio.current;
    if (p == null) {
      return Scaffold(appBar: AppBar(), body: const SizedBox.shrink());
    }
    return Scaffold(
      appBar: AppBar(title: Text(p.name)),
      body: ListView(padding: const EdgeInsets.all(20), children: [
        ClipRRect(borderRadius: BorderRadius.circular(16), child: PointImage(point: p, size: double.infinity, height: 220)),
        const SizedBox(height: 16),
        Text(p.title, style: Theme.of(context).textTheme.titleLarge, textAlign: TextAlign.center),
        if (audio.error != null)
          Padding(padding: const EdgeInsets.only(top: 8), child: Text(audio.error!, style: const TextStyle(color: Colors.red), textAlign: TextAlign.center)),
        if (audio.usingTts)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 16),
            child: Column(children: [
              LinearProgressIndicator(value: audio.ttsProgress),
              const SizedBox(height: 8),
              Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                const Icon(Icons.record_voice_over, size: 18),
                const SizedBox(width: 6),
                Text(app.t('deviceVoice'), style: Theme.of(context).textTheme.bodySmall),
              ]),
            ]),
          )
        else
        StreamBuilder<Duration>(
          stream: audio.player.positionStream,
          builder: (_, snap) {
            final dur = audio.player.duration ?? Duration(seconds: p.audioSeconds);
            final pos = snap.data ?? Duration.zero;
            final max = dur.inMilliseconds.toDouble().clamp(1, double.infinity).toDouble();
            return Column(children: [
              Slider(
                value: pos.inMilliseconds.toDouble().clamp(0, max).toDouble(),
                max: max,
                onChanged: (v) => audio.seekTo(Duration(milliseconds: v.round())),
              ),
              Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [Text(_fmt(pos)), Text(_fmt(dur))]),
            ]);
          },
        ),
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          IconButton(iconSize: 36, icon: const Icon(Icons.replay_10), onPressed: audio.usingTts ? null : () => audio.seekBy(-10)),
          const SizedBox(width: 12),
          IconButton.filled(
            iconSize: 48,
            icon: Icon(audio.playing ? Icons.pause : Icons.play_arrow),
            onPressed: audio.toggle,
          ),
          const SizedBox(width: 12),
          IconButton(iconSize: 36, icon: const Icon(Icons.forward_10), onPressed: audio.usingTts ? null : () => audio.seekBy(10)),
        ]),
        const SizedBox(height: 8),
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          TextButton.icon(onPressed: audio.replay, icon: const Icon(Icons.replay), label: Text(app.t('replay'))),
          const SizedBox(width: 8),
          // A1: tốc độ phát
          DropdownButton<double>(
            value: audio.speed,
            items: const [0.75, 1.0, 1.25, 1.5].map((s) => DropdownMenuItem(value: s, child: Text('${s}x'))).toList(),
            onChanged: (s) => audio.setSpeed(s ?? 1.0),
          ),
          const SizedBox(width: 8),
          TextButton.icon(onPressed: () => showRatingDialog(context, p), icon: const Icon(Icons.star_outline), label: Text(app.t('rate'))),
        ]),
        const Divider(height: 32),
        Text(p.text, style: const TextStyle(fontSize: 16, height: 1.5)),
      ]),
    );
  }
}
