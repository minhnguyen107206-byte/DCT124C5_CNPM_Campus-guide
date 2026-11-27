using System.ComponentModel.DataAnnotations;

namespace CampusGuide.Web.Models;

// ===================== Khu người dùng (trang web công khai) =====================

/// <summary>Video giới thiệu / buổi phát trực tiếp (YouTube, Facebook, file .mp4) – quản lý ở Admin › Video &amp; trực tiếp.</summary>
public class Video
{
    [Key] public int MaVideo { get; set; }
    [Required, MaxLength(200)] public string TieuDe { get; set; } = "";
    [MaxLength(1000)] public string? MoTa { get; set; }
    [Required, MaxLength(500)] public string DuongDan { get; set; } = "";
    public bool LaTrucTiep { get; set; }
    public int? MaDiem { get; set; }
    public Diem? Diem { get; set; }
    public DateTime? BatDau { get; set; }
    public bool HienThi { get; set; } = true;
    public int ThuTu { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
}
