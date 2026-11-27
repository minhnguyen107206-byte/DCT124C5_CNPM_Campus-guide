using System.Globalization;
using System.Text;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Controllers.Api;

/// <summary>API công khai cho app di động 
[ApiController]
[Route("api")]
public class AppApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ContentResolver _resolver;

    public AppApiController(AppDbContext db, ContentResolver resolver)
    {
        _db = db; _resolver = resolver;
    }

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    // ---------------- UC01: ngôn ngữ ----------------
    [HttpGet("languages")]
    public async Task<IActionResult> Languages() =>
        Ok(await _db.NgonNgus.AsNoTracking().Where(n => n.KichHoat).OrderBy(n => n.ThuTu)
            .Select(n => new { code = n.MaNgonNgu, name = n.TenNgonNgu, flag = n.Co, isFallback = n.LaDuPhong })
            .ToListAsync());

    // ---------------- UC02: danh sách / bản đồ ----------------
    [HttpGet("points")]
    public async Task<IActionResult> Points([FromQuery] string lang = "vi")
    {
        var fb = await _resolver.FallbackLangAsync();
        var ratings = await _resolver.RatingsAsync();
        var list = await _resolver.PublishedPoints().ToListAsync();
        return Ok(list.Select(d => _resolver.ToDto(d, lang, fb, BaseUrl, ratings)));
    }

    [HttpGet("points/{id:int}")]
    public async Task<IActionResult> Point(int id, [FromQuery] string lang = "vi")
    {
        var d = await _resolver.PublishedPoints().FirstOrDefaultAsync(x => x.MaDiem == id);
        if (d == null) return NotFound(new { message = "Không tìm thấy điểm" });
        return Ok(_resolver.ToDto(d, lang, await _resolver.FallbackLangAsync(), BaseUrl, await _resolver.RatingsAsync()));
    }

    // ---------------- UC05: tra điểm theo mã QR ----------------
    [HttpGet("points/qr/{code}")]
    public async Task<IActionResult> PointByQr(string code, [FromQuery] string lang = "vi")
    {
        code = code.Trim().Split('/').Last();
        var d = await _resolver.PublishedPoints().FirstOrDefaultAsync(x => x.MaQR == code);
        if (d == null) return NotFound(new { message = "Mã QR không hợp lệ" });
        return Ok(_resolver.ToDto(d, lang, await _resolver.FallbackLangAsync(), BaseUrl, await _resolver.RatingsAsync()));
    }

    // ---------------- UC03: tìm kiếm (không phân biệt dấu) ----------------
    [HttpGet("points/search")]
    public async Task<IActionResult> Search([FromQuery] string q = "", [FromQuery] string lang = "vi")
    {
        var key = Normalize(q);
        if (key.Length < 2) return Ok(Array.Empty<PointDto>());
        var fb = await _resolver.FallbackLangAsync();
        var ratings = await _resolver.RatingsAsync();
        var list = await _resolver.PublishedPoints().ToListAsync();
        var hits = list.Select(d => (d, score: Score(d, key))).Where(x => x.score > 0)
            .OrderByDescending(x => x.score).Select(x => _resolver.ToDto(x.d, lang, fb, BaseUrl, ratings));
        return Ok(hits);
    }

    private static int Score(Diem d, string key)
    {
        int s = 0;
        if (Normalize(d.TenDiem).Contains(key)) s += 10;
        if (Normalize(d.TuKhoa ?? "").Contains(key)) s += 6;
        if (d.NoiDungs.Any(n => Normalize(n.TieuDe + " " + n.VanBan).Contains(key))) s += 2;
        return s;
    }

    public static string Normalize(string s)
    {
        var formD = s.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    // ---------------- UC08: tour ----------------
    [HttpGet("tours")]
    public async Task<IActionResult> Tours([FromQuery] string lang = "vi")
    {
        var fb = await _resolver.FallbackLangAsync();
        var tours = await _db.Tours.AsNoTracking()
            .Include(t => t.Diems).ThenInclude(td => td.Diem)
            .Where(t => t.TrangThai == TrangThaiDiem.DaXuatBan).ToListAsync();
        var packs = await _db.GoiOfflines.AsNoTracking()
            .Where(g => g.MaNgonNgu == lang || g.MaNgonNgu == fb).ToListAsync();
        return Ok(tours.Select(t =>
        {
            var p = packs.Where(g => g.MaTour == t.MaTour && g.MaNgonNgu == lang).OrderByDescending(g => g.PhienBan).FirstOrDefault()
                    ?? packs.Where(g => g.MaTour == t.MaTour && g.MaNgonNgu == fb).OrderByDescending(g => g.PhienBan).FirstOrDefault();
            return new
            {
                id = t.MaTour, name = t.TenTour, description = t.MoTa, cover = ContentResolver.Abs(BaseUrl, t.AnhBia),
                pointCount = t.Diems.Count(td => td.Diem != null && td.Diem.TrangThai == TrangThaiDiem.DaXuatBan),
                distance = t.QuangDuong, minutes = t.ThoiLuong,
                packageVersion = p?.PhienBan, packageSize = p?.DungLuong,
            };
        }));
    }

    [HttpGet("tours/{id:int}")]
    public async Task<IActionResult> Tour(int id, [FromQuery] string lang = "vi")
    {
        var t = await _db.Tours.AsNoTracking().Include(x => x.Diems)
            .FirstOrDefaultAsync(x => x.MaTour == id && x.TrangThai == TrangThaiDiem.DaXuatBan);
        if (t == null) return NotFound();
        var ids = t.Diems.Select(x => x.MaDiem).ToList();
        var pts = await _resolver.PublishedPoints().Where(d => ids.Contains(d.MaDiem)).ToListAsync();
        var fb = await _resolver.FallbackLangAsync();
        var ratings = await _resolver.RatingsAsync();
        var ordered = t.Diems.OrderBy(x => x.ThuTu)
            .Select(x => pts.FirstOrDefault(p => p.MaDiem == x.MaDiem)).Where(p => p != null)
            .Select(p => _resolver.ToDto(p!, lang, fb, BaseUrl, ratings));
        return Ok(new
        {
            id = t.MaTour, name = t.TenTour, description = t.MoTa, cover = ContentResolver.Abs(BaseUrl, t.AnhBia),
            distance = t.QuangDuong, minutes = t.ThoiLuong, points = ordered,
        });
    }

    // ---------------- UC09: gói offline ----------------
    [HttpGet("tours/{id:int}/package")]
    public async Task<IActionResult> Package(int id, [FromQuery] string lang = "vi")
    {
        var fb = await _resolver.FallbackLangAsync();
        var p = await _db.GoiOfflines.AsNoTracking().Where(g => g.MaTour == id && g.MaNgonNgu == lang)
                    .OrderByDescending(g => g.PhienBan).FirstOrDefaultAsync()
                ?? await _db.GoiOfflines.AsNoTracking().Where(g => g.MaTour == id && g.MaNgonNgu == fb)
                    .OrderByDescending(g => g.PhienBan).FirstOrDefaultAsync();
        if (p == null) return NotFound(new { message = "Tour chưa có gói offline" });
        return Ok(new
        {
            tourId = id, lang = p.MaNgonNgu, isFallback = p.MaNgonNgu != lang, version = p.PhienBan,
            size = p.DungLuong, checksum = p.Checksum, url = ContentResolver.Abs(BaseUrl, p.DuongDan),
        });
    }

    // ---------------- UC10: thiết bị + thông báo ----------------
    public record DeviceReq(string DeviceId, string? FcmToken, string? Lang, string? Platform);

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceReq r)
    {
        if (string.IsNullOrWhiteSpace(r.DeviceId) || r.DeviceId.Length > 36) return BadRequest();
        var lang = r.Lang != null && await _db.NgonNgus.AnyAsync(n => n.MaNgonNgu == r.Lang) ? r.Lang : null;
        var tb = await _db.ThietBis.FindAsync(r.DeviceId);
        if (tb == null) _db.ThietBis.Add(new ThietBi { MaThietBi = r.DeviceId, FcmToken = r.FcmToken, MaNgonNgu = lang, NenTang = r.Platform });
        else { tb.FcmToken = r.FcmToken ?? tb.FcmToken; tb.MaNgonNgu = lang ?? tb.MaNgonNgu; tb.NenTang = r.Platform ?? tb.NenTang; }
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("notices/active")]
    public async Task<IActionResult> ActiveNotices([FromQuery] string lang = "vi")
    {
        var now = DateTime.UtcNow;
        var fb = await _resolver.FallbackLangAsync();
        var list = await _db.ThongBaos.AsNoTracking().Include(t => t.BanDichs)
            .Where(t => t.BatDau <= now && t.KetThuc >= now).ToListAsync();
        return Ok(list.Select(t =>
        {
            var tr = lang == "vi"
                ? null
                : t.BanDichs.FirstOrDefault(b => b.MaNgonNgu == lang)
                  ?? t.BanDichs.FirstOrDefault(b => b.MaNgonNgu == fb);
            return new
            {
                id = t.MaThongBao, title = tr?.TieuDe ?? t.TieuDe, body = tr?.NoiDung ?? t.NoiDung,
                lat = (double)t.ViDo, lng = (double)t.KinhDo, radius = t.BanKinh, urgent = t.MucDo == "Khan",
                startAt = t.BatDau, endAt = t.KetThuc,
            };
        }));
    }

    // ---------------- UC05/06/07: ghi lượt nghe ----------------
    public record ListenReq(string DeviceId, int PointId, string Lang, string Source, int Seconds);

    [HttpPost("listens")]
    public async Task<IActionResult> Listen([FromBody] ListenReq r)
    {
        if (!await _db.Diems.AnyAsync(d => d.MaDiem == r.PointId && d.TrangThai == TrangThaiDiem.DaXuatBan)) return NotFound();
        if (!await _db.NgonNgus.AnyAsync(n => n.MaNgonNgu == r.Lang)) return BadRequest(new { message = "Ngôn ngữ không hợp lệ" });
        if (!await EnsureDevice(r.DeviceId)) return BadRequest(new { message = "DeviceId không hợp lệ" });
        var src = new[] { NguonNghe.QR, NguonNghe.GPS, NguonNghe.Tour, NguonNghe.DanhSach }.Contains(r.Source) ? r.Source : NguonNghe.DanhSach;
        _db.LuotNghes.Add(new LuotNghe { MaThietBi = r.DeviceId, MaDiem = r.PointId, MaNgonNgu = r.Lang, Nguon = src, ThoiLuongNghe = Math.Max(0, r.Seconds) });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---------------- UC11: đánh giá ----------------
    public record RatingReq(string DeviceId, int PointId, int Stars, string? Comment);

    [HttpPost("ratings")]
    public async Task<IActionResult> Rate([FromBody] RatingReq r)
    {
        if (r.Stars < 1 || r.Stars > 5) return BadRequest(new { message = "Vui lòng chọn số sao" });
        if ((r.Comment?.Length ?? 0) > 500) return BadRequest(new { message = "Nhận xét tối đa 500 ký tự" });
        if (!await _db.Diems.AnyAsync(d => d.MaDiem == r.PointId && d.TrangThai == TrangThaiDiem.DaXuatBan)) return NotFound();
        if (!await EnsureDevice(r.DeviceId)) return BadRequest(new { message = "DeviceId không hợp lệ" });
        var old = await _db.DanhGias.FirstOrDefaultAsync(x => x.MaThietBi == r.DeviceId && x.MaDiem == r.PointId);
        if (old == null) _db.DanhGias.Add(new DanhGia { MaThietBi = r.DeviceId, MaDiem = r.PointId, SoSao = r.Stars, NhanXet = r.Comment });
        else { old.SoSao = r.Stars; old.NhanXet = r.Comment; old.ThoiGian = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
        var avg = await _db.DanhGias.Where(x => x.MaDiem == r.PointId).Select(x => (double)x.SoSao).ToListAsync();
        return Ok(new { average = Math.Round(avg.Average(), 1), count = avg.Count });
    }

    private async Task<bool> EnsureDevice(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 36) return false;
        if (!await _db.ThietBis.AnyAsync(t => t.MaThietBi == id))
        {
            _db.ThietBis.Add(new ThietBi { MaThietBi = id });
            await _db.SaveChangesAsync();
        }
        return true;
    }
}
