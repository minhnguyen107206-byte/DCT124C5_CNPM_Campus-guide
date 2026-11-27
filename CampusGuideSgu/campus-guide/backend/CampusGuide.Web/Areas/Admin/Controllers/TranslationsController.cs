using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC17 – Duyệt, sửa, từ chối bản dịch; nghe thử bằng giọng của trình duyệt.</summary>
public class TranslationsController : AdminControllerBase
{
    private readonly AppDbContext _db;
    private readonly TranslationPipeline _pipeline;

    public TranslationsController(AppDbContext db, TranslationPipeline pipeline)
    {
        _db = db; _pipeline = pipeline;
    }

    public async Task<IActionResult> Index(string status = TrangThaiNoiDung.ChoDuyet, string? lang = null)
    {
        var q = _db.NoiDungs.AsNoTracking().Include(n => n.Diem).Include(n => n.NgonNgu)
            .Where(n => n.TrangThai != TrangThaiNoiDung.Goc)
            .Where(n => n.Diem!.TrangThai != TrangThaiDiem.DaAn);   // điểm đã xoá thì ẩn bản dịch của nó
        if (status != "all") q = q.Where(n => n.TrangThai == status);
        if (!string.IsNullOrEmpty(lang)) q = q.Where(n => n.MaNgonNgu == lang);
        ViewBag.Status = status; ViewBag.Lang = lang;
        ViewBag.Langs = await _db.NgonNgus.Where(n => !n.LaNgonNguGoc).OrderBy(n => n.ThuTu).ToListAsync();
        return View(await q.OrderByDescending(n => n.NgayCapNhat).ToListAsync());
    }

    public async Task<IActionResult> Review(int id)
    {
        var nd = await _db.NoiDungs.Include(n => n.Diem).Include(n => n.NgonNgu).Include(n => n.Audios).ThenInclude(a => a.GiongDoc)
            .FirstOrDefaultAsync(n => n.MaNoiDung == id);
        if (nd == null || nd.TrangThai == TrangThaiNoiDung.Goc) return NotFound();
        ViewBag.Goc = await _db.NoiDungs.AsNoTracking().FirstOrDefaultAsync(n => n.MaDiem == nd.MaDiem && n.TrangThai == TrangThaiNoiDung.Goc);
        ViewBag.Voices = await _db.GiongDocs.Where(g => g.MaNgonNgu == nd.MaNgonNgu).ToListAsync();
        return View(nd);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        var nd = await _db.NoiDungs.FirstOrDefaultAsync(n => n.MaNoiDung == id);
        if (nd == null) return NotFound();
        if (string.IsNullOrWhiteSpace(nd.VanBan)) { Fail("Bản dịch đang trống."); return RedirectToAction(nameof(Review), new { id }); }
        nd.TrangThai = TrangThaiNoiDung.DaDuyet;
        nd.MaNguoiDuyet = CurrentUserId; nd.NgayDuyet = DateTime.UtcNow; nd.LyDoTuChoi = null;
        await _db.SaveChangesAsync();
        Success("Đã duyệt bản dịch.");
        return await NextOrIndex();
    }

    /// <summary>Duyệt tất cả bản dịch đang chờ duyệt (theo ngôn ngữ đang lọc; để trống = mọi ngôn ngữ).</summary>
    [HttpPost]
    public async Task<IActionResult> ApproveAll(string? lang)
    {
        var q = _db.NoiDungs.Where(n => n.TrangThai == TrangThaiNoiDung.ChoDuyet && n.VanBan != ""
                                        && n.Diem!.TrangThai != TrangThaiDiem.DaAn);
        if (!string.IsNullOrEmpty(lang)) q = q.Where(n => n.MaNgonNgu == lang);
        var list = await q.ToListAsync();
        foreach (var nd in list)
        {
            nd.TrangThai = TrangThaiNoiDung.DaDuyet;
            nd.MaNguoiDuyet = CurrentUserId; nd.NgayDuyet = DateTime.UtcNow; nd.LyDoTuChoi = null;
        }
        await _db.SaveChangesAsync();
        if (list.Count > 0) Success($"Đã duyệt {list.Count} bản dịch.");
        else Fail("Không có bản dịch nào đang chờ duyệt.");
        return RedirectToAction(nameof(Index), new { status = TrangThaiNoiDung.ChoDuyet, lang });
    }

    // A1: sửa bản dịch
    [HttpPost]
    public async Task<IActionResult> Save(int id, string tieuDe, string vanBan)
    {
        var nd = await _db.NoiDungs.FindAsync(id);
        if (nd == null) return NotFound();
        if (string.IsNullOrWhiteSpace(vanBan)) { Fail("Văn bản dịch không được để trống."); return RedirectToAction(nameof(Review), new { id }); }
        nd.TieuDe = tieuDe?.Trim() ?? ""; nd.VanBan = vanBan.Trim(); nd.NgayCapNhat = DateTime.UtcNow;
        nd.TrangThai = TrangThaiNoiDung.ChoDuyet;
        await _db.SaveChangesAsync();
        try
        {
            var audio = await _pipeline.GenerateAudioAsync(nd);
            Success(audio != null ? "Đã lưu bản dịch và sinh lại audio. Nghe thử rồi bấm Duyệt." : "Đã lưu bản dịch. Nghe thử rồi bấm Duyệt.");
        }
        catch (Exception ex) { Fail("Đã lưu văn bản nhưng sinh audio lỗi (giữ audio cũ): " + ex.Message); }
        return RedirectToAction(nameof(Review), new { id });
    }

    // A2: từ chối
    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo)) { Fail("Vui lòng nhập lý do từ chối."); return RedirectToAction(nameof(Review), new { id }); }
        var nd = await _db.NoiDungs.FindAsync(id);
        if (nd == null) return NotFound();
        nd.TrangThai = TrangThaiNoiDung.TuChoi; nd.LyDoTuChoi = lyDo.Trim();
        nd.MaNguoiDuyet = CurrentUserId; nd.NgayDuyet = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Success("Đã từ chối. Ứng dụng sẽ dùng ngôn ngữ dự phòng cho điểm này.");
        return await NextOrIndex();
    }

    private async Task<IActionResult> NextOrIndex()
    {
        var next = await _db.NoiDungs.Where(n => n.TrangThai == TrangThaiNoiDung.ChoDuyet && n.Diem!.TrangThai != TrangThaiDiem.DaAn)
            .OrderBy(n => n.NgayCapNhat).Select(n => (int?)n.MaNoiDung).FirstOrDefaultAsync();
        return next.HasValue ? RedirectToAction(nameof(Review), new { id = next }) : RedirectToAction(nameof(Index));
    }
}
