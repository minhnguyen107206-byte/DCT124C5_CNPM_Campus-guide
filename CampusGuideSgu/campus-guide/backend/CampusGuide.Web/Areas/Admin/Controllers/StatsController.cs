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

    public async Task<IActionResult> Point(int id)
    {
        var d = await _db.Diems.FindAsync(id);
        if (d == null) return NotFound();
        ViewBag.Point = d;
        ViewBag.ByLang = await _db.LuotNghes.Where(l => l.MaDiem == id).GroupBy(l => l.MaNgonNgu)
            .Select(g => new NameCount(g.Key, g.Count())).ToListAsync();
        return View(await _db.DanhGias.AsNoTracking().Where(r => r.MaDiem == id).OrderByDescending(r => r.ThoiGian).ToListAsync());
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
