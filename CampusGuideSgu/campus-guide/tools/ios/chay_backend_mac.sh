#!/bin/bash
# Chạy backend CampusGuide trên macOS ở cổng 5050 (tránh cổng 5000 của AirPlay Receiver).
# Dùng:  bash chay_backend_mac.sh [thư-mục-campus-guide]   (mặc định: thư mục hiện tại)
#        PORT=5000 bash chay_backend_mac.sh                 (nếu đã tắt AirPlay Receiver)
set -e

ROOT="${1:-.}"
PORT="${PORT:-5050}"
WEB="$ROOT/backend/CampusGuide.Web"

if [ ! -f "$WEB/CampusGuide.Web.csproj" ]; then
  echo "Không thấy $WEB/CampusGuide.Web.csproj – hãy truyền đúng thư mục campus-guide."
  exit 1
fi
command -v dotnet >/dev/null || { echo "Chưa cài .NET SDK 8 trở lên (macOS Arm64): https://dotnet.microsoft.com/download"; exit 1; }

IP="$(ipconfig getifaddr en0 2>/dev/null || true)"
[ -z "$IP" ] && IP="$(ipconfig getifaddr en1 2>/dev/null || true)"
[ -z "$IP" ] && IP="localhost"

echo "──────────────────────────────────────────────"
echo " Trang Admin   : http://localhost:$PORT/admin"
echo " iOS Simulator : http://localhost:$PORT"
echo " iPhone thật   : http://$IP:$PORT   (cùng Wi-Fi)"
echo " Mã QR trỏ về  : http://$IP:$PORT/q/<MaQR>"
echo "──────────────────────────────────────────────"

cd "$WEB"
# --no-launch-profile để launchSettings.json (cổng 5000) không ghi đè cổng mới
ASPNETCORE_ENVIRONMENT=Development \
PublicBaseUrl="http://$IP:$PORT" \
dotnet run --no-launch-profile -- --urls "http://0.0.0.0:$PORT"
