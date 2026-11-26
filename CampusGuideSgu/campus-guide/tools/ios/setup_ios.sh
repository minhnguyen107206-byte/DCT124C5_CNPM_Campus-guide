#!/bin/bash
# Dựng thư mục ios/ cho app Flutter CampusGuide và thêm các quyền cần thiết.
# Dùng:  bash setup_ios.sh <thư-mục-mobile>      ví dụ: bash setup_ios.sh ~/campus-guide/mobile
set -e

MOBILE="${1:-.}"
cd "$MOBILE"

[ -f pubspec.yaml ] || { echo "Không thấy pubspec.yaml trong $(pwd) – truyền đúng thư mục mobile/."; exit 1; }
command -v flutter >/dev/null || { echo "Chưa cài Flutter:  brew install --cask flutter"; exit 1; }
xcode-select -p >/dev/null 2>&1 || { echo "Chưa cài Xcode Command Line Tools:  xcode-select --install"; exit 1; }
command -v pod >/dev/null || echo "⚠  Chưa có CocoaPods – chạy:  brew install cocoapods"

# 1. Tạo ios/ và android/ (không ghi đè lib/ đã có)
if [ ! -d ios ] || [ ! -d android ]; then
  echo "→ flutter create ..."
  flutter create --org vn.edu.sgu --project-name campus_guide --platforms ios,android .
else
  echo "→ Đã có ios/ và android/, bỏ qua flutter create"
fi

# 2. Quyền camera, vị trí, cho phép HTTP (script có sẵn trong repo)
echo "→ dart run tool/patch_platforms.dart"
dart run tool/patch_platforms.dart

# 3. iPhone thật gọi IP LAN của Mac cần quyền Mạng cục bộ (iOS 14+)
PLIST="ios/Runner/Info.plist"
if ! /usr/libexec/PlistBuddy -c "Print :NSLocalNetworkUsageDescription" "$PLIST" >/dev/null 2>&1; then
  plutil -replace NSLocalNetworkUsageDescription \
    -string "Kết nối tới máy chủ CampusGuide trong cùng mạng Wi-Fi" "$PLIST"
  echo "→ Đã thêm NSLocalNetworkUsageDescription"
fi
plutil -lint "$PLIST"

# 4. Nhắc .gitignore
if ! grep -qE '^/?ios/?$' .gitignore 2>/dev/null; then
  echo "⚠  mobile/.gitignore chưa bỏ qua ios/ và android/. Thêm 2 dòng:  /ios/  và  /android/"
fi

# 5. Tải package
flutter pub get

echo ""
echo "✓ Xong. Chạy thử trên Simulator:"
echo "   open -a Simulator"
echo "   flutter run --dart-define=API_BASE_URL=http://localhost:5050"
