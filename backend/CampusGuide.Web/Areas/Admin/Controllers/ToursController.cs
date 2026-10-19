using CampusGuide.Web.Areas.Admin.Models;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC18 – Quản lý tour · UC19 – Xuất bản gói offline.</summary>
public class ToursController : AdminControllerBase
{
    private const double WalkMetersPerMinute = 70;
    private readonly AppDbContext _db;
    private readonly IStorageService _store;
    private readonly PackageService _pkg;

    public ToursController(AppDbContext db, IStorageService store, PackageService pkg)
    {
        _db = db; _store = store; _pkg = pkg;
    }

    public async Task<IActionResult> Index()
    {
        var tours = await _db.Tours.AsNoTracking().Include(t => t.Diems).Where(t => t.TrangThai != TrangThaiDiem.DaAn)
            .OrderBy(t => t.TenTour).ToListAsync();
        ViewBag.Packages = await _db.GoiOfflines.AsNoTracking().ToListAsync();
        return View(tours);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadPoints();
        return View("Edit", new TourVm());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await _db.Tours.Include(x => x.Diems).FirstOrDefaultAsync(x => x.MaTour == id);
        if (t == null) return NotFound();
        await LoadPoints();
        return View(new TourVm
        {
            MaTour = t.MaTour, TenTour = t.TenTour, MoTa = t.MoTa, AnhBia = t.AnhBia, TrangThai = t.TrangThai,
            DiemIds = string.Join(",", t.Diems.OrderBy(d => d.ThuTu).Select(d => d.MaDiem)),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(TourVm vm)
    {
        var ids = (vm.DiemIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var x) ? x : 0).Where(x => x > 0).Distinct().ToList();
        var pts = await _db.Diems.Where(d => ids.Contains(d.MaDiem)).ToListAsync();
        ids = ids.Where(id => pts.Any(p => p.MaDiem == id)).ToList();
        if (ids.Count < 2) ModelState.AddModelError(nameof(vm.DiemIds), "Tour cần ít nhất 2 điểm (E1).");
        if (ids.Count > 30) ModelState.AddModelError(nameof(vm.DiemIds), "Tour tối đa 30 điểm.");
        var notPub = pts.Where(p => p.TrangThai != TrangThaiDiem.DaXuatBan).Select(p => p.TenDiem).ToList();
        if (notPub.Count > 0) ModelState.AddModelError(nameof(vm.DiemIds), "Các điểm chưa xuất bản (E2): " + string.Join(", ", notPub));
        if (!ModelState.IsValid) { await LoadPoints(); return View("Edit", vm); }

        Tour t;
        if (vm.MaTour == 0) { t = new Tour { TrangThai = TrangThaiDiem.Nhap }; _db.Tours.Add(t); }
        else
        {
            t = await _db.Tours.Include(x => x.Diems).FirstOrDefaultAsync(x => x.MaTour == vm.MaTour) ?? throw new KeyNotFoundException();
            _db.TourDiems.RemoveRange(t.Diems);
            t.CanXuatBanLai = true;   // A1: đổi tour → gói offline cần xuất bản lại
        }
        t.TenTour = vm.TenTour.Trim(); t.MoTa = vm.MoTa;
        if (vm.Anh != null)
        {
            using var ms = new MemoryStream();
            await vm.Anh.CopyToAsync(ms);
            _store.Delete(t.AnhBia);
            t.AnhBia = await _store.SaveAsync(ms.ToArray(), "images", $"tour_{Guid.NewGuid():N}{Path.GetExtension(vm.Anh.FileName).ToLowerInvariant()}");
        }

        // quãng đường (Haversine giữa các điểm liên tiếp) và thời lượng = đi bộ + nghe audio
        var ordered = ids.Select(id => pts.First(p => p.MaDiem == id)).ToList();
        double meters = 0;
        for (int i = 1; i < ordered.Count; i++) meters += Haversine(ordered[i - 1], ordered[i]);
        var audioSecs = await _db.NoiDungs.Where(n => ids.Contains(n.MaDiem) && n.TrangThai == TrangThaiNoiDung.Goc)
            .SelectMany(n => n.Audios).Select(a => a.ThoiLuong).ToListAsync();
        t.QuangDuong = (int)Math.Round(meters);
        t.ThoiLuong = (int)Math.Ceiling(meters / WalkMetersPerMinute + audioSecs.Sum() / 60.0 + ordered.Count);

        await _db.SaveChangesAsync();
        for (int i = 0; i < ids.Count; i++) _db.TourDiems.Add(new TourDiem { MaTour = t.MaTour, MaDiem = ids[i], ThuTu = i + 1 });
        await _db.SaveChangesAsync();
        Success($"Đã lưu tour: {ids.Count} điểm, {t.QuangDuong} m, khoảng {t.ThoiLuong} phút.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Publish(int id, bool on = true)
    {
        var t = await _db.Tours.FindAsync(id);
        if (t == null) return NotFound();
        t.TrangThai = on ? TrangThaiDiem.DaXuatBan : TrangThaiDiem.Nhap;
        await _db.SaveChangesAsync();
        Success(on ? "Tour đã hiển thị trên ứng dụng." : "Đã ẩn tour khỏi ứng dụng.");
        return RedirectToAction(nameof(Index));
    }

    // A2: ẩn tour
    [HttpPost]
    public async Task<IActionResult> Hide(int id)
    {
        var t = await _db.Tours.FindAsync(id);
        if (t == null) return NotFound();
        t.TrangThai = TrangThaiDiem.DaAn;
        await _db.SaveChangesAsync();
        Success($"Đã ẩn tour \"{t.TenTour}\"; gói offline ngừng phát hành.");
        return RedirectToAction(nameof(Index));
    }

    // ---------------- UC19 ----------------
    [HttpGet]
    public async Task<IActionResult> Packages(int id)
    {
        var t = await _db.Tours.FindAsync(id);
        if (t == null) return NotFound();
        ViewBag.Tour = t;
        ViewBag.Current = await _db.GoiOfflines.Where(g => g.MaTour == id).OrderByDescending(g => g.PhienBan).ToListAsync();
        return View(await _pkg.ReadinessAsync(id));
    }

    [HttpPost]
    public async Task<IActionResult> PublishPackage(int id, string[] langs, bool allowFallback = false)
    {
        if (langs.Length == 0) { Fail("Chọn ít nhất một ngôn ngữ."); return RedirectToAction(nameof(Packages), new { id }); }
        var done = new List<string>(); var errs = new List<string>();
        foreach (var l in langs)
        {
            try
            {
                var g = await _pkg.PublishAsync(id, l, allowFallback, BaseUrl);
                done.Add($"{l} v{g.PhienBan} ({g.DungLuong / 1024.0:0.#} KB)");
            }
            catch (Exception ex) { errs.Add($"{l}: {ex.Message}"); }
        }
        if (done.Count > 0) Success("Đã xuất bản: " + string.Join(", ", done));
        if (errs.Count > 0) Fail(string.Join(" | ", errs));
        return RedirectToAction(nameof(Packages), new { id });
    }

    private async Task LoadPoints() =>
        ViewBag.Points = await _db.Diems.AsNoTracking().Where(d => d.TrangThai == TrangThaiDiem.DaXuatBan).OrderBy(d => d.TenDiem).ToListAsync();

    private static double Haversine(Diem a, Diem b)
    {
        const double R = 6371000;
        double lat1 = (double)a.ViDo * Math.PI / 180, lat2 = (double)b.ViDo * Math.PI / 180;
        double dLat = lat2 - lat1, dLng = ((double)b.KinhDo - (double)a.KinhDo) * Math.PI / 180;
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }
}
