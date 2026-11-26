using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Controllers.Api;

public record PresencePing(string SessionId, string? PageId, string? Source, int? PointId, string? Lang, bool Listening, string? Platform);

/// <summary>Thiết bị báo "đang ở đây" (ping mỗi 5 giây) và "đã rời đi" – nguồn số liệu cho dashboard Trực tiếp.</summary>
[ApiController]
[Route("api/presence")]
public class PresenceController : ControllerBase
{
    private readonly PresenceService _presence;
    private readonly AppDbContext _db;

    public PresenceController(PresenceService presence, AppDbContext db)
    {
        _presence = presence; _db = db;
    }

    [HttpPost("ping")]
    public async Task<IActionResult> Ping([FromBody] PresencePing p)
    {
        if (string.IsNullOrWhiteSpace(p.SessionId)) return BadRequest(new { message = "Thiếu sessionId" });
        string? pointName = null;
        if (p.PointId != null)
            pointName = await _db.Diems.AsNoTracking().Where(d => d.MaDiem == p.PointId).Select(d => d.TenDiem).FirstOrDefaultAsync();
        var source = p.Source == "app" ? "app" : "web";
        var platform = string.IsNullOrWhiteSpace(p.Platform) || p.Platform.Length > 40
            ? PresenceService.PlatformFrom(Request.Headers.UserAgent.ToString())
            : p.Platform;
        _presence.Ping(p.SessionId.Trim(), p.PageId ?? "", source, platform, p.PointId, pointName, p.Lang, p.Listening);
        try { await LogAsync(p.SessionId.Trim()); }
        catch (Exception ex) { HttpContext.RequestServices.GetRequiredService<ILogger<PresenceController>>().LogWarning(ex, "Ghi nhật ký truy cập lỗi"); }
        return NoContent();
    }

    /// <summary>
    /// Ghi lại vào CSDL:
    ///  • LuotTruyCap – mỗi lần mở trang thuyết minh / mở app tại một điểm (cập nhật thời gian xem theo từng ping).
    ///  • LuotNghe (Nguồn = QR) – mỗi lần bấm nghe thuyết minh trên trang web quét QR (app tự ghi qua /api/app/listens).
    /// </summary>
    private async Task LogAsync(string sid)
    {
        var s = _presence.Get(sid);
        if (s == null) return;
        var now = DateTime.UtcNow;

        // --- lượt truy cập ---
        if (s.MaLuotTruyCap == null || s.DiemCuaLuot != s.PointId)
        {
            var row = new LuotTruyCap
            {
                MaPhien = s.Id, MaDiem = s.PointId, MaNgonNgu = s.Lang, Nguon = s.Source,
                NenTang = s.Platform.Length > 40 ? s.Platform[..40] : s.Platform, BatDau = now, CoNghe = s.Listening,
            };
            _db.LuotTruyCaps.Add(row);
            await _db.SaveChangesAsync();
            s.MaLuotTruyCap = row.MaLuot; s.DiemCuaLuot = s.PointId; s.BatDauLuot = now;
        }
        else
        {
            var giay = (int)(now - s.BatDauLuot).TotalSeconds;
            var nghe = s.Listening;
            await _db.LuotTruyCaps.Where(x => x.MaLuot == s.MaLuotTruyCap)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.ThoiGianXem, giay)
                                          .SetProperty(x => x.MaNgonNgu, s.Lang)
                                          .SetProperty(x => x.CoNghe, x => x.CoNghe || nghe));
        }

        // --- lượt nghe (chỉ trang web; app đã tự ghi) ---
        if (s.Source != "web" || s.PointId == null || string.IsNullOrEmpty(s.Lang)) return;
        if (s.Listening && s.MaLuotNghe == null)
        {
            var maTb = s.Id.Length > 36 ? s.Id[..36] : s.Id;
            if (!await _db.ThietBis.AnyAsync(t => t.MaThietBi == maTb))
                _db.ThietBis.Add(new ThietBi { MaThietBi = maTb, NenTang = s.Platform.Length > 10 ? s.Platform[..10] : s.Platform });
            var ln = new LuotNghe { MaThietBi = maTb, MaDiem = s.PointId.Value, MaNgonNgu = s.Lang, Nguon = NguonNghe.QR, ThoiGian = now };
            _db.LuotNghes.Add(ln);
            await _db.SaveChangesAsync();
            s.MaLuotNghe = ln.MaLuot; s.BatDauNghe = now;
        }
        else if (s.MaLuotNghe != null)
        {
            var giay = (int)(now - s.BatDauNghe).TotalSeconds;
            await _db.LuotNghes.Where(x => x.MaLuot == s.MaLuotNghe).ExecuteUpdateAsync(u => u.SetProperty(x => x.ThoiLuongNghe, giay));
            if (!s.Listening) s.MaLuotNghe = null;   // nghe xong – lần bấm sau là lượt nghe mới
        }
    }

    /// <summary>Gọi bằng navigator.sendBeacon khi rời trang nên nhận tham số trên URL (không cần body).</summary>
    [HttpPost("leave")]
    public IActionResult Leave([FromQuery] string sid, [FromQuery] string? page)
    {
        // chốt thời gian xem / thời gian nghe trước khi xoá khỏi danh sách đang online
        var s = string.IsNullOrWhiteSpace(sid) ? null : _presence.Get(sid.Trim());
        if (s != null && (string.IsNullOrEmpty(page) || s.PageId == page))
        {
            var now = DateTime.UtcNow;
            try
            {
                if (s.MaLuotTruyCap != null)
                    _db.LuotTruyCaps.Where(x => x.MaLuot == s.MaLuotTruyCap)
                        .ExecuteUpdate(u => u.SetProperty(x => x.ThoiGianXem, (int)(now - s.BatDauLuot).TotalSeconds));
                if (s.MaLuotNghe != null)
                    _db.LuotNghes.Where(x => x.MaLuot == s.MaLuotNghe)
                        .ExecuteUpdate(u => u.SetProperty(x => x.ThoiLuongNghe, (int)(now - s.BatDauNghe).TotalSeconds));
            }
            catch { /* không để lỗi ghi nhật ký làm hỏng việc rời trang */ }
        }
        _presence.Leave(sid?.Trim() ?? "", page);
        return NoContent();
    }
}
