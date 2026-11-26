using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Services;

/// <summary>Tính quãng đường và thời lượng tour (dùng chung cho UC18 sửa tour và UC13 xoá điểm khỏi tour).</summary>
public static class TourCalc
{
    public const double WalkMetersPerMinute = 70;
    /// <summary>Giọng của thiết bị đọc khoảng 14 ký tự tiếng Việt mỗi giây.</summary>
    public const double CharsPerSecond = 14;

    /// <summary>Gán QuangDuong (Haversine giữa các điểm liên tiếp) và ThoiLuong = đi bộ + nghe + 1 phút mỗi điểm.</summary>
    public static async Task RecomputeAsync(AppDbContext db, Tour t, List<Diem> ordered)
    {
        double meters = 0;
        for (int i = 1; i < ordered.Count; i++) meters += Haversine(ordered[i - 1], ordered[i]);
        var ids = ordered.Select(d => d.MaDiem).ToList();
        var textLens = await db.NoiDungs.Where(n => ids.Contains(n.MaDiem) && n.TrangThai == TrangThaiNoiDung.Goc)
            .Select(n => n.VanBan.Length).ToListAsync();
        var listenSecs = textLens.Sum(len => Math.Ceiling(len / CharsPerSecond));
        t.QuangDuong = (int)Math.Round(meters);
        t.ThoiLuong = (int)Math.Ceiling(meters / WalkMetersPerMinute + listenSecs / 60.0 + ordered.Count);
    }

    public static double Haversine(Diem a, Diem b)
    {
        const double R = 6371000;
        double lat1 = (double)a.ViDo * Math.PI / 180, lat2 = (double)b.ViDo * Math.PI / 180;
        double dLat = lat2 - lat1, dLng = ((double)b.KinhDo - (double)a.KinhDo) * Math.PI / 180;
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }
}
