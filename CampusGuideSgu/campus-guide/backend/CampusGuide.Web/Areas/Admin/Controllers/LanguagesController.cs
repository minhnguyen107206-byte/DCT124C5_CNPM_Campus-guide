using CampusGuide.Web.Areas.Admin.Models;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC22 – Quản lý ngôn ngữ, giọng đọc, ngôn ngữ dự phòng.</summary>
[Authorize(Policy = "QuanTri")]
public class LanguagesController : AdminControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITtsService _tts;

    public LanguagesController(AppDbContext db, ITtsService tts)
    {
        _db = db; _tts = tts;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.NgonNgus.Include(n => n.GiongDocs).OrderBy(n => n.ThuTu).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var existing = await _db.NgonNgus.Select(n => n.MaNgonNgu).ToListAsync();
        ViewBag.Catalog = LanguageCatalog.All.Where(c => !existing.Contains(c.Code)).ToList();
        return View(new LanguageVm());
    }

    /// <summary>AJAX: danh sách giọng đọc của ngôn ngữ (gọi TTS API).</summary>
    [HttpGet]
    public async Task<IActionResult> Voices(string code)
    {
        var cat = LanguageCatalog.All.FirstOrDefault(c => c.Code == code);
        var voiceLang = cat.Voice ?? code;
        try { return Json(await _tts.ListVoicesAsync(voiceLang)); }
        catch (Exception ex) { return Json(new { error = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(LanguageVm vm)
    {
        if (await _db.NgonNgus.AnyAsync(n => n.MaNgonNgu == vm.MaNgonNgu)) ModelState.AddModelError(nameof(vm.MaNgonNgu), "Ngôn ngữ đã có");
        if (!ModelState.IsValid)
        {
            ViewBag.Catalog = LanguageCatalog.All.ToList();
            return View(vm);
        }
        var max = await _db.NgonNgus.MaxAsync(n => (int?)n.ThuTu) ?? 0;
        _db.NgonNgus.Add(new NgonNgu { MaNgonNgu = vm.MaNgonNgu, TenNgonNgu = vm.TenNgonNgu, Co = vm.Co, KichHoat = false, ThuTu = max + 1 });
        _db.GiongDocs.Add(new GiongDoc { MaNgonNgu = vm.MaNgonNgu, TenGiongAPI = vm.TenGiongAPI, GioiTinh = vm.GioiTinh, MacDinh = true });
        await _db.SaveChangesAsync();
        Success($"Đã thêm {vm.TenNgonNgu} (đang tắt). Bấm \"Dịch tất cả điểm\" ở trang Điểm thuyết minh, duyệt xong rồi bật ngôn ngữ.");
        return RedirectToAction(nameof(Index));
    }

    // A1: bật / tắt
    [HttpPost]
    public async Task<IActionResult> Toggle(string id)
    {
        var n = await _db.NgonNgus.FindAsync(id);
        if (n == null) return NotFound();
        if (n.KichHoat && (n.LaNgonNguGoc || n.LaDuPhong)) { Fail("Không thể tắt tiếng Việt hoặc ngôn ngữ dự phòng (E2)."); return RedirectToAction(nameof(Index)); }
        n.KichHoat = !n.KichHoat;
        await _db.SaveChangesAsync();
        Success($"{n.TenNgonNgu}: {(n.KichHoat ? "đã bật" : "đã tắt")}.");
        return RedirectToAction(nameof(Index));
    }

    // A2: thêm giọng / đổi giọng mặc định
    [HttpPost]
    public async Task<IActionResult> AddVoice(string id, string tenGiongAPI, string gioiTinh = "Nu")
    {
        if (string.IsNullOrWhiteSpace(tenGiongAPI)) return RedirectToAction(nameof(Index));
        if (!await _db.GiongDocs.AnyAsync(g => g.MaNgonNgu == id && g.TenGiongAPI == tenGiongAPI))
        {
            _db.GiongDocs.Add(new GiongDoc { MaNgonNgu = id, TenGiongAPI = tenGiongAPI.Trim(), GioiTinh = gioiTinh });
            await _db.SaveChangesAsync();
        }
        Success("Đã thêm giọng đọc.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DefaultVoice(int maGiong)
    {
        var g = await _db.GiongDocs.FindAsync(maGiong);
        if (g == null) return NotFound();
        var all = await _db.GiongDocs.Where(x => x.MaNgonNgu == g.MaNgonNgu).ToListAsync();
        all.ForEach(x => x.MacDinh = x.MaGiong == maGiong);
        await _db.SaveChangesAsync();
        Success($"Giọng mặc định mới: {g.TenGiongAPI}. Audio cũ giữ nguyên; sinh lại ở trang Duyệt bản dịch nếu cần.");
        return RedirectToAction(nameof(Index));
    }

    // A3: đặt ngôn ngữ dự phòng
    [HttpPost]
    public async Task<IActionResult> Fallback(string id)
    {
        var n = await _db.NgonNgus.FindAsync(id);
        if (n == null) return NotFound();
        if (!n.KichHoat || n.LaNgonNguGoc) { Fail("Chỉ chọn ngôn ngữ đang bật, khác tiếng Việt."); return RedirectToAction(nameof(Index)); }
        foreach (var x in await _db.NgonNgus.ToListAsync()) x.LaDuPhong = x.MaNgonNgu == id;
        await _db.SaveChangesAsync();
        Success($"Ngôn ngữ dự phòng: {n.TenNgonNgu}.");
        return RedirectToAction(nameof(Index));
    }
}

