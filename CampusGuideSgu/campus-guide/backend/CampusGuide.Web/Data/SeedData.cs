using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Data;

/// <summary>Dữ liệu ban đầu: tài khoản, ngôn ngữ, 20 điểm thật và 12 thuật ngữ (nguồn: Data/Seed/*.csv của Thắng).
/// Đổi dữ liệu xong thì xoá campusguide.db để seed lại.</summary>
public static class SeedData
{
    public static async Task RunAsync(AppDbContext db)
    {
        if (await db.VaiTros.AnyAsync()) return;

        var btvRole = new VaiTro { TenVaiTro = "Biên tập viên", MaCode = VaiTroTen.BienTapVien };
        var qtvRole = new VaiTro { TenVaiTro = "Quản trị viên", MaCode = VaiTroTen.QuanTriVien };
        db.VaiTros.AddRange(btvRole, qtvRole);

        var admin = new NguoiDung
        {
            HoTen = "Quản trị hệ thống", Email = "admin@campusguide.local",
            MatKhauHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), VaiTro = qtvRole,
        };
        var editor = new NguoiDung
        {
            HoTen = "Biên tập viên mẫu", Email = "btv@campusguide.local",
            MatKhauHash = BCrypt.Net.BCrypt.HashPassword("Btv@1234"), VaiTro = btvRole,
        };
        db.NguoiDungs.AddRange(admin, editor);

        db.NgonNgus.AddRange(
            new NgonNgu { MaNgonNgu = "vi", TenNgonNgu = "Tiếng Việt", Co = "🇻🇳", LaNgonNguGoc = true, ThuTu = 1 },
            new NgonNgu { MaNgonNgu = "en", TenNgonNgu = "English", Co = "🇬🇧", LaDuPhong = true, ThuTu = 2 },
            new NgonNgu { MaNgonNgu = "ja", TenNgonNgu = "日本語", Co = "🇯🇵", ThuTu = 3 },
            new NgonNgu { MaNgonNgu = "ko", TenNgonNgu = "한국어", Co = "🇰🇷", ThuTu = 4 },
            new NgonNgu { MaNgonNgu = "zh-CN", TenNgonNgu = "中文", Co = "🇨🇳", ThuTu = 5 });
        // Giọng đọc của thiết bị: lưu mã vùng ngôn ngữ, app đọc bằng giọng có sẵn của điện thoại
        db.GiongDocs.AddRange(
            new GiongDoc { MaNgonNgu = "vi", TenGiongAPI = "vi-VN", GioiTinh = "Nu", MacDinh = true },
            new GiongDoc { MaNgonNgu = "en", TenGiongAPI = "en-US", GioiTinh = "Nu", MacDinh = true },
            new GiongDoc { MaNgonNgu = "ja", TenGiongAPI = "ja-JP", GioiTinh = "Nu", MacDinh = true },
            new GiongDoc { MaNgonNgu = "ko", TenGiongAPI = "ko-KR", GioiTinh = "Nu", MacDinh = true },
            new GiongDoc { MaNgonNgu = "zh-CN", TenGiongAPI = "zh-CN", GioiTinh = "Nu", MacDinh = true });

        // Loại điểm và 20 điểm thật – lấy từ Data/Seed/diem.csv của Thắng (đo quanh 273 An Dương Vương).
        var cong = new LoaiDiem { TenLoai = "Cổng", BieuTuong = "gate" };
        var toaNha = new LoaiDiem { TenLoai = "Toà nhà học", BieuTuong = "building" };
        var hoiTruong = new LoaiDiem { TenLoai = "Hội trường", BieuTuong = "hall" };
        var thuVien = new LoaiDiem { TenLoai = "Thư viện", BieuTuong = "library" };
        var khac = new LoaiDiem { TenLoai = "Khác", BieuTuong = "landmark" };
        var cangTin = new LoaiDiem { TenLoai = "Căng tin", BieuTuong = "food" };
        var phongBan = new LoaiDiem { TenLoai = "Phòng ban", BieuTuong = "office" };
        var giuXe = new LoaiDiem { TenLoai = "Bãi giữ xe", BieuTuong = "parking" };
        var theThao = new LoaiDiem { TenLoai = "Sân thể thao", BieuTuong = "sport" };
        db.LoaiDiems.AddRange(cong, toaNha, hoiTruong, thuVien, khac, cangTin, phongBan, giuXe, theThao);

