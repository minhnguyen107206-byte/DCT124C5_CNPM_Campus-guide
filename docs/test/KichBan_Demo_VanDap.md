# Kịch bản demo vấn đáp – CampusGuide

Bám theo mục 3 của README. Thời lượng dự kiến: khoảng 10–12 phút demo, chừa thời gian cho câu hỏi của thầy.

## Chuẩn bị trước giờ demo (làm từ hôm trước)

- [ ] Backend chạy ổn; đăng nhập admin được. Đã bấm **Dịch tất cả điểm** và **Duyệt** bản dịch tiếng Anh cho cả 8 điểm (hoặc ~20 điểm nếu Thắng đã nhập xong).
- [ ] Tour mẫu đã **Xuất bản**, gói offline `en` đã xuất bản sẵn (bước 6 chỉ cần bấm lại để cho thấy phiên bản tăng).
- [ ] Mở sẵn: tab Admin (đã đăng nhập), ảnh QR của 1 điểm trên màn hình, app đã cài trên emulator/điện thoại.
- [ ] Android Emulator: đặt sẵn vị trí **xa** các điểm (để bước 5 chuyển vị trí vào gần cho thấy tự động thuyết minh).
- [ ] Xóa gói offline cũ trong app trước khi demo (để bước 6 tải lại từ đầu).
- [ ] Có sẵn video quay màn hình toàn bộ luồng làm phương án dự phòng.
- [ ] Phân vai: ai thao tác Admin, ai thao tác app, ai nói.

## Luồng demo

| # | Thao tác | Lời nói chính | Kết quả cần thấy | UC |
|---|---|---|---|---|
| 0 | Giới thiệu 30 giây | Bài toán: tân sinh viên, phụ huynh, khách quốc tế tìm đường và nghe giới thiệu tòa nhà; cán bộ chỉ nhập tiếng Việt, hệ thống tự dịch và sinh giọng đọc | Sơ đồ kiến trúc (docs/diagrams/kientruc.png) | – |
| 1 | Admin đăng nhập → **Điểm thuyết minh** → **Dịch tất cả điểm** | Cán bộ chỉ nhập nội dung gốc tiếng Việt; bấm một nút để dịch và sinh audio cho các ngôn ngữ bật | Các điểm có bản dịch (DEMO: dạng `[en] ...`) | UC16 |
| 2 | **Duyệt bản dịch** → nghe thử → **Duyệt** | Bản dịch máy phải được người duyệt trước khi tới khách | Trạng thái Đã duyệt | UC17 |
| 3 | App: chọn **English** → bản đồ 8 điểm → bấm điểm → **Listen** | Khách chọn ngôn ngữ của mình, xem điểm, nghe thuyết minh | Giao diện tiếng Anh, audio phát | UC01, 02, 07 |
| 4 | Admin → **Mã QR** → mở ảnh QR → app **Scan QR** | In QR dán ở cổng từng tòa nhà; khách quét là nghe | Mở đúng điểm, phát audio | UC14, 05 |
| 5 | Bật nút **tai nghe** + đổi vị trí giả lập vào gần một điểm | Đi tới gần là tự thuyết minh (geofence), không phát lặp trong 30 phút | App tự phát | UC06 |
| 6 | Admin → **Tour & gói offline** → **Gói offline** → xuất bản `en` → app **Tours → Download** → **tắt mạng** → nghe lại | Khu vực sóng yếu vẫn dùng được; gói gồm nội dung, ảnh, audio, kiểm tra SHA-256 | Nghe được khi tắt mạng | UC19, 09 |
| 7 | Admin → **Thông báo khu vực** → tạo mức **Khẩn** → app | Báo khẩn (đóng đường, sự kiện) tới khách; dịch tự động sang các ngôn ngữ | App hiện thông báo | UC20, 10 |
| 8 | Admin → **Thống kê** → lọc → **Xuất Excel** | Quản trị theo dõi lượt nghe, quét QR, đánh giá để cải thiện nội dung | Biểu đồ + file Excel 5 sheet | UC24 |

Gợi ý chen thêm nếu còn thời gian (phần của Vương): tạo tour mới (UC18), tạo tài khoản và phân quyền BTV (UC21), đánh giá một điểm trong app rồi xem ở Thống kê (UC11).

## Phương án dự phòng

- **Mất mạng/Wi-Fi hỏng:** chạy backend và emulator trên cùng một máy (Android dùng `10.0.2.2:5000`), không cần mạng ngoài. Riêng bản đồ nền và chỉ đường (OSRM) cần Internet; nói thẳng giới hạn này nếu thầy hỏi.
- **Chưa có API key Google:** nói rõ đang ở **chế độ DEMO** (dịch dạng `[en] ...`, audio bíp) và luồng thật chỉ khác ở lời gọi API.
- **Camera emulator không quét được QR:** dùng điện thoại thật, hoặc mở link `/q/{code}` để trình bày luồng thay thế.
- **Mọi thứ hỏng:** phát video quay sẵn.

## Câu hỏi thầy có thể hỏi (phần Vương)

**Gói offline gồm những gì và đảm bảo toàn vẹn thế nào?** Zip có `tour.json`, thư mục `audio/`, `images/`. Backend tính SHA-256 khi xuất bản; app tải về, tính lại SHA-256 và so với checksum từ API, sai thì từ chối cài. Chỉ giữ 2 phiên bản gần nhất mỗi (tour, ngôn ngữ).

**Khi sửa tour thì gói offline cũ xử lý ra sao?** Tour được đánh dấu `CanXuatBanLai`; Admin xuất bản lại thì phiên bản tăng, app tải lại sẽ thay gói cũ.

**Thiếu bản dịch thì xuất bản gói thế nào?** Mặc định chặn và liệt kê điểm thiếu; nếu bật "cho phép dự phòng", điểm thiếu dùng ngôn ngữ dự phòng.

**Thông báo khẩn tới người dùng bằng cách nào?** Lưu thông báo, dịch sang các ngôn ngữ đang bật, gửi push qua Firebase (nếu cấu hình). Chưa cấu hình thì app vẫn nhận khi đang mở, kiểm tra mỗi 2 phút. Thông báo thường chỉ hiện khi người dùng vào bán kính vùng.

**Phân quyền thế nào?** Hai vai trò: Quản trị viên và Biên tập viên. Trang Tài khoản và Thống kê chỉ Quản trị viên vào được (policy `QuanTri`). Không thể hạ/khóa Quản trị viên cuối cùng, không tự khóa mình; mật khẩu băm bằng BCrypt.

**Vì sao không có chức năng quên mật khẩu bằng email?** Bản đồ án chưa có máy chủ email; Quản trị viên đặt lại mật khẩu tạm ở trang Tài khoản.

**Thống kê tính từ đâu?** Bảng lượt nghe và đánh giá do app gửi lên (`POST /api/listens`, `/api/ratings`); backend gom nhóm theo ngày, ngôn ngữ, nguồn, điểm. Do dữ liệu nhỏ nên gom trong bộ nhớ để tránh khác biệt SQL giữa SQLite và PostgreSQL.

**Giới hạn của hệ thống?** Tự thuyết minh chỉ chạy khi app đang mở; điều khiển audio trên màn hình khóa cần `just_audio_background`; bản đồ nền vẫn cần mạng; push khi app đóng cần cấu hình Firebase.

> Trước khi vào demo, nên chạy qua ít nhất một lượt và ghi lại các điểm app/backend lỗi để cả nhóm thống nhất cách nói.
