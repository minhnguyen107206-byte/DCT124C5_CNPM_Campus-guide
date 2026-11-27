using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Services;

/// <summary>Điểm hiển thị trên web: nội dung theo ngôn ngữ (ContentResolver) + bản dịch máy đang chờ duyệt nếu chưa có bản đã duyệt.</summary>
public record SitePoint(PointDto P, bool Machine, string TypeIcon, string Tone, int MaLoai, int Popularity);

public record SiteTour(Tour T, int Stops, string? Cover, List<string> StopNames);

public record SiteNotice(int Id, string Title, string Body, bool Urgent, DateTime Start, DateTime End, bool Active,
                         double Lat, double Lng, int Radius);

/// <summary>Dữ liệu dùng chung cho các trang khu người dùng – chỉ đọc, dùng lại ContentResolver của app.</summary>
public class SiteContentService
{
    private readonly AppDbContext _db;
    private readonly ContentResolver _resolver;
    public SiteContentService(AppDbContext db, ContentResolver resolver) { _db = db; _resolver = resolver; }

    public async Task<List<SitePoint>> PointsAsync(string lang, string baseUrl, IEnumerable<int>? only = null)
    {
        var q = _resolver.PublishedPoints();
        if (only != null) { var ids = only.ToList(); q = q.Where(d => ids.Contains(d.MaDiem)); }
        var list = await q.ToListAsync();
        var fb = await _resolver.FallbackLangAsync();
        var ratings = await _resolver.RatingsAsync();
        var pop = await PopularityAsync();
        return list.Select(d => Build(d, lang, fb, baseUrl, ratings, pop)).ToList();
    }

    public async Task<SitePoint?> PointAsync(int id, string lang, string baseUrl)
    {
        var d = await _resolver.PublishedPoints().FirstOrDefaultAsync(x => x.MaDiem == id);
        if (d == null) return null;
        return Build(d, lang, await _resolver.FallbackLangAsync(), baseUrl, await _resolver.RatingsAsync(), await PopularityAsync());
    }

    public SitePoint Build(Diem d, string lang, string fb, string baseUrl,
                           Dictionary<int, (double avg, int count)> ratings, Dictionary<int, int> pop)
    {
        var p = _resolver.ToDto(d, lang, fb, baseUrl, ratings);
        var machine = false;
        if (lang != "vi" && p.Lang != lang)
        {
            var goc = d.NoiDungs.FirstOrDefault(n => n.TrangThai == TrangThaiNoiDung.Goc);
            var may = d.NoiDungs.FirstOrDefault(n => n.MaNgonNgu == lang && n.TrangThai == TrangThaiNoiDung.ChoDuyet
                                                    && !string.IsNullOrWhiteSpace(n.VanBan)
                                                    && (goc == null || n.PhienBanGoc == goc.PhienBanGoc));
            if (may != null)
            {
                p = p with { Title = may.TieuDe, Text = may.VanBan, Lang = lang, IsFallback = false, AudioUrl = null };
                machine = true;
            }
        }
        var icon = d.LoaiDiem?.BieuTuong;
        return new SitePoint(p, machine, SiteContext.Icon(icon), SiteContext.Tone(icon), d.MaLoai, pop.GetValueOrDefault(d.MaDiem));
    }

    /// <summary>Độ "hot" của điểm = lượt nghe + lượt truy cập trong 30 ngày.</summary>
    public async Task<Dictionary<int, int>> PopularityAsync()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var nghe = await _db.LuotNghes.AsNoTracking().Where(l => l.ThoiGian >= since)
            .GroupBy(l => l.MaDiem).Select(g => new { g.Key, C = g.Count() }).ToListAsync();
        var xem = await _db.LuotTruyCaps.AsNoTracking().Where(l => l.BatDau >= since && l.MaDiem != null)
            .GroupBy(l => l.MaDiem!.Value).Select(g => new { g.Key, C = g.Count() }).ToListAsync();
        var res = nghe.ToDictionary(x => x.Key, x => x.C);
        foreach (var x in xem) res[x.Key] = res.GetValueOrDefault(x.Key) + x.C;
        return res;
    }

    public async Task<List<LoaiDiem>> TypesAsync() =>
        await _db.LoaiDiems.AsNoTracking()
            .Where(l => _db.Diems.Any(d => d.MaLoai == l.MaLoai && d.TrangThai == TrangThaiDiem.DaXuatBan))
            .OrderBy(l => l.MaLoai).ToListAsync();

    public async Task<List<SiteTour>> ToursAsync(string baseUrl)
    {
        var tours = await _db.Tours.AsNoTracking().Include(t => t.Diems).ThenInclude(x => x.Diem)
            .Where(t => t.TrangThai == TrangThaiDiem.DaXuatBan).OrderBy(t => t.MaTour).ToListAsync();
        return tours.Select(t =>
        {
            var stops = t.Diems.Where(x => x.Diem != null && x.Diem.TrangThai == TrangThaiDiem.DaXuatBan).OrderBy(x => x.ThuTu).ToList();
            var cover = ContentResolver.Abs(baseUrl, t.AnhBia)
                        ?? stops.Select(x => ContentResolver.Abs(baseUrl, x.Diem!.HinhAnh)).FirstOrDefault(x => x != null);
            return new SiteTour(t, stops.Count, cover, stops.Select(x => x.Diem!.TenDiem).ToList());
        }).ToList();
    }

    /// <summary>Thông báo đang hiệu lực và sắp diễn ra (đã dịch theo ngôn ngữ nếu Admin có bản dịch).</summary>
    public async Task<List<SiteNotice>> NoticesAsync(string lang, int take = 50)
    {
        var now = DateTime.UtcNow;
        var list = await _db.ThongBaos.AsNoTracking().Include(t => t.BanDichs)
            .Where(t => t.KetThuc >= now).OrderBy(t => t.BatDau).Take(take).ToListAsync();
        return list.Select(t =>
        {
            var tr = lang == "vi" ? null : t.BanDichs.FirstOrDefault(b => b.MaNgonNgu == lang);
            return new SiteNotice(t.MaThongBao, tr?.TieuDe ?? t.TieuDe, tr?.NoiDung ?? t.NoiDung, t.MucDo == "Khan",
                t.BatDau, t.KetThuc, t.BatDau <= now, (double)t.ViDo, (double)t.KinhDo, t.BanKinh);
        })
        .OrderByDescending(n => n.Active).ThenByDescending(n => n.Urgent).ThenBy(n => n.Start).ToList();
    }

    public async Task<List<Video>> VideosAsync(int? maDiem = null)
    {
        var q = _db.Videos.AsNoTracking().Include(v => v.Diem).Where(v => v.HienThi);
        if (maDiem != null) q = q.Where(v => v.MaDiem == maDiem);
        return await q.OrderByDescending(v => v.LaTrucTiep).ThenBy(v => v.ThuTu).ThenByDescending(v => v.NgayTao).ToListAsync();
    }
}