        var mau = new (string ma, LoaiDiem loai, decimal lat, decimal lng, int banKinh, string? tang, string? gio, string tuKhoa, string vanBan)[]
        {
            ("CG01", cong, 10.759411m, 106.682068m, 35, null, "Mở cửa 06:00–21:00", "cổng, cổng chính, lối vào, bảo vệ, an dương vương",
                "Chào mừng bạn đến với Trường Đại học Sài Gòn. Đây là cổng chính của trường trên đường An Dương Vương. Đi thẳng qua cột cờ là đến sân trường; bên trái là khu Hiệu bộ, bên phải là khu D. Hãy mở bản đồ trong ứng dụng để chọn nơi bạn muốn đến."),
            ("CG02", cong, 10.760174m, 106.682716m, 25, null, "Mở cửa 06:00–21:00", "cổng phụ, cổng sau, nguyễn trãi, lối vào",
                "Đây là cổng phụ số 6 đường Nguyễn Trãi. Cổng nằm cạnh khu A và gần khu E, bãi giữ xe. Nếu bạn đi xe, hãy rẽ sang bãi giữ xe trước khi vào khu học."),
            ("A01", toaNha, 10.760174m, 106.682091m, 40, "Nhiều tầng phòng học", "07:00–21:00 (Thứ 2–Thứ 7)", "giảng đường, phòng học, khu a",
                "Bạn đang ở khu A, dãy nhà dài nằm phía trong cùng của sân trường. Đây là một trong các khu giảng đường chính. Mã phòng được ghi ở cửa từng phòng; hãy xem mã phòng trên thời khoá biểu rồi tìm theo số tầng."),
            ("A001", toaNha, 10.760037m, 106.682609m, 20, "Tầng trệt, đầu khu A", "07:00–21:00 (Thứ 2–Thứ 7)", "phòng học, a001, khu a",
                "Đây là phòng A001, nằm ở đầu khu A, gần cổng Nguyễn Trãi. Phòng thường được dùng cho các buổi học và sinh hoạt đông người. Hãy kiểm tra lịch sử dụng phòng trước khi vào."),
            ("HT01", hoiTruong, 10.760009m, 106.681622m, 30, "Hội trường lớn", "Theo lịch sự kiện", "hội trường, sự kiện, lễ, hội nghị",
                "Bạn đang đến Hội trường A. Đây là nơi tổ chức lễ khai giảng, lễ tốt nghiệp, hội thảo và các chương trình văn nghệ của sinh viên. Lịch sự kiện được thông báo trên trang của trường và trong ứng dụng."),
            ("B01", toaNha, 10.759838m, 106.681749m, 35, "Phòng học các tầng", "07:00–21:00 (Thứ 2–Thứ 7)", "giảng đường, phòng học, khu b",
                "Đây là dãy trái của khu B, nằm giữa Hội trường A và khu C. Khu B có các phòng học lý thuyết dùng chung cho nhiều khoa. Khi đi qua hành lang, bạn vui lòng giữ trật tự vì các lớp có thể đang học."),
            ("B02", toaNha, 10.759838m, 106.682413m, 35, "Phòng học các tầng", "07:00–21:00 (Thứ 2–Thứ 7)", "giảng đường, phòng học, khu b",
                "Đây là dãy phải của khu B, đối diện dãy trái qua lối đi chính. Ngay phía sau dãy này là căn tin. Hãy xem mã phòng để biết phòng học nằm ở dãy trái hay dãy phải."),
            ("KLF01", thuVien, 10.759728m, 106.681544m, 20, "Khu tự học hai bên", "07:00–21:00", "thư viện, sách, tự học, đọc sách, klf",
                "Bạn đang ở khu KLF, nằm giữa khu B và khu C phía bên trái. Hai bên là khu tự học, nơi sinh viên ôn bài và làm việc nhóm. Vui lòng giữ yên lặng để không làm phiền người khác."),
            ("CC01", khac, 10.759705m, 106.682073m, 20, null, "Mở cửa cả ngày", "cột cờ, sân trường, chào cờ",
                "Đây là cột cờ, nằm ở trung tâm sân trường. Từ đây, khu B ở phía trước, khu C ở hai bên gần cổng chính. Cột cờ cũng là nơi tổ chức lễ chào cờ và là điểm hẹn quen thuộc của sinh viên."),
            ("CT01", cangTin, 10.759728m, 106.682643m, 20, "Khu ăn uống", "06:30–19:00", "căng tin, căn tin, ăn uống, cơm, nước",
                "Bạn đang ở căn tin, nằm giữa khu B và khu C bên phải. Đây là nơi sinh viên và cán bộ ăn sáng, ăn trưa và nghỉ ngơi giữa các giờ học. Vui lòng giữ vệ sinh chung và bỏ rác đúng nơi quy định."),
            ("C01", toaNha, 10.759617m, 106.681749m, 35, "Phòng học các tầng", "07:00–21:00 (Thứ 2–Thứ 7)", "giảng đường, phòng học, khu c",
                "Đây là dãy trái của khu C, nằm gần cổng chính. Khu C có các phòng học và phòng thực hành của nhiều khoa. Từ đây, đi thẳng vào trong là khu B và Hội trường A."),
            ("C02", toaNha, 10.759617m, 106.682413m, 35, "Phòng học các tầng", "07:00–21:00 (Thứ 2–Thứ 7)", "giảng đường, phòng học, khu c",
                "Đây là dãy phải của khu C. Phía sau dãy này là căn tin và khu D. Hãy đối chiếu mã phòng trên thời khoá biểu để tìm đúng phòng học."),
            ("HB01", phongBan, 10.759426m, 106.681703m, 35, "Các phòng ban của trường", "07:30–11:30, 13:30–16:30 (Thứ 2–Thứ 6)", "hiệu bộ, phòng đào tạo, công tác sinh viên, học phí, phòng ban, học vụ",
                "Bạn đang ở khu Hiệu bộ, ngay bên trái cổng chính. Đây là nơi làm việc của Ban Giám hiệu và các phòng ban. Sinh viên cần làm thủ tục hành chính, hồ sơ hay giấy xác nhận thì đến đây trong giờ hành chính."),
            ("D01", toaNha, 10.759426m, 106.682456m, 35, "Phòng học các tầng", "07:00–21:00 (Thứ 2–Thứ 7)", "giảng đường, phòng học, khu d",
                "Đây là khu D, nằm bên phải cổng chính. Khu D có các phòng học phục vụ nhiều khoa. Từ đây, đi vào trong là khu C và căn tin."),
            ("E01", toaNha, 10.760133m, 106.682853m, 30, "Phòng học các tầng", "07:00–21:00 (Thứ 2–Thứ 7)", "giảng đường, phòng học, khu e",
                "Bạn đang ở khu E, nằm bên kia cổng số 6 Nguyễn Trãi, cạnh bãi giữ xe. Khu E có các phòng học dành cho nhiều lớp. Hãy chú ý xe ra vào khi đi qua khu vực cổng."),
            ("GX01", giuXe, 10.759907m, 106.682853m, 35, null, "06:00–21:30", "gửi xe, giữ xe, nhà xe, bãi xe",
                "Đây là bãi giữ xe của trường, nằm cạnh khu E và gần cổng Nguyễn Trãi. Bạn vui lòng giữ vé xe cẩn thận và để xe đúng khu vực được hướng dẫn."),
            ("BB01", theThao, 10.759765m, 106.682896m, 15, null, "Theo lịch sử dụng", "bóng bàn, thể thao",
                "Đây là nhà bóng bàn, nằm ở góc bãi giữ xe. Sinh viên có thể chơi bóng bàn trong giờ học thể dục hoặc theo lịch đăng ký của trường."),
            ("BR01", theThao, 10.759624m, 106.682814m, 15, null, "Theo lịch sử dụng", "bóng rổ, thể thao, sân",
                "Bạn đang ở sân bóng rổ. Sân được dùng cho các giờ học giáo dục thể chất và các hoạt động thể thao của sinh viên. Vui lòng mang giày thể thao khi vào sân."),
            ("CL01", theThao, 10.759624m, 106.682896m, 15, null, "Theo lịch sử dụng", "cầu lông, thể thao, sân",
                "Đây là sân cầu lông, nằm cạnh sân bóng rổ. Sân phục vụ các giờ học thể dục và câu lạc bộ cầu lông của sinh viên."),
            ("BD01", theThao, 10.759472m, 106.682853m, 30, null, "Theo lịch sử dụng", "bóng đá, thể thao, sân",
                "Bạn đang ở sân bóng đá, khu thể thao lớn nhất của trường. Nơi đây tổ chức giờ học thể dục, giải bóng đá sinh viên và các hoạt động ngoài trời."),
        };
        var ten = new Dictionary<string, string>
        {
            ["CG01"] = "Cổng chính 273 An Dương Vương",
            ["CG02"] = "Cổng số 6 Nguyễn Trãi",
            ["A01"] = "Khu A",
            ["A001"] = "Phòng A001",
            ["HT01"] = "Hội trường A",
            ["B01"] = "Khu B (dãy trái)",
            ["B02"] = "Khu B (dãy phải)",
            ["KLF01"] = "KLF và khu tự học",
            ["CC01"] = "Cột cờ",
            ["CT01"] = "Căn tin",
            ["C01"] = "Khu C (dãy trái)",
            ["C02"] = "Khu C (dãy phải)",
            ["HB01"] = "Khu Hiệu bộ",
            ["D01"] = "Khu D",
            ["E01"] = "Khu E",
            ["GX01"] = "Bãi giữ xe",
            ["BB01"] = "Nhà bóng bàn",
            ["BR01"] = "Sân bóng rổ",
            ["CL01"] = "Sân cầu lông",
            ["BD01"] = "Sân bóng đá",
        };

