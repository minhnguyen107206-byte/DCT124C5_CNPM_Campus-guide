#!/bin/bash
# Giả lập vị trí GPS cho iOS Simulator đang chạy (Xcode 14 trở lên) – test UC02, UC04, UC06, UC08.
# Toạ độ = 20 điểm thật trong backend/CampusGuide.Web/Data/SeedData.cs (số thứ tự 1–20 = mã QR CG0001–CG0020).
#
#   bash mo_phong_gps.sh ds              liệt kê 20 điểm
#   bash mo_phong_gps.sh diem 8          đứng ngay tại điểm 8 (KLF và khu tự học)  → UC06 tự phát
#   bash mo_phong_gps.sh xa              đứng bên kia đường An Dương Vương, ngoài mọi bán kính → UC04 bắt đầu chỉ đường
#   bash mo_phong_gps.sh cum             đứng chỗ 6 vùng giao nhau gần căn tin      → UC06 A1 chọn điểm gần nhất (Căn tin)
#   bash mo_phong_gps.sh di 1 8          đi bộ ảo từ điểm 1 đến điểm 8 (1,4 m/s)
#   bash mo_phong_gps.sh lech            đi lệch khỏi đường > 30 m                  → UC04 A1 tính lại đường
#   bash mo_phong_gps.sh tour            đi bộ ảo qua 8 điểm của "Tour tân sinh viên" → UC08
#   bash mo_phong_gps.sh dung            xoá giả lập, trả về vị trí mặc định
set -e

TEN=( "" "Cổng chính 273 An Dương Vương" "Cổng số 6 Nguyễn Trãi" "Khu A" "Phòng A001" "Hội trường A"
      "Khu B (dãy trái)" "Khu B (dãy phải)" "KLF và khu tự học" "Cột cờ" "Căn tin"
      "Khu C (dãy trái)" "Khu C (dãy phải)" "Khu Hiệu bộ" "Khu D" "Khu E"
      "Bãi giữ xe" "Nhà bóng bàn" "Sân bóng rổ" "Sân cầu lông" "Sân bóng đá" )
LAT=( "" 10.759411 10.760174 10.760174 10.760037 10.760009 10.759838 10.759838 10.759728 10.759705 10.759728
         10.759617 10.759617 10.759426 10.759426 10.760133 10.759907 10.759765 10.759624 10.759624 10.759472 )
LNG=( "" 106.682068 106.682716 106.682091 106.682609 106.681622 106.681749 106.682413 106.681544 106.682073 106.682643
         106.681749 106.682413 106.681703 106.682456 106.682853 106.682853 106.682896 106.682814 106.682896 106.682853 )
BK=(  "" 35 25 40 20 30 35 35 20 20 20 35 35 35 35 30 35 15 15 15 30 )   # bán kính kích hoạt (m)
TOUR=( 1 9 13 8 5 3 10 16 )          # thứ tự điểm trong "Tour tân sinh viên"
XA="10.758600,106.682100"            # bên kia đường An Dương Vương, cách cổng chính ~90 m
SPEED=1.4                            # tốc độ đi bộ (m/s)

DEV="booted"
xcrun simctl list devices booted | grep -q Booted || { echo "Chưa mở Simulator nào. Chạy: open -a Simulator"; exit 1; }

p() { echo "${LAT[$1]},${LNG[$1]}"; }

case "$1" in
  ds)
    for i in $(seq 1 20); do printf "%2d. %-32s %s  (bán kính %s m)\n" "$i" "${TEN[$i]}" "$(p $i)" "${BK[$i]}"; done ;;
  diem)
    i="${2:?Thiếu số điểm 1-20}"
    xcrun simctl location $DEV set "$(p $i)"
    echo "📍 Đang đứng tại: ${TEN[$i]} ($(p $i))" ;;
  xa)
    xcrun simctl location $DEV set "$XA"
    echo "📍 Đứng bên kia đường An Dương Vương ($XA), ngoài mọi bán kính" ;;
  cum)
    xcrun simctl location $DEV set "10.759660,106.682660"
    echo "📍 Giữa 6 vùng giao nhau (Căn tin, Khu B/C/D dãy phải, Bãi giữ xe, Sân bóng đá) – app phải chọn Căn tin (gần nhất, ~8 m)" ;;
  di)
    a="${2:?Thiếu điểm đầu}"; b="${3:?Thiếu điểm cuối}"
    xcrun simctl location $DEV start --speed=$SPEED "$(p $a)" "$(p $b)"
    echo "🚶 Đi từ ${TEN[$a]} → ${TEN[$b]} (${SPEED} m/s). Dừng: bash $0 dung" ;;
  lech)
    # từ bên kia đường đi ngang sang đông ~100 m rồi mới vòng lại cổng chính
    xcrun simctl location $DEV start --speed=3 "$XA" "10.758600,106.683000" "10.759000,106.683000" "$(p 1)"
    echo "🚶 Đi lệch sang phía đông (> 30 m so với đường OSRM). Mở Chỉ đường tới KLF (điểm 8) TRƯỚC khi chạy lệnh này." ;;
  tour)
    pts=("$XA")
    for i in "${TOUR[@]}"; do pts+=("$(p $i)"); done
    xcrun simctl location $DEV start --speed=$SPEED "${pts[@]}"
    echo "🚶 Đi theo tour: Cổng chính → Cột cờ → Khu Hiệu bộ → KLF → Hội trường A → Khu A → Căn tin → Bãi giữ xe"
    echo "   Khoảng 400 m ≈ 5 phút. Muốn nhanh: sửa SPEED=4 trong script." ;;
  dung)
    xcrun simctl location $DEV clear
    echo "Đã xoá giả lập vị trí." ;;
  *)
    sed -n '2,12p' "$0" ;;
esac
