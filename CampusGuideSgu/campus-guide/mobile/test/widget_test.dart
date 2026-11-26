import 'package:campus_guide/screens/search_screen.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Bỏ dấu tiếng Việt khi tìm kiếm offline', () {
    expect(normalizeVi('Phòng Đào tạo'), 'phong dao tao');
    expect(normalizeVi('Nộp học phí'), 'nop hoc phi');
  });
}
