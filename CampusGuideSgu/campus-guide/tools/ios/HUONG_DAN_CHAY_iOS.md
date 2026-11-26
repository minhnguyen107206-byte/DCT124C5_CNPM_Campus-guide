# Chạy CampusGuide trên iOS (MacBook Air M4)

Các script nằm cùng thư mục này (`tools/ios/`). Mọi lệnh dưới đây chạy từ thư mục gốc `campus-guide`.

Thứ tự: cài công cụ → chạy backend trên Mac → dựng `ios/` → chạy Simulator → chạy iPhone thật.

---

## Bước 0. Cài công cụ (làm 1 lần)

```bash
xcode-select --install                 # nếu chưa có Command Line Tools
brew install cocoapods                 # cần cho các plugin iOS (camera, GPS, audio)
brew install --cask flutter            # cần bản 3.29 trở lên (flutter --version)
flutter doctor                         # phải thấy ✓ Xcode, ✓ Flutter
```

- **.NET SDK 8 trở lên** (8, 9 hoặc 10): tải bản **macOS Arm64** ở https://dotnet.microsoft.com/download. Backend nhắm `net8.0` và có `RollForward=Major`, nên máy chỉ có .NET 10 vẫn chạy được. Kiểm tra: `dotnet --list-sdks`.

- Mở Xcode 1 lần, đồng ý license, vào **Settings → Components** tải **iOS Simulator runtime**.
- `flutter doctor` báo thiếu gì thì sửa theo gợi ý của nó (thường là `sudo xcodebuild -runFirstLaunch`).

---

## Bước 1. Chạy backend trên Mac

> **Cẩn thận cổng 5000:** macOS bật sẵn **AirPlay Receiver** chiếm cổng 5000. Gọi `http://localhost:5000` sẽ ra lỗi 403 hoặc backend báo "address already in use".
> Có 2 cách: (a) tắt ở **System Settings → General → AirDrop & Handoff → AirPlay Receiver**, hoặc (b) chạy backend ở cổng khác. Script dưới đây dùng cách (b), cổng **5050**.

```bash
bash tools/ios/chay_backend_mac.sh
```

Script tự lấy IP Wi-Fi của Mac, đặt `PublicBaseUrl` (để mã QR trỏ đúng), chạy ở `http://0.0.0.0:5050`.
Mở `http://localhost:5050/admin` (trang đăng nhập Halloween), đăng nhập `admin@campusguide.local` / `Admin@123` → **Dịch tất cả điểm** (MyMemory dịch thật; app đọc bằng giọng của iPhone – vào **Settings → Accessibility → Spoken Content → Voices** tải thêm giọng Vietnamese nếu đọc sai giọng).

---

## Bước 2. Dựng thư mục `ios/` + quyền

```bash
bash tools/ios/setup_ios.sh mobile
```

Script làm: `flutter create` (chỉ tạo `ios/`, `android/` nếu chưa có) → `patch_platforms.dart` (quyền camera, vị trí, **mạng cục bộ** – iPhone thật cần quyền này mới gọi được IP của Mac, cho phép HTTP) → `flutter pub get`.

`ios/` và `android/` đã nằm trong `mobile/.gitignore`, không bị commit nhầm.

---

## Bước 3. Chạy trên iOS Simulator

```bash
open -a Simulator                      # chọn iPhone 16/17 bất kỳ
cd mobile
flutter run --dart-define=API_BASE_URL=http://localhost:5050
```

- Lần đầu app hỏi quyền vị trí → **Allow While Using App**.
- Giả lập vị trí: `bash tools/ios/mo_phong_gps.sh ds` để xem 20 điểm, `… diem 8`, `… tour` (nhanh hơn menu **Features → Location → Custom Location**). iPhone chạy từ Xcode: **Debug → Simulate Location** → chọn file trong `tools/ios/gpx/`.
- Nếu đã từng đổi địa chỉ máy chủ trong app thì app dùng địa chỉ đã lưu: vào **Cài đặt → Địa chỉ máy chủ** sửa lại `http://localhost:5050`.

### Quét QR

`pubspec.yaml` đã dùng `mobile_scanner: ^7.1.0` (iOS dùng Apple Vision, không còn MLKit nên build được trên Simulator Mac chip M). Simulator vẫn không có camera → test quét QR trên iPhone thật với trang `tools/ios/ma_QR_mau.html` (20 mã CG0001–CG0020).

---

## Bước 4. Chạy trên iPhone thật (bắt buộc cho UC05 quét QR)

1. Cắm iPhone vào Mac, bấm **Trust**.
2. iPhone: **Settings → Privacy & Security → Developer Mode → On** (máy khởi động lại).
3. `open ios/Runner.xcworkspace` → chọn target **Runner** → **Signing & Capabilities** → Team: chọn Apple ID của Minh (Personal Team, miễn phí).
   Nếu báo trùng Bundle Identifier → đổi `vn.edu.sgu.campusGuide` thành `vn.edu.sgu.campusGuide.minh`.
4. Mac và iPhone cùng Wi-Fi. Chạy:
   ```bash
   flutter run -d <tên iPhone> --dart-define=API_BASE_URL=http://<IP-Mac>:5050
   ```
   (IP in ra khi chạy `chay_backend_mac.sh`, hoặc `ipconfig getifaddr en0`.)
5. Lần đầu mở app: iPhone hỏi **"tìm thiết bị trong mạng cục bộ"** → **Allow**. Nếu lỡ từ chối: Settings → Privacy & Security → Local Network → bật CampusGuide.
6. Nếu iPhone báo "Untrusted Developer": Settings → General → VPN & Device Management → Trust.
7. macOS hỏi cho phép `dotnet` nhận kết nối → **Allow**.

> Tài khoản Apple miễn phí: app hết hạn sau 7 ngày, chỉ cần `flutter run` lại. Trước buổi demo nhớ chạy lại 1 lần.

### Kiểm tra âm thanh khi gạt nút im lặng

Trên iPhone thật, gạt **nút im lặng** rồi bấm Nghe: vẫn phải nghe được (app đã cấu hình `audio_session` loại *playback* trong `main.dart`).

---

## Lỗi hay gặp

| Hiện tượng | Nguyên nhân / cách sửa |
| --- | --- |
| App hiện "Không có kết nối" dù backend đang chạy | Sai địa chỉ/cổng. Simulator: `http://localhost:5050`. iPhone: `http://<IP-Mac>:5050`. Kiểm tra lại trong **Cài đặt** của app |
| `localhost:5000` trả 403 | AirPlay Receiver, xem Bước 1 |
| `pod install` lỗi | `cd ios && pod repo update && pod install`; vẫn lỗi → `flutter clean`, xoá `ios/Pods` `ios/Podfile.lock` rồi chạy lại |
| `localhost:5000/admin` vào được nhưng trang đăng nhập trắng | Mở DevTools xem tab Network: thư mục `wwwroot/admin-login/` phải tải được (ảnh, `style.css`, `script.js`) |
| Màn hình quét QR đen trên Simulator | Bình thường – Simulator không có camera. Test UC05 trên iPhone |
| Bản đồ trắng | Simulator/iPhone chưa có Internet (tile lấy từ openstreetmap.org) |
| Bật tai nghe nhưng không tự phát | Chưa vào bán kính, hoặc điểm đó đã phát trong 30 phút qua → khởi động lại app (phím `R` trong `flutter run`) |