        var diems = new Dictionary<string, Diem>();
        int i = 0;
        foreach (var m in mau)
        {
            i++;
            var d = new Diem
            {
                TenDiem = ten[m.ma], LoaiDiem = m.loai, ViDo = m.lat, KinhDo = m.lng,
                BanKinh = m.banKinh, Tang = m.tang, GioLamViec = m.gio, TuKhoa = m.tuKhoa,
                MaQR = $"CG{i:D4}", TrangThai = TrangThaiDiem.DaXuatBan, NguoiTao = editor,
            };
            d.NoiDungs.Add(new NoiDung { MaNgonNgu = "vi", TieuDe = d.TenDiem, VanBan = m.vanBan, TrangThai = TrangThaiNoiDung.Goc });
            diems[m.ma] = d;
        }
        db.Diems.AddRange(diems.Values);

        var tour = new Tour
        {
            TenTour = "Tour tân sinh viên",
            MoTa = "Lộ trình làm quen với trường cho tân sinh viên: cổng chính, cột cờ, khu Hiệu bộ (các phòng ban), khu tự học, hội trường, giảng đường khu A, căn tin và bãi giữ xe.",
            QuangDuong = 412, ThoiLuong = 22, TrangThai = TrangThaiDiem.DaXuatBan,
        };
        string[] thuTu = { "CG01", "CC01", "HB01", "KLF01", "HT01", "A01", "CT01", "GX01" };
        for (int k = 0; k < thuTu.Length; k++)
            tour.Diems.Add(new TourDiem { Diem = diems[thuTu[k]], ThuTu = k + 1 });
        db.Tours.Add(tour);

