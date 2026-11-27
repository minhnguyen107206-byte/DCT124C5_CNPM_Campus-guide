import 'dart:io';

import 'package:flutter/material.dart';

import '../models.dart';
import 'type_icon.dart';

/// Ảnh điểm: ảnh online, ảnh trong gói offline, hoặc biểu tượng loại điểm.
class PointImage extends StatelessWidget {
  final Point point;
  final double size;
  final double? height;
  const PointImage({super.key, required this.point, this.size = 64, this.height});

  @override
  Widget build(BuildContext context) {
    final h = height ?? size;
    Widget placeholder() => Container(
          width: size,
          height: h,
          color: TypeIcon.colorFor(point.typeIcon).withValues(alpha: 0.12),
          child: Icon(TypeIcon.iconFor(point.typeIcon), color: TypeIcon.colorFor(point.typeIcon), size: h * 0.45),
        );
    if (point.localImage != null) {
      return Image.file(File(point.localImage!), width: size, height: h, fit: BoxFit.cover, errorBuilder: (_, __, ___) => placeholder());
    }
    if (point.image != null) {
      return Image.network(point.image!, width: size, height: h, fit: BoxFit.cover, errorBuilder: (_, __, ___) => placeholder());
    }
    return placeholder();
  }
}
