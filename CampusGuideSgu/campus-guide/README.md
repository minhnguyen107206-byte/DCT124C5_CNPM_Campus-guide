# CampusGuide – Thuyết minh & hướng dẫn đa ngôn ngữ trong khuôn viên trường

Đồ án môn **Công nghệ phần mềm** – Trường Đại học Sài Gòn · Nhóm 14 – DCT124C5 · GVHD: TS. Nguyễn Quốc Huy

Repo: https://github.com/minhnguyen107206-byte/DCT124C5_CNPM_Campus-guide

CampusGuide giúp tân sinh viên, phụ huynh và khách quốc tế tìm đường và nghe giới thiệu từng tòa nhà, phòng ban trong trường. Khách quét mã QR hoặc đi đến gần một tòa nhà (GPS) để nghe audio bằng ngôn ngữ của mình. Cán bộ nhà trường chỉ nhập nội dung tiếng Việt; hệ thống tự dịch và sinh giọng đọc cho các ngôn ngữ còn lại.

| Thành phần | Công nghệ | Thư mục |
| --- | --- | --- |
| Backend API + Trang Admin | ASP.NET Core 8 (Web API + MVC), EF Core, SQLite / PostgreSQL | `backend/CampusGuide.Web` |
| App khách tham quan | Flutter (Android + iOS) | `mobile/` |
| Dịch tự động | [MyMemory](https://mymemory.translated.net) – miễn phí, không cần tài khoản hay API key | |
| Giọng đọc | Giọng có sẵn của điện thoại (`flutter_tts`); trang Admin nghe thử bằng giọng của trình duyệt | |
| Thông báo đẩy | Firebase Cloud Messaging (tùy chọn) | |

---

## Thành viên và phần phụ trách

| Thành viên | MSSV | Use case | File chính |
| --- | --- | --- | --- |
| Nguyễn Hữu Hoàng Minh | 3124411163 | UC01–UC08 | `mobile/lib/screens/` (language, map, list, search, point_detail, directions, qr_scan, player, tours, tour_detail, tour_run), `mobile/lib/state/` |
| Nguyễn Ngọc Thắng | 3124411284 | UC12–UC17, UC22, UC23 | `backend/.../Models`, `Data`, `Services/{Translation,Tts,TranslationPipeline,Qr,ContentResolver}`, `Controllers/Api`, Admin: `Account`, `Points`, `Translations`, `Languages`, `Glossary` |
| Nguyễn Hải Vương | 3124411353 | UC09–UC11, UC18–UC21, UC24 | `mobile/lib/services/offline_store.dart`, `screens/{history,notices}_screen.dart`, `widgets/rating_dialog.dart`; backend `Services/{Package,Stats,Push}Service.cs`, Admin: `Tours`, `Notices`, `Users`, `Stats` |

---

## 1. Chạy backend + trang Admin

**Cài đặt:** .NET SDK **8 trở lên** (8, 9 hoặc 10 đều chạy được) – https://dotnet.microsoft.com/download (macOS Apple Silicon chọn bản Arm64). Kiểm tra: `dotnet --list-sdks`.

```bash
cd backend/CampusGuide.Web
dotnet run
```

- Trình duyệt mở `http://localhost:5000/admin` (trang đăng nhập giao diện Halloween – bấm nút mặt trăng để bật đêm, bấm hình con mắt để soi mật khẩu bằng đèn pin).
- Lần chạy đầu tự tạo CSDL SQLite `campusguide.db` cùng dữ liệu: **20 điểm thật** quanh 273 An Dương Vương (toạ độ, nội dung do Thắng đo và viết), 1 tour 8 điểm, 5 ngôn ngữ, 12 thuật ngữ của trường (48 bản dịch). Không cần cài SQL Server hay PostgreSQL.
- Đã có `campusguide.db` từ bản cũ (8 điểm mẫu)? **Xoá file đó** rồi chạy lại để nạp dữ liệu mới.
- Tài khoản mẫu:

| Vai trò | Email | Mật khẩu |
| --- | --- | --- |
| Quản trị viên | `admin@campusguide.local` | `Admin@123` |
| Biên tập viên | `btv@campusguide.local` | `Btv@1234` |

Windows: mở `backend/CampusGuide.sln` bằng Visual Studio 2022 rồi bấm Run, hoặc dùng lệnh như trên.
VS Code: mở thư mục gốc `campus-guide`, tab **Run and Debug** có sẵn 2 cấu hình (`.vscode/launch.json`).

> **macOS:** AirPlay Receiver chiếm sẵn cổng 5000 (gọi `localhost:5000` ra lỗi 403). Tắt ở **System Settings → General → AirDrop & Handoff → AirPlay Receiver**, hoặc chạy cổng khác:
> `dotnet run -- --urls http://0.0.0.0:5050` (VS Code: chọn cấu hình *Backend trên Mac*), và trong app dùng `http://localhost:5050`.

### Dịch tự động (MyMemory) và giọng đọc

- **Dịch:** bấm **Dịch tất cả điểm** là backend gọi MyMemory dịch thật, không cần tài khoản hay API key (máy chạy backend cần có Internet).
- **Hạn mức miễn phí:** khoảng **5.000 ký tự/ngày** nếu không khai báo email – không đủ dịch 20 điểm sang 4 ngôn ngữ. Nên khai báo email để tăng lên khoảng **50.000 ký tự/ngày**:
  sao chép `appsettings.Development.example.json` thành `appsettings.Development.json` rồi điền email thật vào `Translation:Email`.
  Hết hạn mức thì trang Admin báo lỗi rõ ràng; thử lại ngày hôm sau.
- **Không có Internet** (VD demo mất mạng): đặt `"Translation": { "Provider": "Demo" }` → bản dịch giả lập dạng `[en] văn bản gốc`.
- **Giọng đọc:** máy chủ không sinh file audio. App đọc văn bản thuyết minh bằng **giọng có sẵn của điện thoại** (iPhone: Settings → Accessibility → Spoken Content → Voices để tải thêm giọng tiếng Việt/Nhật/Hàn/Trung; Android: cài giọng trong Text-to-speech của máy). Trang Admin có nút **Nghe thử** dùng giọng của trình duyệt.

`appsettings.Development.json` không được commit lên GitHub (đã có trong `.gitignore`).

### Dùng PostgreSQL thay SQLite (tùy chọn)

Trong `appsettings.Development.json`:

```json
"Database": { "Provider": "Postgres", "ConnectionString": "Host=...;Database=...;Username=...;Password=..." }
```

### Chạy với điện thoại thật

1. Máy tính và điện thoại cùng mạng Wi-Fi; xem IP LAN của máy tính (macOS: `ipconfig getifaddr en0`, Windows: `ipconfig`).
2. Mã QR tự lấy IP Wi-Fi/LAN của máy chạy backend (xem địa chỉ ở trang **Mã QR** của điểm). Chỉ đặt `PublicBaseUrl` khi muốn cố định địa chỉ (VD tên miền thật).
3. Trong app: **Cài đặt → Địa chỉ máy chủ** nhập `http://<IP>:5000` – hoặc cứ **quét một mã QR**: app không kết nối được máy chủ cũ sẽ tự chuyển sang máy chủ ghi trong mã.
4. macOS có thể hỏi cho phép kết nối đến – chọn Allow. Windows: cho phép cổng 5000 trong Firewall.

---

## 2. Chạy app Flutter

**Cài đặt:** [Flutter](https://docs.flutter.dev/get-started/install) stable **3.29 trở lên** (gói quét QR `mobile_scanner` 7 yêu cầu), Android Studio (Android Emulator). Build iOS cần máy Mac có Xcode + CocoaPods (`brew install cocoapods`).

Repo chỉ chứa mã nguồn Dart; thư mục `android/` và `ios/` được tạo trên máy mỗi người (đã có trong `mobile/.gitignore`, đừng commit):

```bash
cd mobile
flutter create --org vn.edu.sgu --project-name campus_guide .
dart run tool/patch_platforms.dart      # thêm quyền camera, vị trí, mạng cục bộ (iOS), cho phép HTTP
flutter pub get
flutter run
```

- Android Emulator tự gọi backend qua `http://10.0.2.2:5000`; iOS Simulator dùng `http://localhost:5000`.
- Có thể chỉ định sẵn: `flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5000`.
- Giả lập vị trí để test GPS: Android Emulator → Extended controls → Location; iOS Simulator → Features → Location → Custom Location (tâm trường `10.7598, 106.6823`), hoặc dùng script `tools/ios/mo_phong_gps.sh` (đứng tại điểm, đi lệch đường, đi theo tour).

Nếu `patch_platforms.dart` báo lỗi, thêm thủ công:
- `android/app/src/main/AndroidManifest.xml`: quyền `INTERNET`, `CAMERA`, `ACCESS_FINE_LOCATION` và `android:usesCleartextTraffic="true"` trong thẻ `<application>`.
- `ios/Runner/Info.plist`: `NSCameraUsageDescription`, `NSLocationWhenInUseUsageDescription`, `NSLocalNetworkUsageDescription`, `NSAppTransportSecurity → NSAllowsArbitraryLoads = true`.

Hướng dẫn riêng cho iOS (Simulator, iPhone thật, ký app bằng Apple ID miễn phí): `tools/ios/HUONG_DAN_CHAY_iOS.md`.

---

## 3. Kịch bản demo

1. **Admin** đăng nhập `admin@campusguide.local` → **Điểm thuyết minh** → **Dịch tất cả điểm** (UC16).
2. **Duyệt bản dịch** → nghe thử, sửa nếu cần, bấm **Duyệt** (UC17).
3. Mở app → chọn **English** → bản đồ hiện 20 điểm (UC01, UC02) → bấm một điểm → **Listen** (UC07).
4. Admin → **Mã QR** của một điểm → mở ảnh QR trên màn hình máy tính → app **Scan QR** (UC05, UC14).
5. Bật nút **tai nghe** trên bản đồ (tự động thuyết minh) + giả lập vị trí vào gần một điểm (UC06).
6. Admin → **Tour & gói offline** → **Gói offline** → xuất bản gói `en` (UC19) → app **Tours → Download**, tắt mạng, vẫn nghe được (UC09).
7. Admin → **Thông báo khu vực** → tạo thông báo mức **Khẩn** (UC20) → app hiện thông báo (UC10).
8. Admin → **Thống kê** → xem biểu đồ, **xuất Excel** (UC24).
9. Admin → **Trực tiếp** → 4 điện thoại quét QR cùng lúc: số tăng, bấm Nghe thì "Đang nghe" tăng, đóng tab thì giảm (xem `docs/minh/GIAI_THICH_SO_LIEU_TRUC_TIEP.md`).

Kịch bản chi tiết kèm lời nói và câu hỏi vấn đáp: `docs/test/KichBan_Demo_VanDap.md`. Test case 24 UC: `docs/test/TestCase_CampusGuide_24UC.xlsx`.

---

## 4. Bảng đối chiếu use case – mã nguồn

| UC | Chức năng | Backend | App |
| --- | --- | --- | --- |
| UC01 | Chọn ngôn ngữ | `GET /api/languages` | `language_screen.dart` |
| UC02 | Bản đồ, danh sách điểm | `GET /api/points` | `map_screen.dart`, `list_screen.dart`, `point_detail_screen.dart` |
| UC03 | Tìm kiếm | `GET /api/points/search` | `search_screen.dart` |
| UC04 | Chỉ đường | (OSRM – OpenStreetMap) | `directions_screen.dart` |
| UC05 | Quét QR | `GET /api/points/qr/{code}` | `qr_scan_screen.dart` |
| UC06 | Thuyết minh theo GPS | – | `app_state.dart` (`_checkGeofence`) |
| UC07 | Điều khiển audio | `POST /api/listens` | `audio_controller.dart`, `player_screen.dart` |
| UC08 | Tour | `GET /api/tours`, `/api/tours/{id}` | `tours_screen.dart`, `tour_detail_screen.dart`, `tour_run_screen.dart` |
| UC09 | Tải offline | `GET /api/tours/{id}/package` | `offline_store.dart` |
| UC10 | Thông báo khu vực | `POST /api/devices`, `GET /api/notices/active` | `app_state.dart`, `notices_screen.dart` |
| UC11 | Lịch sử, đánh giá | `POST /api/ratings` | `history_screen.dart`, `rating_dialog.dart` |
| UC12 | Đăng nhập, đổi mật khẩu | `Admin/AccountController`, `Views/Account/Login.cshtml` + `wwwroot/admin-login/` (giao diện Halloween) | |
| UC13 | Quản lý điểm | `Admin/PointsController` (Index, Save, Hide = xoá mềm, tự gỡ khỏi tour, Restore = khôi phục, Publish), `TourCalc` | |
| UC14 | Mã QR | `PointsController` (Qr, QrPng, QrSheet), `QrService` (tự lấy IP LAN), `/q/{code}` (`QrLandingController`: xem + nghe trên trình duyệt) | `qr_scan_screen.dart` |
| UC15 | Nội dung gốc | `PointsController.ContentEdit` | |
| UC16 | Dịch tự động | `TranslationPipeline`, `MyMemoryTranslationService`, `DeviceTtsService` | `audio_controller.dart` (đọc bằng `flutter_tts`) |
| UC17 | Duyệt bản dịch | `Admin/TranslationsController` | |
| UC18 | Quản lý tour | `Admin/ToursController` | |
| UC19 | Gói offline | `ToursController.Packages`, `PackageService` | |
| UC20 | Thông báo | `Admin/NoticesController`, `FirebasePushService` | |
| UC21 | Tài khoản, phân quyền | `Admin/UsersController` | |
| UC22 | Ngôn ngữ, giọng đọc | `Admin/LanguagesController` | |
| UC23 | Thuật ngữ | `Admin/GlossaryController`, `GlossaryService` | |
| UC24 | Thống kê | `Admin/StatsController`, `StatsService` | |
| – | **Trực tiếp**: số thiết bị đang truy cập / đang nghe | `Admin/LiveController`, `Services/PresenceService.cs` (số liệu + hệ số `HeSoHienThi`), `Api/PresenceController` | `services/presence.dart`; trang web `/q/{mã}` |

---

## 5. Giới hạn của bản đồ án

- Tự động thuyết minh chạy khi app đang mở (chưa theo dõi vị trí lúc app chạy nền).
- Dịch dùng MyMemory miễn phí: có hạn mức theo ngày, chất lượng kém hơn dịch thương mại – bản dịch vẫn phải được duyệt (UC17).
- Giọng đọc phụ thuộc giọng cài sẵn trên điện thoại; khi đọc bằng giọng của máy không tua ±10 giây được (vẫn dừng, đọc tiếp, nghe lại, đổi tốc độ).
- Điều khiển audio trên màn hình khóa cần thêm gói `just_audio_background`.
- Gói offline chứa nội dung, ảnh, audio; nền bản đồ vẫn cần mạng (khi mất mạng vẫn thấy các ghim điểm).
- Quên mật khẩu: chưa có máy chủ email, Quản trị viên đặt lại mật khẩu ở trang Tài khoản.
- Thông báo đẩy khi app đóng cần cấu hình Firebase (`Firebase:CredentialsPath` ở backend, thêm `firebase_messaging` ở app); khi chưa cấu hình, app vẫn nhận thông báo lúc đang mở (kiểm tra mỗi 2 phút).

---

## 6. Quy trình Git

```bash
git checkout main && git pull
git checkout -b minh/uc05-quet-qr        # nhánh theo người + use case
git add . && git commit -m "UC05: quét QR và phát audio"
git push -u origin minh/uc05-quet-qr     # rồi tạo Pull Request vào main
```

Không push thẳng vào `main`. Review chéo: Minh → Thắng → Vương → Minh.

Mẫu Pull Request: `.github/pull_request_template.md`. Checklist duyệt PR: `docs/minh/CHECKLIST_DUYET_PR.md`.

---

## 7. Lịch sử bản gộp

- 23/10/2026: gộp nhánh `vuong/admin-offline`, 20 điểm thật + thuật ngữ của Thắng, trang đăng nhập Halloween, sửa lỗi app iOS – chi tiết: `docs/THAY_DOI_BAN_GOP.md`.
- 23/10/2026 (2): bỏ Google Cloud – dịch bằng MyMemory, đọc bằng giọng của điện thoại; xoá điểm rồi khôi phục / thêm lại được.
- 23/10/2026 (3): xoá điểm đang thuộc tour (tự gỡ khỏi tour); nút Menu cho trang Admin trên màn hình hẹp.
- 23/10/2026 (4): sửa mã QR – tự dùng IP LAN thay cho localhost, trang quét bằng camera thường nghe được, app tự kết nối máy chủ trong mã QR.
- 23/10/2026 (5): dashboard **Trực tiếp** – đếm thiết bị đang truy cập / đang nghe theo thời gian thực; giải thích số liệu ở `docs/minh/GIAI_THICH_SO_LIEU_TRUC_TIEP.md`.
