using System.ComponentModel.DataAnnotations;
using CampusGuide.Web.Models;

namespace CampusGuide.Web.Areas.Admin.Models;

public class LoginVm
{
    [Required(ErrorMessage = "Nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu"), DataType(DataType.Password)]
    public string MatKhau { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class ChangePasswordVm
{
    [Required(ErrorMessage = "Nhập mật khẩu cũ"), DataType(DataType.Password)] public string MatKhauCu { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu mới"), DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu tối thiểu 8 ký tự, gồm chữ và số")]
    public string MatKhauMoi { get; set; } = "";
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Xác nhận không khớp"), DataType(DataType.Password)]
    public string XacNhan { get; set; } = "";
}

public class PointVm
{
    public int MaDiem { get; set; }
    [Required(ErrorMessage = "Nhập tên điểm"), MaxLength(150)] public string TenDiem { get; set; } = "";
    [Required(ErrorMessage = "Chọn loại điểm")] public int MaLoai { get; set; }
    [Required(ErrorMessage = "Chọn vị trí trên bản đồ"), Range(-90, 90)] public decimal? ViDo { get; set; }
    [Required(ErrorMessage = "Chọn vị trí trên bản đồ"), Range(-180, 180)] public decimal? KinhDo { get; set; }
    [Range(0, 30, ErrorMessage = "Bán kính phải từ 0 đến 30 m")] public int BanKinh { get; set; } = 30;
    [MaxLength(100)] public string? Tang { get; set; }
    [MaxLength(100)] public string? GioLamViec { get; set; }
    [MaxLength(300)] public string? TuKhoa { get; set; }
    public string? HinhAnh { get; set; }
    public IFormFile? Anh { get; set; }
    public string TrangThai { get; set; } = TrangThaiDiem.Nhap;
}

public class ContentVm
{
    public int MaDiem { get; set; }
    public string TenDiem { get; set; } = "";
    [Required(ErrorMessage = "Nhập tiêu đề"), MaxLength(200)] public string TieuDe { get; set; } = "";
    [Required(ErrorMessage = "Nhập văn bản thuyết minh"), MaxLength(5000, ErrorMessage = "Tối đa 5.000 ký tự")]
    public string VanBan { get; set; } = "";
    public int PhienBan { get; set; }
    public string? AudioUrl { get; set; }
    public List<NoiDung> BanDichs { get; set; } = new();
    public List<NgonNgu> NgonNgus { get; set; } = new();
}

public class TourVm
{
    public int MaTour { get; set; }
    [Required(ErrorMessage = "Nhập tên tour"), MaxLength(150)] public string TenTour { get; set; } = "";
    [MaxLength(1000)] public string? MoTa { get; set; }
    public string? AnhBia { get; set; }
    public IFormFile? Anh { get; set; }
    /// <summary>Mã điểm theo thứ tự, phân tách bằng dấu phẩy (kéo thả trên giao diện)</summary>
    public string DiemIds { get; set; } = "";
    public string TrangThai { get; set; } = TrangThaiDiem.Nhap;
}

public class NoticeVm
{
    public int MaThongBao { get; set; }
    [Required(ErrorMessage = "Nhập tiêu đề"), MaxLength(200)] public string TieuDe { get; set; } = "";
    [Required(ErrorMessage = "Nhập nội dung"), MaxLength(1000)] public string NoiDung { get; set; } = "";
    [Required(ErrorMessage = "Vẽ vùng trên bản đồ"), Range(-90, 90, ErrorMessage = "Vĩ độ không hợp lệ")] public decimal? ViDo { get; set; }
    [Required(ErrorMessage = "Vẽ vùng trên bản đồ"), Range(-180, 180, ErrorMessage = "Kinh độ không hợp lệ")] public decimal? KinhDo { get; set; }
    [Range(20, 1000, ErrorMessage = "Bán kính từ 20 đến 1.000 m")] public int BanKinh { get; set; } = 100;
    public string MucDo { get; set; } = "Thuong";
    public DateTime BatDau { get; set; } = DateTime.Now;
    public DateTime KetThuc { get; set; } = DateTime.Now.AddDays(1);
}

public class UserVm
{
    public int MaND { get; set; }
    [Required(ErrorMessage = "Nhập họ tên"), MaxLength(100)] public string HoTen { get; set; } = "";
    [Required(ErrorMessage = "Nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")] public string Email { get; set; } = "";
    public int MaVaiTro { get; set; }
}

public class LanguageVm
{
    [Required(ErrorMessage = "Chọn ngôn ngữ"), MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    [Required(ErrorMessage = "Nhập tên hiển thị"), MaxLength(50)] public string TenNgonNgu { get; set; } = "";
    [MaxLength(10)] public string? Co { get; set; }
    [Required(ErrorMessage = "Chọn giọng đọc")] public string TenGiongAPI { get; set; } = "";
    public string GioiTinh { get; set; } = "Nu";
}

public class GlossaryVm
{
    public int MaThuatNgu { get; set; }
    [Required(ErrorMessage = "Nhập từ gốc"), MaxLength(150)] public string TuGoc { get; set; } = "";
    [MaxLength(300)] public string? GhiChu { get; set; }
    public Dictionary<string, string> BanDich { get; set; } = new();
}

public static class LanguageCatalog
{
    /// <summary>Một số ngôn ngữ MyMemory dịch được: (mã dịch, mã giọng đọc của thiết bị, tên, cờ)</summary>
    public static readonly (string Code, string Voice, string Name, string Flag)[] All =
    {
        ("en", "en-US", "English", "🇬🇧"), ("ja", "ja-JP", "日本語", "🇯🇵"), ("ko", "ko-KR", "한국어", "🇰🇷"),
        ("zh-CN", "zh-CN", "中文", "🇨🇳"), ("fr", "fr-FR", "Français", "🇫🇷"), ("de", "de-DE", "Deutsch", "🇩🇪"),
        ("es", "es-ES", "Español", "🇪🇸"), ("ru", "ru-RU", "Русский", "🇷🇺"), ("th", "th-TH", "ไทย", "🇹🇭"),
        ("id", "id-ID", "Bahasa Indonesia", "🇮🇩"), ("lo", "lo-LA", "ລາວ", "🇱🇦"), ("km", "km-KH", "ខ្មែរ", "🇰🇭"),
    };
}
