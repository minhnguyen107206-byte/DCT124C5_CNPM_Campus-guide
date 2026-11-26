using CampusGuide.Web.Data;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC24 – Xem thống kê và xuất báo cáo.</summary>
[Authorize(Policy = "QuanTri")]
public class StatsController : AdminControllerBase
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;

    public StatsController(AppDbContext db, StatsService stats)
    {
        _db = db; _stats = stats;
    }

    public async Task<IActionResult> Index(StatsFilter f)
    {
        if (!Validate(f)) f = new StatsFilter();
        ViewBag.Filter = f;
        ViewBag.Points = await _db.Diems.AsNoTracking().OrderBy(d => d.TenDiem).ToListAsync();
        ViewBag.Langs = await _db.NgonNgus.AsNoTracking().OrderBy(n => n.ThuTu).ToListAsync();
        return View(await _stats.ComputeAsync(f));
    }

    public async Task<IActionResult> Point(int id, DateTime? from = null, DateTime? to = null)
    {
        var d = await _db.Diems.FindAsync(id);
        if (d == null) return NotFound();
        ViewBag.Point = d;
        var lq = _db.LuotNghes.AsNoTracking().Where(l => l.MaDiem == id);
        var rq = _db.DanhGias.AsNoTracking().Where(r => r.MaDiem == id);
        if (from.HasValue)
        {
            var fDate = from.Value.Date;
            lq = lq.Where(l => l.ThoiGian >= fDate);
            rq = rq.Where(r => r.ThoiGian >= fDate);
        }
        if (to.HasValue)
        {
            var tDate = to.Value.Date.AddDays(1);
            lq = lq.Where(l => l.ThoiGian < tDate);
            rq = rq.Where(r => r.ThoiGian < tDate);
        }
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.ByLang = await lq.GroupBy(l => l.MaNgonNgu)
            .Select(g => new NameCount(g.Key, g.Count())).ToListAsync();
        return View(await rq.OrderByDescending(r => r.ThoiGian).ToListAsync());
    }

    public async Task<IActionResult> Export(StatsFilter f)
    {
        if (!Validate(f)) return RedirectToAction(nameof(Index));
        var bytes = await _stats.ExportExcelAsync(f);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ThongKe_{f.From:yyyyMMdd}_{f.To:yyyyMMdd}.xlsx");
    }

    private bool Validate(StatsFilter f)
    {
        if (f.From > f.To || (f.To - f.From).TotalDays > 366)
        {
            Fail("Khoảng thời gian không hợp lệ (tối đa 1 năm, ngày bắt đầu trước ngày kết thúc) – E2.");
            return false;
        }
        return true;
    }
}