        // Thuật ngữ của trường – lấy từ Data/Seed/thuat-ngu.csv của Thắng (zh → zh-CN cho khớp bảng NgonNgu).
        var thuatNgu = new (string tuGoc, string? ghiChu, (string lang, string text)[] banDich)[]
        {
            ("Trường Đại học Sài Gòn", "Tên chính thức của trường", new[] { ("en", "Saigon University"), ("zh-CN", "西贡大学"), ("ko", "사이공 대학교"), ("ja", "サイゴン大学") }),
            ("Đại học Sài Gòn", "Tên rút gọn", new[] { ("en", "Saigon University"), ("zh-CN", "西贡大学"), ("ko", "사이공 대학교"), ("ja", "サイゴン大学") }),
            ("Cơ sở chính", null, new[] { ("en", "Main Campus"), ("zh-CN", "主校区"), ("ko", "본교 캠퍼스"), ("ja", "本キャンパス") }),
            ("An Dương Vương", "Tên đường, giữ nguyên không dịch", new[] { ("en", "An Duong Vuong"), ("zh-CN", "An Duong Vuong"), ("ko", "An Duong Vuong"), ("ja", "An Duong Vuong") }),
            ("Thành phố Hồ Chí Minh", null, new[] { ("en", "Ho Chi Minh City"), ("zh-CN", "胡志明市"), ("ko", "호찌민시"), ("ja", "ホーチミン市") }),
            ("Phòng Đào tạo", null, new[] { ("en", "Office of Academic Affairs"), ("zh-CN", "教务处"), ("ko", "학사관리처"), ("ja", "教務課") }),
            ("Phòng Công tác Sinh viên", null, new[] { ("en", "Office of Student Affairs"), ("zh-CN", "学生工作处"), ("ko", "학생처"), ("ja", "学生課") }),
            ("Phòng Hợp tác Quốc tế", null, new[] { ("en", "Office of International Relations"), ("zh-CN", "国际合作处"), ("ko", "국제협력처"), ("ja", "国際交流課") }),
            ("Khoa Công nghệ Thông tin", null, new[] { ("en", "Faculty of Information Technology"), ("zh-CN", "信息技术学院"), ("ko", "정보기술학부"), ("ja", "情報技術学部") }),
            ("Dãy nhà A", null, new[] { ("en", "Building A"), ("zh-CN", "A栋"), ("ko", "A동"), ("ja", "A棟") }),
            ("Dãy nhà B", null, new[] { ("en", "Building B"), ("zh-CN", "B栋"), ("ko", "B동"), ("ja", "B棟") }),
            ("Dãy nhà C", null, new[] { ("en", "Building C"), ("zh-CN", "C栋"), ("ko", "C동"), ("ja", "C棟") }),
        };
        foreach (var t in thuatNgu)
        {
            var tn = new ThuatNgu { TuGoc = t.tuGoc, GhiChu = t.ghiChu };
            foreach (var b in t.banDich) tn.BanDichs.Add(new BanDichThuatNgu { MaNgonNgu = b.lang, BanDich = b.text });
            db.ThuatNgus.Add(tn);
        }

