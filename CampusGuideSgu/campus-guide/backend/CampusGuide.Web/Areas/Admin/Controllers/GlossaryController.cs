using System.Text;
using CampusGuide.Web.Areas.Admin.Models;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC23 – Quản lý bảng thuật ngữ.</summary>
[Authorize(Policy = "QuanTri")]
public class GlossaryController : AdminControllerBase
{
    private readonly AppDbContext _db;
    public GlossaryController(AppDbContext db) => _db = db;

    private Task<List<NgonNgu>> Langs() => _db.NgonNgus.Where(n => !n.LaNgonNguGoc).OrderBy(n => n.ThuTu).ToListAsync();

    public async Task<IActionResult> Index()
    {
        ViewBag.Langs = await Langs();
        return View(await _db.ThuatNgus.Include(t => t.BanDichs).OrderBy(t => t.TuGoc).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Langs = await Langs();
        return View("Edit", new GlossaryVm());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await _db.ThuatNgus.Include(x => x.BanDichs).FirstOrDefaultAsync(x => x.MaThuatNgu == id);
        if (t == null) return NotFound();
        ViewBag.Langs = await Langs();
        return View(new GlossaryVm
        {
            MaThuatNgu = t.MaThuatNgu, TuGoc = t.TuGoc, GhiChu = t.GhiChu,
            BanDich = t.BanDichs.ToDictionary(b => b.MaNgonNgu, b => b.BanDich),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(GlossaryVm vm)
    {
        var tu = vm.TuGoc?.Trim() ?? "";
        if (await _db.ThuatNgus.AnyAsync(t => t.TuGoc == tu && t.MaThuatNgu != vm.MaThuatNgu))
            ModelState.AddModelError(nameof(vm.TuGoc), "Thuật ngữ đã tồn tại (E1)");
        if (!ModelState.IsValid) { ViewBag.Langs = await Langs(); return View("Edit", vm); }

        ThuatNgu t;
        if (vm.MaThuatNgu == 0) { t = new ThuatNgu(); _db.ThuatNgus.Add(t); }
        else
        {
            t = await _db.ThuatNgus.Include(x => x.BanDichs).FirstOrDefaultAsync(x => x.MaThuatNgu == vm.MaThuatNgu) ?? throw new KeyNotFoundException();
            _db.BanDichThuatNgus.RemoveRange(t.BanDichs);
        }
        t.TuGoc = tu; t.GhiChu = vm.GhiChu;
        await _db.SaveChangesAsync();
        foreach (var (lang, tr) in vm.BanDich.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
            _db.BanDichThuatNgus.Add(new BanDichThuatNgu { MaThuatNgu = t.MaThuatNgu, MaNgonNgu = lang, BanDich = tr.Trim() });
        await _db.SaveChangesAsync();
        Success($"Đã lưu thuật ngữ \"{tu}\". Bấm \"Áp dụng lại\" để đánh dấu các bản dịch cũ cần dịch lại.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.ThuatNgus.Include(x => x.BanDichs).FirstOrDefaultAsync(x => x.MaThuatNgu == id);
        if (t == null) return NotFound();
        _db.BanDichThuatNgus.RemoveRange(t.BanDichs);
        _db.ThuatNgus.Remove(t);
        await _db.SaveChangesAsync();
        Success("Đã xóa thuật ngữ.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Áp dụng lại: đánh dấu Cần cập nhật cho bản dịch của các điểm có chứa thuật ngữ.</summary>
    [HttpPost]
    public async Task<IActionResult> Reapply(int id)
    {
        var t = await _db.ThuatNgus.FindAsync(id);
        if (t == null) return NotFound();
        var lower = t.TuGoc.ToLower();
        var gocs = await _db.NoiDungs.Where(n => n.TrangThai == TrangThaiNoiDung.Goc).ToListAsync();
        var ids = gocs.Where(n => (n.TieuDe + " " + n.VanBan).ToLower().Contains(lower)).Select(n => n.MaDiem).ToList();
        var trs = await _db.NoiDungs.Where(n => ids.Contains(n.MaDiem) && n.TrangThai != TrangThaiNoiDung.Goc).ToListAsync();
        trs.ForEach(n => n.TrangThai = TrangThaiNoiDung.CanCapNhat);
        await _db.SaveChangesAsync();
        Success($"{ids.Count} điểm có chứa \"{t.TuGoc}\"; {trs.Count} bản dịch được đánh dấu Cần cập nhật. Vào từng điểm bấm \"Dịch & sinh audio\".");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>A3 – Nhập từ CSV (UTF-8). Dòng đầu: tu_goc,en,ja,ko,... (Excel: Lưu thành CSV UTF-8).</summary>
    [HttpPost]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file == null || file.Length == 0) { Fail("Chọn file CSV."); return RedirectToAction(nameof(Index)); }
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var header = (await reader.ReadLineAsync())?.Split(',').Select(h => h.Trim().Trim('"')).ToArray();
        if (header == null || header.Length < 2) { Fail("File không đúng định dạng."); return RedirectToAction(nameof(Index)); }
        var langs = (await _db.NgonNgus.Select(n => n.MaNgonNgu).ToListAsync()).ToHashSet();
        int ok = 0, line = 1; var errs = new List<int>();
        string? row;
        while ((row = await reader.ReadLineAsync()) != null)
        {
            line++;
            var cols = row.Split(',').Select(c => c.Trim().Trim('"')).ToArray();
            if (cols.Length == 0 || string.IsNullOrWhiteSpace(cols[0])) { errs.Add(line); continue; }
            var tu = cols[0];
            var t = await _db.ThuatNgus.Include(x => x.BanDichs).FirstOrDefaultAsync(x => x.TuGoc == tu);
            if (t == null) { t = new ThuatNgu { TuGoc = tu }; _db.ThuatNgus.Add(t); await _db.SaveChangesAsync(); }
            for (int c = 1; c < header.Length && c < cols.Length; c++)
            {
                if (!langs.Contains(header[c]) || string.IsNullOrWhiteSpace(cols[c])) continue;
                var b = t.BanDichs.FirstOrDefault(x => x.MaNgonNgu == header[c]);
                if (b == null) _db.BanDichThuatNgus.Add(new BanDichThuatNgu { MaThuatNgu = t.MaThuatNgu, MaNgonNgu = header[c], BanDich = cols[c] });
                else b.BanDich = cols[c];
            }
            await _db.SaveChangesAsync();
            ok++;
        }
        if (errs.Count > 0) Fail($"Dòng lỗi (bỏ qua): {string.Join(", ", errs)}");
        Success($"Đã nhập {ok} thuật ngữ.");
        return RedirectToAction(nameof(Index));
    }
}

