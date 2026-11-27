using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusGuide.Web.Models;

// ===================== Hằng số trạng thái =====================
public static class TrangThaiDiem
{
    public const string Nhap = "Nhap";
    public const string DaXuatBan = "DaXuatBan";
    public const string DaAn = "DaAn";
}

public static class TrangThaiNoiDung
{
    public const string Goc = "Goc";              // bản gốc tiếng Việt
    public const string ChoDuyet = "ChoDuyet";
    public const string DaDuyet = "DaDuyet";
    public const string TuChoi = "TuChoi";
    public const string CanCapNhat = "CanCapNhat";
    public const string LoiDich = "LoiDich";
}

public static class VaiTroTen
{
    public const string BienTapVien = "BienTapVien";
    public const string QuanTriVien = "QuanTriVien";
}

public static class NguonNghe
{
    public const string QR = "QR";
    public const string GPS = "GPS";
    public const string DanhSach = "DanhSach";
    public const string Tour = "Tour";
}

// ===================== Tài khoản =====================
public class VaiTro
{
    [Key] public int MaVaiTro { get; set; }
    [MaxLength(50)] public string TenVaiTro { get; set; } = "";
    [MaxLength(30)] public string MaCode { get; set; } = "";   // BienTapVien / QuanTriVien
    public List<NguoiDung> NguoiDungs { get; set; } = new();
}