        await db.SaveChangesAsync();
    }
    /// <summary>Thông báo khu vực và video mẫu. Chạy cả với CSDL cũ: mỗi mục mẫu chỉ thêm khi chưa có mục cùng tiêu đề còn hiệu lực
    /// (không đụng dữ liệu đã có, chạy lại nhiều lần không bị trùng).
    /// Trả về số thông báo và số video vừa thêm.
    /// Ngày giờ tính theo ngày chạy để thông báo luôn đang hiệu lực (Hội thao là thứ Bảy gần nhất, cách ít nhất 5 ngày).</summary>
    public static async Task<(int thongBao, int video)> RunSamplesAsync(AppDbContext db)
    {
        var diems = await db.Diems.AsNoTracking().ToListAsync();
        Diem? Tim(string ten) => diems.FirstOrDefault(d => d.TenDiem == ten);

        // Ngày Hội thao: thứ Bảy gần nhất, cách hôm nay ít nhất 5 ngày
        var homNay = DateTime.Now.Date;
        var ngayHoiThao = homNay.AddDays(5);
        while (ngayHoiThao.DayOfWeek != DayOfWeek.Saturday) ngayHoiThao = ngayHoiThao.AddDays(1);
        var batDauHoiThao = ngayHoiThao.AddHours(7);   // 7:00 giờ máy chủ

        int soThongBao = 0, soVideo = 0;
        {
            var bayGio = DateTime.Now;
            var admin = await db.NguoiDungs.AsNoTracking().OrderBy(n => n.MaND).FirstOrDefaultAsync();

            ThongBao Tao(string tieuDe, string noiDung, string? tenDiem, decimal lat, decimal lng, int banKinh, string mucDo,
                double batDauTruocGio, DateTime ketThucLocal, string enTieuDe, string enNoiDung)
            {
                var d = tenDiem == null ? null : Tim(tenDiem);
                var t = new ThongBao
                {
                    TieuDe = tieuDe, NoiDung = noiDung, MucDo = mucDo, BanKinh = banKinh,
                    ViDo = d?.ViDo ?? lat, KinhDo = d?.KinhDo ?? lng,
                    BatDau = bayGio.AddHours(-batDauTruocGio).ToUniversalTime(), KetThuc = ketThucLocal.ToUniversalTime(),
                    MaNguoiTao = admin?.MaND,
                };
                t.BanDichs.Add(new BanDichThongBao { MaNgonNgu = "en", TieuDe = enTieuDe, NoiDung = enNoiDung });
                return t;
            }

            var thu = $"thứ Bảy {ngayHoiThao:dd/MM/yyyy}";
            var mauThongBao = new[] {
        
                Tao("Hội thao toàn khoa tại sân thể thao cơ sở chính",
                    $"Sự kiện Hội thao toàn khoa diễn ra vào {thu}, từ 7:00 đến 11:30 tại sân thể thao cơ sở chính (sân bóng đá, sân bóng rổ, sân cầu lông). " +
                    "Các nội dung thi đấu: bóng đá nam. Sinh viên tham gia tập trung tại sân bóng đá lúc 13h00, mang giày thể thao và áo khoa. Mời các bạn đến cổ vũ!",
                    null, 10.759600m, 106.682860m, 60, "Thuong", 2, ngayHoiThao.AddHours(12),
                    "Faculty Sports Festival at the main campus sports ground",
                    $"The Faculty Sports Festival takes place on Saturday {ngayHoiThao:dd/MM/yyyy}, 7:00–11:30, at the main campus sports ground (football, basketball and badminton courts). " +
                    "Events: men's football, basketball, badminton and tug of war. Players gather at the football field at 6:45. Come and cheer!"),
                Tao("Tạm đóng cổng số 6 Nguyễn Trãi",
                    "Cổng số 6 Nguyễn Trãi tạm đóng để sửa chữa mặt đường trong 3 ngày tới. Sinh viên và khách vui lòng ra vào bằng cổng chính 273 An Dương Vương. Xe máy vẫn gửi ở bãi giữ xe như thường.",
                    "Cổng số 6 Nguyễn Trãi", 10.760174m, 106.682716m, 60, "Khan", 1, bayGio.AddDays(3),
                    "Gate No. 6 (Nguyen Trai) temporarily closed",
                    "Gate No. 6 on Nguyen Trai Street is closed for road repairs for the next 3 days. Please use the main gate at 273 An Duong Vuong. The parking lot remains open."),
                Tao("Chào đón tân sinh viên khoá mới",
                    "Chương trình chào đón tân sinh viên diễn ra tại Hội trường A. Tân sinh viên được giới thiệu về các phòng ban, câu lạc bộ và nhận sổ tay sinh viên. Đừng quên thử Tour tân sinh viên trong ứng dụng để làm quen với trường!",
                    "Hội trường A", 10.760009m, 106.681622m, 50, "Thuong", 1.5, bayGio.AddDays(10),
                    "Welcome Day for new students",
                    "The welcome programme for new students is held at Hall A: meet the offices and student clubs and get your student handbook. Try the New Student Tour in the app to get to know the campus!"),
                Tao("Bảo trì hệ thống điện khu C",
                    "Khu C (dãy trái và dãy phải) bảo trì hệ thống điện từ 12:00 đến 13:30 các ngày trong tuần này. Trong thời gian bảo trì, thang máy và điều hoà tạm ngưng; các lớp học buổi chiều vẫn diễn ra bình thường.",
                    "Khu C (dãy trái)", 10.759617m, 106.681749m, 50, "Thuong", 1, bayGio.AddDays(6),
                    "Electrical maintenance in Building C",
                    "Building C (left and right wings) has electrical maintenance from 12:00 to 13:30 every day this week. Lifts and air conditioning are off during that time; afternoon classes go ahead as usual."),
               
            };
            // Bỏ qua mẫu nào đã có thông báo cùng tiêu đề còn hiệu lực
            var utcNow = DateTime.UtcNow;
            var dangCo = await db.ThongBaos.Where(t => t.KetThuc >= utcNow).Select(t => t.TieuDe).ToListAsync();
            var themTB = mauThongBao.Where(t => !dangCo.Contains(t.TieuDe)).ToList();
            db.ThongBaos.AddRange(themTB);
            soThongBao = themTB.Count;
        }

        {
            var mauVideo = new[] {
                new Video
                {
                    TieuDe = "Giới thiệu Trường Đại học Sài Gòn", ThuTu = 2,
                    MoTa = "Video giới thiệu chung về Trường Đại học Sài Gòn (SGU).",
                    DuongDan = "https://www.youtube.com/watch?v=dCUqlcTXYd8",
                    MaDiem = Tim("Cổng chính 273 An Dương Vương")?.MaDiem,
                },
                new Video
                {
                    TieuDe = "Khám phá cơ sở chính SGU", ThuTu = 3,
                    MoTa = "Đi một vòng cơ sở chính 273 An Dương Vương: cổng, sân trường và các khu nhà.",
                    DuongDan = "https://www.youtube.com/watch?v=lwnaxwNGOrs",
                    MaDiem = Tim("Khu A")?.MaDiem,
                },
                new Video
                {
                    TieuDe = "Tour tân sinh viên: căn tin, khu tự học và khuôn viên", ThuTu = 4,
                    MoTa = "Review cơ sở chính dành cho tân sinh viên: khuôn viên, căn tin, khu tự học, nhà vệ sinh…",
                    DuongDan = "https://www.youtube.com/watch?v=DRUr1zb3Dx4",
                    MaDiem = Tim("Căn tin")?.MaDiem,
                },
                new Video
                {
                    TieuDe = "Hội thảo quốc tế về ứng dụng trí tuệ nhân tạo trong kinh tế", ThuTu = 5,
                    MoTa = "Trường Đại học Sài Gòn tổ chức hội thảo quốc tế về ứng dụng AI trong kinh tế.",
                    DuongDan = "https://www.youtube.com/watch?v=wO5XkOUU5rM",
                },
            };
            var dangCo = await db.Videos.Select(v => v.TieuDe).ToListAsync();
            var themVideo = mauVideo.Where(v => !dangCo.Contains(v.TieuDe)).ToList();
            db.Videos.AddRange(themVideo);
            soVideo = themVideo.Count;
        }

        await db.SaveChangesAsync();
        return (soThongBao, soVideo);
    }
}
