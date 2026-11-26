using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

public class HomeController : AdminControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITranslationService _tr;
    private readonly ITtsService _tts;
    private readonly IPushService _push;

    public HomeController(AppDbContext db, ITranslationService tr, ITtsService tts, IPushService push)
    {
        _db = db; _tr = tr; _tts = tts; _push = push;
    }

    public async Task<IActionResult> Index()
    {
        var since = DateTime.UtcNow.AddDays(-7);
        ViewBag.Points = await _db.Diems.CountAsync(d => d.TrangThai != TrangThaiDiem.DaAn);
        ViewBag.Published = await _db.Diems.CountAsync(d => d.TrangThai == TrangThaiDiem.DaXuatBan);
        ViewBag.Pending = await _db.NoiDungs.CountAsync(n => n.TrangThai == TrangThaiNoiDung.ChoDuyet && n.Diem!.TrangThai != TrangThaiDiem.DaAn);
        ViewBag.Tours = await _db.Tours.CountAsync(t => t.TrangThai != TrangThaiDiem.DaAn);
        ViewBag.Listens7 = await _db.LuotNghes.CountAsync(l => l.ThoiGian >= since);
        ViewBag.Notices = await _db.ThongBaos.CountAsync(t => t.KetThuc >= DateTime.UtcNow);
        ViewBag.Demo = _tr.IsDemo;
        ViewBag.Translator = _tr.Name;
        ViewBag.PushOn = _push.Enabled;
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}

