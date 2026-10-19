using CampusGuide.Web.Areas.Admin.Models;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC20 – Quản lý thông báo theo khu vực.</summary>
public class NoticesController : AdminControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITranslationService _tr;
    private readonly IPushService _push;
    private readonly ILogger<NoticesController> _log;

    public NoticesController(AppDbContext db, ITranslationService tr, IPushService push, ILogger<NoticesController> log)
    {
        _db = db; _tr = tr; _push = push; _log = log;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.ThongBaos.AsNoTracking().Include(t => t.BanDichs).OrderByDescending(t => t.BatDau).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new NoticeVm());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await _db.ThongBaos.FindAsync(id);
        if (t == null) return NotFound();
        if (t.KetThuc < DateTime.UtcNow) { Fail("Thông báo đã hết hạn, không sửa được."); return RedirectToAction(nameof(Index)); }
        return View(new NoticeVm
        {
            MaThongBao = t.MaThongBao, TieuDe = t.TieuDe, NoiDung = t.NoiDung, ViDo = t.ViDo, KinhDo = t.KinhDo,
            BanKinh = t.BanKinh, MucDo = t.MucDo, BatDau = t.BatDau.ToLocalTime(), KetThuc = t.KetThuc.ToLocalTime(),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(NoticeVm vm)
    {
        if (vm.KetThuc <= vm.BatDau) ModelState.AddModelError(nameof(vm.KetThuc), "Thời gian kết thúc phải sau thời gian bắt đầu");
        if ((vm.KetThuc - vm.BatDau).TotalDays > 30) ModelState.AddModelError(nameof(vm.KetThuc), "Hiệu lực tối đa 30 ngày");
        if (!ModelState.IsValid) return View("Edit", vm);

        ThongBao t;
        if (vm.MaThongBao == 0) { t = new ThongBao { MaNguoiTao = CurrentUserId }; _db.ThongBaos.Add(t); }
        else
        {
            t = await _db.ThongBaos.Include(x => x.BanDichs).FirstOrDefaultAsync(x => x.MaThongBao == vm.MaThongBao) ?? throw new KeyNotFoundException();
            _db.BanDichThongBaos.RemoveRange(t.BanDichs);
        }
        t.TieuDe = vm.TieuDe.Trim(); t.NoiDung = vm.NoiDung.Trim(); t.ViDo = vm.ViDo!.Value; t.KinhDo = vm.KinhDo!.Value;
        t.BanKinh = vm.BanKinh; t.MucDo = vm.MucDo == "Khan" ? "Khan" : "Thuong";
        t.BatDau = ToUtc(vm.BatDau); t.KetThuc = ToUtc(vm.KetThuc);
        await _db.SaveChangesAsync();

        // dịch sang các ngôn ngữ đang bật
        var langs = await _db.NgonNgus.Where(n => n.KichHoat && !n.LaNgonNguGoc).Select(n => n.MaNgonNgu).ToListAsync();
        var errs = new List<string>();
        foreach (var l in langs)
        {
            try
            {
                _db.BanDichThongBaos.Add(new BanDichThongBao
                {
                    MaThongBao = t.MaThongBao, MaNgonNgu = l,
                    TieuDe = await _tr.TranslateAsync(t.TieuDe, l), NoiDung = await _tr.TranslateAsync(t.NoiDung, l),
                });
            }
            catch (Exception ex) { errs.Add($"{l}: {ex.Message}"); }
        }
        await _db.SaveChangesAsync();

        string pushMsg = "";
        if (t.MucDo == "Khan")
        {
            try
            {
                var tr = await _db.BanDichThongBaos.Where(b => b.MaThongBao == t.MaThongBao).ToListAsync();
                var devices = await _db.ThietBis.Where(d => d.FcmToken != null).ToListAsync();
                var msgs = devices.Select(d =>
                {
                    var b = tr.FirstOrDefault(x => x.MaNgonNgu == d.MaNgonNgu);
                    return new PushMessage(d.FcmToken!, b?.TieuDe ?? t.TieuDe, b?.NoiDung ?? t.NoiDung,
                        new Dictionary<string, string> { ["noticeId"] = t.MaThongBao.ToString() });
                }).ToList();
                var sent = await _push.SendAsync(msgs);
                pushMsg = _push.Enabled ? $" Đã gửi thông báo đẩy tới {sent}/{msgs.Count} thiết bị." : " (Chưa cấu hình Firebase – app sẽ nhận khi mở lại.)";
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Gửi push lỗi");
                pushMsg = " Gửi thông báo đẩy lỗi (E2) – thông báo vẫn được lưu.";
            }
        }
        if (errs.Count > 0) Fail("Một số bản dịch lỗi: " + string.Join(" | ", errs));
        Success("Đã lưu thông báo." + pushMsg);
        return RedirectToAction(nameof(Index));
    }

    // A2: kết thúc sớm
    [HttpPost]
    public async Task<IActionResult> End(int id)
    {
        var t = await _db.ThongBaos.FindAsync(id);
        if (t == null) return NotFound();
        t.KetThuc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Success("Đã kết thúc thông báo.");
        return RedirectToAction(nameof(Index));
    }
}
