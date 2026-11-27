using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Services;

public record PointDto(
    int Id, string Name, string Type, string TypeIcon, double Lat, double Lng, int Radius,
    string? Floor, string? Hours, string? Image, string? QrCode, string? Keywords,
    string Title, string Text, string Lang, bool IsFallback, string? AudioUrl, int AudioSeconds,
    double? Rating, int RatingCount);

/// <summary>Chọn nội dung hiển thị cho app theo ngôn ngữ: đã duyệt → ngôn ngữ dự phòng → bản gốc tiếng Việt.</summary>
public class ContentResolver
{
    private readonly AppDbContext _db;
    public ContentResolver(AppDbContext db) => _db = db;

    public IQueryable<Diem> PublishedPoints() =>
        _db.Diems.AsNoTracking()
            .Include(d => d.LoaiDiem)
            .Include(d => d.NoiDungs).ThenInclude(n => n.Audios)
            .Where(d => d.TrangThai == TrangThaiDiem.DaXuatBan);

    public async Task<string> FallbackLangAsync() =>
        await _db.NgonNgus.Where(n => n.LaDuPhong).Select(n => n.MaNgonNgu).FirstOrDefaultAsync() ?? "en";

    public async Task<Dictionary<int, (double avg, int count)>> RatingsAsync()
    {
        var rows = await _db.DanhGias.AsNoTracking().GroupBy(r => r.MaDiem)
            .Select(g => new { g.Key, Avg = g.Average(x => (double)x.SoSao), Count = g.Count() }).ToListAsync();
        return rows.ToDictionary(r => r.Key, r => (r.Avg, r.Count));
    }

    public PointDto ToDto(Diem d, string lang, string fallback, string baseUrl, Dictionary<int, (double avg, int count)>? ratings = null)
    {
        NoiDung? Pick(string l) => d.NoiDungs.FirstOrDefault(n => n.MaNgonNgu == l &&
            (n.TrangThai == TrangThaiNoiDung.DaDuyet || n.TrangThai == TrangThaiNoiDung.Goc));

        var nd = Pick(lang);
        bool fb = false;
        if (nd == null && lang != "vi") { nd = Pick(fallback); fb = true; }
        nd ??= Pick("vi");
        if (nd != null && nd.MaNgonNgu != lang) fb = true;

        var audio = nd?.Audios.OrderByDescending(a => a.NgaySinh).FirstOrDefault();
        (double avg, int count) r = default;
        bool hasR = ratings != null && ratings.TryGetValue(d.MaDiem, out r);

        return new PointDto(
            d.MaDiem, d.TenDiem, d.LoaiDiem?.TenLoai ?? "", d.LoaiDiem?.BieuTuong ?? "place",
            (double)d.ViDo, (double)d.KinhDo, d.BanKinh, d.Tang, d.GioLamViec,
            Abs(baseUrl, d.HinhAnh), d.MaQR, d.TuKhoa,
            nd?.TieuDe ?? d.TenDiem, nd?.VanBan ?? "", nd?.MaNgonNgu ?? "vi", fb,
            Abs(baseUrl, audio?.DuongDan), audio?.ThoiLuong ?? EstimateSeconds(nd?.VanBan),
            hasR ? Math.Round(r.avg, 1) : null, hasR ? r.count : 0);
    }

    /// <summary>Không có file audio (đọc bằng giọng của máy): ước tính ~14 ký tự/giây.</summary>
    public static int EstimateSeconds(string? text) => string.IsNullOrWhiteSpace(text) ? 0 : (int)Math.Ceiling(text.Length / 14.0);

    public static string? Abs(string baseUrl, string? rel) =>
        string.IsNullOrEmpty(rel) ? null : rel.StartsWith("http") ? rel : baseUrl.TrimEnd('/') + rel;
}
