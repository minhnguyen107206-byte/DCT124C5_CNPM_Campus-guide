using CampusGuide.Web.Areas.Admin.Models;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC13 Quản lý điểm (xoá mềm, khôi phục) · UC14 Mã QR · UC15 Nội dung gốc · UC16 Dịch tự động.</summary>
public class PointsController : AdminControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStorageService _store;
    private readonly QrService _qr;
    private readonly TranslationPipeline _pipeline;

    public PointsController(AppDbContext db, IStorageService store, QrService qr, TranslationPipeline pipeline)
    {
        _db = db; _store = store; _qr = qr; _pipeline = pipeline;
    }

    // ---------------- UC13: danh sách ----------------
    public async Task<IActionResult> Index(string? q, int? loai, string? trangThai)
    {
        var query = _db.Diems.Include(d => d.LoaiDiem).Include(d => d.NoiDungs).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(d => d.TenDiem.Contains(q));
        if (loai.HasValue) query = query.Where(d => d.MaLoai == loai);
        query = string.IsNullOrEmpty(trangThai) ? query.Where(d => d.TrangThai != TrangThaiDiem.DaAn) : query.Where(d => d.TrangThai == trangThai);
        ViewBag.Loai = new SelectList(await _db.LoaiDiems.ToListAsync(), "MaLoai", "TenLoai", loai);
        ViewBag.Q = q; ViewBag.TrangThai = trangThai;
        // điểm nào đang thuộc tour nào – để hỏi xác nhận trước khi xoá
        var links = await _db.TourDiems.AsNoTracking().Select(td => new { td.MaDiem, td.Tour!.TenTour }).ToListAsync();
        ViewBag.InTours = links.GroupBy(x => x.MaDiem).ToDictionary(g => g.Key, g => g.Select(x => x.TenTour).ToList());
        return View(await query.OrderBy(d => d.TenDiem).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookups();
        return View("Edit", new PointVm());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var d = await _db.Diems.FindAsync(id);
        if (d == null) return NotFound();
        await LoadLookups();
        return View(new PointVm
        {
            MaDiem = d.MaDiem, TenDiem = d.TenDiem, MaLoai = d.MaLoai, ViDo = d.ViDo, KinhDo = d.KinhDo, BanKinh = d.BanKinh,
            Tang = d.Tang, GioLamViec = d.GioLamViec, TuKhoa = d.TuKhoa, HinhAnh = d.HinhAnh, TrangThai = d.TrangThai,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(PointVm vm)
    {
        // Tên điểm là duy nhất trong CSDL – kể cả điểm đã xoá (ẩn). Thêm mới trùng tên một điểm đã xoá
        // thì dùng lại điểm đó (khôi phục + ghi thông tin mới), giữ nội dung, bản dịch, mã QR, thống kê cũ.
        var ten = (vm.TenDiem ?? "").Trim();
        Diem? revived = null;
        var sameName = await _db.Diems.FirstOrDefaultAsync(d => d.TenDiem == ten && d.MaDiem != vm.MaDiem);
        if (sameName != null)
        {
            if (vm.MaDiem == 0 && sameName.TrangThai == TrangThaiDiem.DaAn) revived = sameName;
            else ModelState.AddModelError(nameof(vm.TenDiem), sameName.TrangThai == TrangThaiDiem.DaAn
                ? "Tên này thuộc một điểm đã xoá. Chọn bộ lọc \"Đã xoá\" rồi bấm Khôi phục điểm đó."
                : "Tên điểm đã tồn tại");
        }
        if (vm.Anh != null)
        {
            var ext = Path.GetExtension(vm.Anh.FileName).ToLowerInvariant();
            if (vm.Anh.Length > 5 * 1024 * 1024 || ext is not (".jpg" or ".jpeg" or ".png"))
                ModelState.AddModelError(nameof(vm.Anh), "Ảnh phải là JPG/PNG và không quá 5 MB");
        }
        if (!ModelState.IsValid) { await LoadLookups(); return View("Edit", vm); }

        Diem d;
        if (revived != null)
        {
            d = revived;
            d.TrangThai = TrangThaiDiem.Nhap;
        }
        else if (vm.MaDiem == 0)
        {
            d = new Diem { MaNguoiTao = CurrentUserId, TrangThai = TrangThaiDiem.Nhap };
            _db.Diems.Add(d);
        }
        else
        {
            d = await _db.Diems.FindAsync(vm.MaDiem) ?? throw new KeyNotFoundException();
            if (d.ViDo != vm.ViDo || d.KinhDo != vm.KinhDo || d.BanKinh != vm.BanKinh)
            {
                // A1: đổi vị trí → các tour chứa điểm cần xuất bản lại gói offline
                var tours = await _db.TourDiems.Where(t => t.MaDiem == d.MaDiem).Select(t => t.Tour!).ToListAsync();
                tours.ForEach(t => t.CanXuatBanLai = true);
            }
        }
        d.TenDiem = ten; d.MaLoai = vm.MaLoai;
        d.ViDo = vm.ViDo ?? throw new InvalidOperationException(); d.KinhDo = vm.KinhDo ?? throw new InvalidOperationException();
        d.BanKinh = vm.BanKinh; d.Tang = vm.Tang; d.GioLamViec = vm.GioLamViec; d.TuKhoa = vm.TuKhoa;
        d.NgayCapNhat = DateTime.UtcNow;
        if (vm.Anh != null)
        {
            using var ms = new MemoryStream();
            await vm.Anh.CopyToAsync(ms);
            if (!string.IsNullOrEmpty(d.HinhAnh) && d.HinhAnh.StartsWith("/media/")
                && !await _db.Diems.AnyAsync(x => x.MaDiem != d.MaDiem && x.HinhAnh == d.HinhAnh))
                _store.Delete(d.HinhAnh);
            d.HinhAnh = await _store.SaveAsync(ms.ToArray(), "images", $"{Guid.NewGuid():N}{Path.GetExtension(vm.Anh.FileName).ToLowerInvariant()}");
        }
        await _db.SaveChangesAsync();
        Success(revived != null
            ? $"Điểm \"{d.TenDiem}\" từng bị xoá: đã thêm lại với thông tin mới (giữ nội dung, bản dịch và mã QR cũ). Kiểm tra nội dung rồi bấm Xuất bản."
            : vm.MaDiem == 0 ? "Đã thêm điểm (trạng thái Nháp). Hãy nhập nội dung thuyết minh." : "Đã cập nhật điểm.");
        return vm.MaDiem == 0 ? RedirectToAction(nameof(ContentEdit), new { id = d.MaDiem }) : RedirectToAction(nameof(Index));
    }

    // UC13 A2: xóa mềm (ẩn) điểm để giữ nguyên dữ liệu thống kê (LuotNghe, DanhGia) và cho phép khôi phục.
    // E3: điểm đang thuộc tour → trang danh sách hỏi xác nhận; đồng ý (goKhoiTour) thì gỡ điểm khỏi các tour rồi ẩn.
    [HttpPost]
    public async Task<IActionResult> Hide(int id, bool goKhoiTour = false)
    {
        var d = await _db.Diems.FindAsync(id);
        if (d == null) return NotFound();
        var links = await _db.TourDiems.Include(t => t.Tour).Where(t => t.MaDiem == id).ToListAsync();
        if (links.Count > 0 && !goKhoiTour)
        {
            Fail($"Điểm đang thuộc tour: {string.Join(", ", links.Select(l => l.Tour!.TenTour))}. Bấm ẩn lại và xác nhận để gỡ điểm khỏi tour (E3).");
            return RedirectToAction(nameof(Index));
        }

        var notes = new List<string>();
        if (links.Count > 0)
        {
            var tourIds = links.Select(l => l.MaTour).Distinct().ToList();
            _db.TourDiems.RemoveRange(links);
            await _db.SaveChangesAsync();
            foreach (var tourId in tourIds)
            {
                var t = await _db.Tours.Include(x => x.Diems).ThenInclude(x => x.Diem).FirstAsync(x => x.MaTour == tourId);
                var stops = t.Diems.OrderBy(x => x.ThuTu).ToList();
                for (int i = 0; i < stops.Count; i++) stops[i].ThuTu = i + 1;   // đánh số lại thứ tự
                await TourCalc.RecomputeAsync(_db, t, stops.Select(x => x.Diem!).ToList());
                t.CanXuatBanLai = true;                                          // gói offline cần xuất bản lại
                var note = $"{t.TenTour} (còn {stops.Count} điểm)";
                if (stops.Count < 2 && t.TrangThai == TrangThaiDiem.DaXuatBan)
                {
                    t.TrangThai = TrangThaiDiem.Nhap;                            // tour cần ít nhất 2 điểm (UC18 E1)
                    note += " – đã chuyển về Nháp vì còn dưới 2 điểm";
                }
                notes.Add(note);
            }
        }

        d.TrangThai = TrangThaiDiem.DaAn;
        d.NgayCapNhat = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var suffix = notes.Count > 0 ? $" Đã gỡ khỏi tour: {string.Join("; ", notes)}. Nhớ xuất bản lại gói offline." : "";
        Success($"Đã ẩn điểm \"{d.TenDiem}\" (giữ nguyên dữ liệu thống kê; có thể khôi phục ở bộ lọc \"Đã xoá\").{suffix}");
        return RedirectToAction(nameof(Index));
    }

    // Khôi phục điểm đã xoá → về Nháp (kiểm tra nội dung rồi xuất bản lại)
    [HttpPost]
    public async Task<IActionResult> Restore(int id)
    {
        var d = await _db.Diems.FindAsync(id);
        if (d == null) return NotFound();
        if (d.TrangThai != TrangThaiDiem.DaAn) { Fail("Điểm này chưa bị xoá."); return RedirectToAction(nameof(Index)); }
        d.TrangThai = TrangThaiDiem.Nhap;
        d.NgayCapNhat = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Success($"Đã khôi phục \"{d.TenDiem}\" (trạng thái Nháp). Bấm Xuất bản để hiện lại trên ứng dụng.");
        return RedirectToAction(nameof(Index));
    }

    // A3: xuất bản / gỡ xuất bản
    [HttpPost]
    public async Task<IActionResult> Publish(int id, bool on = true)
    {
        var d = await _db.Diems.Include(x => x.NoiDungs).FirstOrDefaultAsync(x => x.MaDiem == id);
        if (d == null) return NotFound();
        if (on)
        {
            bool hasGoc = d.NoiDungs.Any(n => n.TrangThai == TrangThaiNoiDung.Goc && !string.IsNullOrWhiteSpace(n.VanBan));
            bool hasApproved = d.NoiDungs.Any(n => n.TrangThai == TrangThaiNoiDung.DaDuyet);
            if (!hasGoc || !hasApproved)
            {
                Fail("Cần có nội dung tiếng Việt và ít nhất một bản dịch đã duyệt trước khi xuất bản.");
                return RedirectToAction(nameof(Index));
            }
        }
        d.TrangThai = on ? TrangThaiDiem.DaXuatBan : TrangThaiDiem.Nhap;
        await _db.SaveChangesAsync();
        Success(on ? "Điểm đã được xuất bản lên ứng dụng." : "Đã chuyển điểm về Nháp.");
        return RedirectToAction(nameof(Index));
    }

    // ---------------- UC15: nội dung gốc ----------------
    [HttpGet]
    public async Task<IActionResult> ContentEdit(int id)
    {
        var d = await _db.Diems.Include(x => x.NoiDungs).ThenInclude(n => n.Audios).FirstOrDefaultAsync(x => x.MaDiem == id);
        if (d == null) return NotFound();
        var goc = d.NoiDungs.FirstOrDefault(n => n.TrangThai == TrangThaiNoiDung.Goc);
        return View(new ContentVm
        {
            MaDiem = d.MaDiem, TenDiem = d.TenDiem, TieuDe = goc?.TieuDe ?? d.TenDiem, VanBan = goc?.VanBan ?? "",
            PhienBan = goc?.PhienBanGoc ?? 0, AudioUrl = goc?.Audios.OrderByDescending(a => a.NgaySinh).FirstOrDefault()?.DuongDan,
            BanDichs = d.NoiDungs.Where(n => n.TrangThai != TrangThaiNoiDung.Goc).OrderBy(n => n.MaNgonNgu).ToList(),
            NgonNgus = await _db.NgonNgus.Where(n => n.KichHoat && !n.LaNgonNguGoc).OrderBy(n => n.ThuTu).ToListAsync(),
        });
    }

    [HttpPost]
    public async Task<IActionResult> ContentEdit(ContentVm vm, bool translateNow = false)
    {
        if (!ModelState.IsValid)
        {
            Fail("Nội dung chưa hợp lệ: " + string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(ContentEdit), new { id = vm.MaDiem });
        }
        var goc = await _db.NoiDungs.FirstOrDefaultAsync(n => n.MaDiem == vm.MaDiem && n.TrangThai == TrangThaiNoiDung.Goc);
        bool changed = goc == null || goc.TieuDe != vm.TieuDe || goc.VanBan != vm.VanBan;
        if (goc == null)
        {
            goc = new NoiDung { MaDiem = vm.MaDiem, MaNgonNgu = "vi", TrangThai = TrangThaiNoiDung.Goc, PhienBanGoc = 0 };
            _db.NoiDungs.Add(goc);
        }
        if (changed)
        {
            goc.TieuDe = vm.TieuDe.Trim(); goc.VanBan = vm.VanBan.Trim(); goc.PhienBanGoc++; goc.NgayCapNhat = DateTime.UtcNow;
            var others = await _db.NoiDungs.Where(n => n.MaDiem == vm.MaDiem && n.TrangThai != TrangThaiNoiDung.Goc).ToListAsync();
            others.ForEach(n => n.TrangThai = TrangThaiNoiDung.CanCapNhat);
        }
        await _db.SaveChangesAsync();
        if (translateNow) return await Translate(vm.MaDiem, null);
        Success(changed ? $"Đã lưu bản gốc (phiên bản {goc.PhienBanGoc}). Các bản dịch được đánh dấu Cần cập nhật." : "Nội dung không thay đổi.");
        return RedirectToAction(nameof(ContentEdit), new { id = vm.MaDiem });
    }

    // ---------------- UC16: dịch và sinh audio ----------------
    [HttpPost]
    public async Task<IActionResult> Translate(int id, string[]? langs)
    {
        try
        {
            var res = await _pipeline.TranslatePointAsync(id, langs is { Length: > 0 } ? langs : null);
            var ok = res.Count(r => r.Translated && r.Lang != "vi");
            var errs = res.Where(r => r.Error != null).Select(r => $"{r.Lang}: {r.Error}").ToList();
            if (errs.Count == 0) Success($"Đã dịch sang {ok} ngôn ngữ. Bản dịch đang chờ duyệt.");
            else Fail($"Thành công {ok} ngôn ngữ. Lỗi: {string.Join(" | ", errs)}");
        }
        catch (Exception ex) { Fail(ex.Message); }
        return RedirectToAction(nameof(ContentEdit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> TranslateAll()
    {
        var ids = await _db.NoiDungs.Where(n => n.TrangThai == TrangThaiNoiDung.Goc).Select(n => n.MaDiem).ToListAsync();
        int ok = 0, fail = 0;
        foreach (var id in ids)
        {
            try
            {
                var r = await _pipeline.TranslatePointAsync(id);
                ok += r.Count(x => x.Error == null); fail += r.Count(x => x.Error != null);
            }
            catch { fail++; }
        }
        if (fail == 0) Success($"Đã dịch {ids.Count} điểm ({ok} lượt). Vào \"Duyệt bản dịch\" để duyệt.");
        else Fail($"Hoàn thành {ok} lượt, lỗi {fail} lượt. Xem chi tiết ở từng điểm.");
        return RedirectToAction(nameof(Index));
    }

    // ---------------- UC14: mã QR ----------------
    [HttpGet]
    public async Task<IActionResult> Qr(int id)
    {
        var d = await _db.Diems.Include(x => x.LoaiDiem).Include(x => x.NoiDungs).FirstOrDefaultAsync(x => x.MaDiem == id);
        ViewBag.Langs = await _db.NgonNgus.AsNoTracking().Where(n => n.KichHoat).OrderBy(n => n.ThuTu).ToListAsync();
        if (d == null) return NotFound();
        try { await _qr.EnsureCodeAsync(d); }
        catch (InvalidOperationException ex) { Fail(ex.Message); return RedirectToAction(nameof(Index)); }
        ViewBag.Link = _qr.LinkFor(d.MaQR!);
        ViewBag.Reachable = _qr.ReachableFromPhones;
        return View(d);
    }

    [HttpGet]
    public async Task<IActionResult> QrPng(int id, bool download = false)
    {
        var d = await _db.Diems.FindAsync(id);
        if (d?.MaQR == null) return NotFound();
        var png = _qr.Png(d.MaQR);
        return download ? File(png, "image/png", $"QR_{d.MaQR}.png") : File(png, "image/png");
    }

    [HttpPost]
    public async Task<IActionResult> QrRegenerate(int id)
    {
        var d = await _db.Diems.FindAsync(id);
        if (d == null) return NotFound();
        await _qr.EnsureCodeAsync(d, regenerate: true);
        Success("Đã sinh mã mới; mã cũ không còn hiệu lực.");
        return RedirectToAction(nameof(Qr), new { id });
    }

    /// <summary>A1 – In QR hàng loạt: trang in 4 mã/trang A4 (dùng Ctrl/Cmd + P → Lưu PDF).</summary>
    [HttpGet]
    public async Task<IActionResult> QrSheet([FromQuery] int[] ids)
    {
        var list = await _db.Diems.Where(d => ids.Contains(d.MaDiem) && d.TrangThai != TrangThaiDiem.DaAn).ToListAsync();
        foreach (var d in list) await _qr.EnsureCodeAsync(d);
        return View(list);
    }

    private async Task LoadLookups() =>
        ViewBag.Loai = new SelectList(await _db.LoaiDiems.ToListAsync(), "MaLoai", "TenLoai");
}
