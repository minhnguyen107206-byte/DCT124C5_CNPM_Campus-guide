# CampusGuide - Thuyết minh & Hướng dẫn trong khuôn viên trường
## Đồ án môn *Công nghệ phần mềm* - Trường Đại học Sài Gòn 
### Giảng viên hướng dẫn: TS. Nguyễn Quốc Huy

CampusGuide là ứng dụng thuyết minh đa ngôn ngữ giúp tân sinh viên, phụ huynh và khách quốc tế tìm đường và nghe giới thiệu từng toà nhà, phòng ban trong trường. Khách quét mã QR hoặc đi đến gần một toà nhà (GPS) để nghe audio bằng ngôn ngữ đã chọn. Cán bộ nhà trường chỉ cần nhập nội dung tiếng Việt, hệ thống tự động dịch và sinh giọng đọc cho các ngôn ngữ còn lại.

## Thành viên
| Thành viên | MSSV | Phụ trách |
| --- | --- | --- |
| Nguyễn Hữu Hoàng Minh | 3124411163 | App mobile phía khách tham quan (UC01–UC08), build iOS |
| Nguyễn Ngọc Thắng | 3124411284 | Backend, database, luồng dịch + sinh audio (UC12–UC17, UC22, UC23) |
| Nguyễn Hải Vương | 3124411353 | Trang Admin, gói offline, thông báo, thống kê (UC09–UC11, UC18–UC21, UC24) |

---

## Chức năng chính
### Khách tham quan (app mobile, không cần tài khoản)
- Chọn ngôn ngữ, xem bản đồ và danh sách điểm, tìm kiếm điểm
- Xem đường đi đến điểm
- Quét QR để nghe thuyết minh
- Tự động phát thuyết minh khi đến gần toà nhà (GPS geofence)
- Điều khiển audio: phát, tạm dừng, tua, nghe lại
- Đi theo tua, tải tour để nghe offline
- Nhận thông báo theo khu vực
- Xem lịch sử đã nghe, đánh giá điểm

### Biên tập viên (trang Admin)
- Đăng nhập, đăng xuất, đổi mật khẩu
- Quản lý điểm và sinh mã QR
- Nhập nội dung gốc tiếng Việt; tự động dịch và sinh audio
- Duyệt, sửa, từ chối bản dịch; nghe thử, sinh lại audio
- Quản lý tour, xuất bản gói offline, quản lý thông báo theo khu vực

### Quản trị viên (thêm vào quyền Biên tập viên)
- Quản lý tài khoản và phân quyền
- Quản lý ngôn ngữ, giọng đọc, ngôn ngữ dự phòng
- Quản lý bảng thuật ngữ
- Xem thống kê, xuất báo cáo

## Công nghệ
| **Thành phần** | **Công nghệ** |
| --- | --- | --- |
| App mobile | Flutter (Dart) |
| Backend API + Admin | ASP.NET Core Web API + ASP.NET Core MVC, Entity Framework Core | 
| Database | PostgreSQL (Supabase / Neon) |
| Lưu ảnh, audio | Supabase Storage / Cloudinary |
| Dịch + giọng đọc | Google Cloud Translation + Google Cloud Text-to-Speech |
| Thông báo đẩy | Firebase Cloud Messaging |
| Bản đồ | OpenStreetMap (flutter_map) |

---

## Cấu trúc thư mục

```
campus-guide/
├── mobile/        # App Flutter cho khách tham quan
├── backend/       # ASP.NET Core: API cho app + trang Admin
├── docs/          # Lược đồ (.drawio), ERD, báo cáo
└── README.md
```

---

## Cài đặt

### Yêu cầu chung
- Git
- Flutter SDK (bản stable mới nhất)
- .NET SDK 8 trở lên
- Android Studio (để có Android Emulator)

### macOS (Minh)
```bash
# Cài công cụ
brew install --cask flutter
brew install --cask dotnet-sdk
brew install gh

# Đăng nhập GitHub và clone
gh auth login
git clone https://github.com/minhnguyen107206-byte/campus-guide.git
cd campus-guide
```
- Build iOS cần Xcode (cài từ App Store) và CocoaPods: `brew install cocoapods`.
- Mở backend bằng VS Code + C# Dev Kit hoặc JetBrains Rider (Visual Studio for Mac đã ngừng hỗ trợ).

### Windows (Thắng, Vương)
- Cài **GitHub Desktop** → đăng nhập → **Clone repository** → chọn `campus-guide`.
- Cài **Visual Studio 2022** (workload *ASP.NET and web development*) để mở `backend/`.
- Cài **Flutter SDK** và **Android Studio**, chạy app bằng Android Emulator.

---

## Cấu hình

Không commit API key lên GitHub. Mỗi người tự tạo file cấu hình trên máy mình.

**Backend** – tạo `backend/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Host=...;Database=...;Username=...;Password=..."
  },
  "GoogleCloud": {
    "CredentialsPath": "path/to/service-account.json"
  },
  "Storage": {
    "Url": "...",
    "Key": "..."
  },
  "Jwt": {
    "Secret": "..."
  }
}
```

**Mobile** – tạo `mobile/.env`:
```
API_BASE_URL=http://10.0.2.2:5000
```
- Android Emulator gọi backend trên máy mình qua `10.0.2.2`.
- iOS Simulator dùng `http://localhost:5000`.
- Điện thoại thật: dùng IP LAN của máy chạy backend.

File Firebase (`google-services.json`, `GoogleService-Info.plist`) xin trong nhóm, không đưa lên repo.

---

## Chạy dự án

**Backend**
```bash
cd backend
dotnet restore
dotnet ef database update
dotnet run
```
- API: `http://localhost:5000/api`
- Trang Admin: `http://localhost:5000/admin`

**Mobile**
```bash
cd mobile
flutter pub get
flutter run
```

---

## Quy trình làm việc với Git

1. Luôn kéo code mới trước khi làm:
   ```bash
   git checkout main
   git pull
   ```
2. Tạo nhánh riêng cho từng chức năng, đặt tên theo người + use case:
   ```bash
   git checkout -b minh/uc05-quet-qr
   ```
3. Commit thường xuyên, ghi rõ nội dung:
   ```bash
   git add .
   git commit -m "UC05: quét QR và phát audio"
   git push -u origin minh/uc05-quet-qr
   ```
4. Lên GitHub tạo **Pull Request** vào `main`, nhờ một bạn khác review rồi mới merge.
5. Không push thẳng vào `main`.

Review chéo: Minh review Thắng, Thắng review Vương, Vương review Minh.

---

## Tài liệu

Lược đồ use case, đặc tả, Activity, Sequence và ERD nằm trong thư mục `docs/`.