public class NguoiDung
{
    [Key] public int MaND { get; set; }
    [MaxLength(100)] public string HoTen { get; set; } = "";
    [MaxLength(100)] public string Email { get; set; } = "";
    [MaxLength(255)] public string MatKhauHash { get; set; } = "";
    public int MaVaiTro { get; set; }
    public VaiTro? VaiTro { get; set; }
    [MaxLength(20)] public string TrangThai { get; set; } = "HoatDong"; // HoatDong / BiKhoa
    public int SoLanSai { get; set; }
    public DateTime? KhoaDen { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
}

// ===================== Điểm & nội dung =====================
public class LoaiDiem
{
    [Key] public int MaLoai { get; set; }
    [MaxLength(50)] public string TenLoai { get; set; } = "";
    [MaxLength(50)] public string BieuTuong { get; set; } = "place";
}

public class Diem
{
    [Key] public int MaDiem { get; set; }
    [Required, MaxLength(150)] public string TenDiem { get; set; } = "";
    public int MaLoai { get; set; }
    public LoaiDiem? LoaiDiem { get; set; }
    [Column(TypeName = "decimal(9,6)")] public decimal ViDo { get; set; }
    [Column(TypeName = "decimal(9,6)")] public decimal KinhDo { get; set; }
    [Range(0, 30)] public int BanKinh { get; set; } = 30;   // bán kính kích hoạt 0–30 m (CSDL có trigger chặn ngoài khoảng)
    [MaxLength(100)] public string? Tang { get; set; }
    [MaxLength(100)] public string? GioLamViec { get; set; }
    [MaxLength(300)] public string? TuKhoa { get; set; }
    [MaxLength(300)] public string? HinhAnh { get; set; }
    [MaxLength(40)] public string? MaQR { get; set; }
    [MaxLength(20)] public string TrangThai { get; set; } = TrangThaiDiem.Nhap;
    public int? MaNguoiTao { get; set; }
    public NguoiDung? NguoiTao { get; set; }
    public DateTime NgayCapNhat { get; set; } = DateTime.UtcNow;
    public List<NoiDung> NoiDungs { get; set; } = new();
}

public class NgonNgu
{
    [Key, MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    [MaxLength(50)] public string TenNgonNgu { get; set; } = "";
    [MaxLength(10)] public string? Co { get; set; }          // emoji cờ
    public bool LaNgonNguGoc { get; set; }
    public bool LaDuPhong { get; set; }
    public bool KichHoat { get; set; } = true;
    public int ThuTu { get; set; }
    public List<GiongDoc> GiongDocs { get; set; } = new();
}

public class GiongDoc
{
    [Key] public int MaGiong { get; set; }
    [MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    public NgonNgu? NgonNgu { get; set; }
    [MaxLength(80)] public string TenGiongAPI { get; set; } = "";
    [MaxLength(10)] public string GioiTinh { get; set; } = "Nu";
    public bool MacDinh { get; set; }
}

public class NoiDung
{
    [Key] public int MaNoiDung { get; set; }
    public int MaDiem { get; set; }
    public Diem? Diem { get; set; }
    [MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    public NgonNgu? NgonNgu { get; set; }
    [MaxLength(200)] public string TieuDe { get; set; } = "";
    public string VanBan { get; set; } = "";
    public int PhienBanGoc { get; set; } = 1;
    [MaxLength(20)] public string TrangThai { get; set; } = TrangThaiNoiDung.ChoDuyet;
    [MaxLength(300)] public string? LyDoTuChoi { get; set; }
    public int? MaNguoiDuyet { get; set; }
    public NguoiDung? NguoiDuyet { get; set; }
    public DateTime? NgayDuyet { get; set; }
    public DateTime NgayCapNhat { get; set; } = DateTime.UtcNow;
    public List<Audio> Audios { get; set; } = new();
}

public class Audio
{
    [Key] public int MaAudio { get; set; }
    public int MaNoiDung { get; set; }
    public NoiDung? NoiDung { get; set; }
    public int? MaGiong { get; set; }
    public GiongDoc? GiongDoc { get; set; }
    [MaxLength(300)] public string DuongDan { get; set; } = "";
    public int ThoiLuong { get; set; }
    public DateTime NgaySinh { get; set; } = DateTime.UtcNow;
}

public class ThuatNgu
{
    [Key] public int MaThuatNgu { get; set; }
    [Required, MaxLength(150)] public string TuGoc { get; set; } = "";
    [MaxLength(300)] public string? GhiChu { get; set; }
    public List<BanDichThuatNgu> BanDichs { get; set; } = new();
}

public class BanDichThuatNgu
{
    public int MaThuatNgu { get; set; }
    public ThuatNgu? ThuatNgu { get; set; }
    [MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    public NgonNgu? NgonNgu { get; set; }
    [MaxLength(150)] public string BanDich { get; set; } = "";
}

// ===================== Tour, gói offline, thông báo =====================
public class Tour
{
    [Key] public int MaTour { get; set; }
    [Required, MaxLength(150)] public string TenTour { get; set; } = "";
    [MaxLength(1000)] public string? MoTa { get; set; }
    [MaxLength(300)] public string? AnhBia { get; set; }
    public int QuangDuong { get; set; }
    public int ThoiLuong { get; set; }
    [MaxLength(20)] public string TrangThai { get; set; } = TrangThaiDiem.Nhap;
    public bool CanXuatBanLai { get; set; }
    public List<TourDiem> Diems { get; set; } = new();
}

public class TourDiem
{
    public int MaTour { get; set; }
    public Tour? Tour { get; set; }
    public int MaDiem { get; set; }
    public Diem? Diem { get; set; }
    public int ThuTu { get; set; }
}

public class GoiOffline
{
    [Key] public int MaGoi { get; set; }
    public int MaTour { get; set; }
    public Tour? Tour { get; set; }
    [MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    public int PhienBan { get; set; }
    public long DungLuong { get; set; }
    [MaxLength(64)] public string Checksum { get; set; } = "";
    [MaxLength(300)] public string DuongDan { get; set; } = "";
    public DateTime NgayXuatBan { get; set; } = DateTime.UtcNow;
}

public class ThongBao
{
    [Key] public int MaThongBao { get; set; }
    [Required, MaxLength(200)] public string TieuDe { get; set; } = "";
    [MaxLength(1000)] public string NoiDung { get; set; } = "";
    [Column(TypeName = "decimal(9,6)")] public decimal ViDo { get; set; }
    [Column(TypeName = "decimal(9,6)")] public decimal KinhDo { get; set; }
    public int BanKinh { get; set; } = 100;
    [MaxLength(10)] public string MucDo { get; set; } = "Thuong"; // Thuong / Khan
    public DateTime BatDau { get; set; } = DateTime.UtcNow;
    public DateTime KetThuc { get; set; } = DateTime.UtcNow.AddDays(1);
    public int? MaNguoiTao { get; set; }
    public NguoiDung? NguoiTao { get; set; }
    public List<BanDichThongBao> BanDichs { get; set; } = new();
}

public class BanDichThongBao
{
    public int MaThongBao { get; set; }
    public ThongBao? ThongBao { get; set; }
    [MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    [MaxLength(200)] public string TieuDe { get; set; } = "";
    [MaxLength(1000)] public string NoiDung { get; set; } = "";
}

// ===================== Dữ liệu của khách (ẩn danh) =====================
public class ThietBi
{
    [Key, MaxLength(36)] public string MaThietBi { get; set; } = "";
    [MaxLength(255)] public string? FcmToken { get; set; }
    [MaxLength(10)] public string? MaNgonNgu { get; set; }
    [MaxLength(10)] public string? NenTang { get; set; }
    public DateTime NgayDangKy { get; set; } = DateTime.UtcNow;
}

public class LuotNghe
{
    [Key] public long MaLuot { get; set; }
    [MaxLength(36)] public string MaThietBi { get; set; } = "";
    public ThietBi? ThietBi { get; set; }
    public int MaDiem { get; set; }
    public Diem? Diem { get; set; }
    [MaxLength(10)] public string MaNgonNgu { get; set; } = "";
    [MaxLength(10)] public string Nguon { get; set; } = NguonNghe.DanhSach;
    public int ThoiLuongNghe { get; set; }
    public DateTime ThoiGian { get; set; } = DateTime.UtcNow;
}
public class LuotTruyCap
{
    [Key] public long MaLuot { get; set; }
    [MaxLength(64)] public string MaPhien { get; set; } = "";
    public int? MaDiem { get; set; }
    public Diem? Diem { get; set; }
    [MaxLength(10)] public string? MaNgonNgu { get; set; }
    [MaxLength(10)] public string Nguon { get; set; } = "web";
    [MaxLength(40)] public string NenTang { get; set; } = "";
    public DateTime BatDau { get; set; } = DateTime.UtcNow;
    public int ThoiGianXem { get; set; }
    public bool CoNghe { get; set; }
}

public class DanhGia
{
    [Key] public int MaDanhGia { get; set; }
    [MaxLength(36)] public string MaThietBi { get; set; } = "";
    public ThietBi? ThietBi { get; set; }
    public int MaDiem { get; set; }
    public Diem? Diem { get; set; }
    [Range(1, 5)] public int SoSao { get; set; }
    [MaxLength(500)] public string? NhanXet { get; set; }
    public DateTime ThoiGian { get; set; } = DateTime.UtcNow;
}
