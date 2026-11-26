import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../screens/player_screen.dart';
import '../state/audio_controller.dart';

/// Trình phát thu nhỏ ở cuối màn hình, bấm để mở trình phát đầy đủ (UC07).
class MiniPlayer extends StatelessWidget {
  const MiniPlayer({super.key});

  @override
  Widget build(BuildContext context) {
    final audio = context.watch<AudioController>();
    final p = audio.current;
    if (p == null) return const SizedBox.shrink();
    return Material(
      color: Theme.of(context).colorScheme.primaryContainer,
      child: InkWell(
        onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const PlayerScreen())),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          if (audio.usingTts)
            LinearProgressIndicator(value: audio.ttsProgress, minHeight: 2)
          else
            StreamBuilder<Duration>(
              stream: audio.player.positionStream,
              builder: (_, snap) {
                final d = audio.player.duration?.inMilliseconds ?? 0;
                final pos = snap.data?.inMilliseconds ?? 0;
                return LinearProgressIndicator(value: d == 0 ? 0 : (pos / d).clamp(0, 1).toDouble(), minHeight: 2);
              },
            ),
          ListTile(
            dense: true,
            leading: Icon(audio.usingTts ? Icons.record_voice_over : Icons.graphic_eq),
            title: Text(p.name, maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: audio.error != null ? Text(audio.error!, maxLines: 1) : null,
            trailing: Row(mainAxisSize: MainAxisSize.min, children: [
              IconButton(icon: Icon(audio.playing ? Icons.pause : Icons.play_arrow), onPressed: audio.toggle),
              IconButton(icon: const Icon(Icons.close), onPressed: audio.stop),
            ]),
          ),
        ]),
      ),
    );
  }
}
