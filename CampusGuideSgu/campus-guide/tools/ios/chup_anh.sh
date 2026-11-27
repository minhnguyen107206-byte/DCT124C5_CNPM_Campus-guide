#!/bin/bash
# Chụp màn hình iOS Simulator cho báo cáo / slide.
#   bash chup_anh.sh dep          thanh trạng thái gọn: 9:41, pin đầy, Wi-Fi đủ vạch (chạy 1 lần sau khi mở Simulator)
#   bash chup_anh.sh 02_UC02_ban_do   chụp và lưu vào anh/02_UC02_ban_do.png
#   bash chup_anh.sh ds           xem ảnh nào đã chụp, ảnh nào còn thiếu
#   bash chup_anh.sh bo           trả thanh trạng thái về bình thường
set -e
DIR="$(cd "$(dirname "$0")" && pwd)/anh"
mkdir -p "$DIR"

CAN=( 01_UC01_chon_ngon_ngu 02_UC02_ban_do 03_UC02_the_diem 04_UC02_danh_sach 05_UC02_chi_tiet_diem
      06_UC03_tim_kiem 07_UC03_khong_ket_qua 08_UC04_chi_duong 09_UC04_da_den 10_UC05_quet_qr
      11_UC06_tu_dong_thuyet_minh 12_UC07_trinh_phat 13_UC07_mini_player 14_UC08_danh_sach_tour
      15_UC08_chi_tiet_tour 16_UC08_dang_di_tour 17_UC08_hoan_thanh_tour 18_cai_dat
      19_UC01_giao_dien_tieng_Anh 20_UC05_ket_qua_quet )

case "$1" in
  dep)
    xcrun simctl status_bar booted override --time "9:41" --batteryState charged --batteryLevel 100 \
      --wifiBars 3 --cellularMode active --cellularBars 4 --dataNetwork wifi
    echo "Đã chỉnh thanh trạng thái." ;;
  bo)
    xcrun simctl status_bar booted clear; echo "Đã trả về bình thường." ;;
  ds)
    n=0
    for f in "${CAN[@]}"; do
      if [ -f "$DIR/$f.png" ]; then echo "  ✓ $f"; n=$((n+1)); else echo "  ☐ $f"; fi
    done
    echo "Đã chụp $n/${#CAN[@]} (ảnh 10 và 20 chụp trên iPhone thật rồi AirDrop vào thư mục anh/)" ;;
  "")
    sed -n '2,6p' "$0" ;;
  *)
    xcrun simctl io booted screenshot --type=png "$DIR/$1.png"
    echo "📸 $DIR/$1.png" ;;
esac
