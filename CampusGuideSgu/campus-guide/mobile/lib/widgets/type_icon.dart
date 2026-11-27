import 'package:flutter/material.dart';

/// Biểu tượng ghim theo loại điểm (LoaiDiem.BieuTuong).
class TypeIcon extends StatelessWidget {
  final String type;
  final bool selected;
  const TypeIcon({super.key, required this.type, this.selected = false});

  static IconData iconFor(String t) => switch (t) {
        'gate' => Icons.door_front_door_outlined,
        'building' => Icons.apartment,
        'library' => Icons.local_library_outlined,
        'office' => Icons.badge_outlined,
        'hall' => Icons.groups_outlined,
        'food' => Icons.restaurant_outlined,
        'service' => Icons.local_cafe_outlined,
        'parking' => Icons.local_parking,
        'sport' => Icons.sports_soccer,
        'landmark' => Icons.flag_outlined,
        _ => Icons.place,
      };

  static Color colorFor(String t) => switch (t) {
        'gate' => const Color(0xFFC62828),
        'building' => const Color(0xFF2F5597),
        'library' => const Color(0xFF00838F),
        'office' => const Color(0xFF7B3FA0),
        'hall' => const Color(0xFFAD1457),
        'food' => const Color(0xFFEF6C00),
        'service' => const Color(0xFF2E7D32),
        'parking' => const Color(0xFF455A64),
        'sport' => const Color(0xFF2E7D32),
        'landmark' => const Color(0xFF6D4C41),
        _ => Colors.red,
      };

  @override
  Widget build(BuildContext context) {
    final c = colorFor(type);
    return AnimatedScale(
      scale: selected ? 1.2 : 1,
      duration: const Duration(milliseconds: 150),
      child: Container(
        decoration: BoxDecoration(
          color: selected ? c : Colors.white,
          shape: BoxShape.circle,
          border: Border.all(color: c, width: 2),
          boxShadow: const [BoxShadow(blurRadius: 4, color: Colors.black26)],
        ),
        child: Icon(iconFor(type), color: selected ? Colors.white : c, size: 22),
      ),
    );
  }
}
