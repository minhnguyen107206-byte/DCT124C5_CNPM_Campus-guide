using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Controllers.Site;

public class HomeVm
{
    public List<SitePoint> Featured { get; set; } = new();
    public List<SiteTour> Tours { get; set; } = new();
    public List<SiteNotice> Notices { get; set; } = new();
    public Video? Live { get; set; }
    public int PointCount { get; set; }
    public int TermCount { get; set; }
}

/// <summary>Trang chủ khu người dùng + đổi ngôn ngữ.</summary>
public class HomeSiteController : SiteControllerBase
{
    private readonly SiteContentService _content;
    public HomeSiteController(SiteContentService content) => _content = content;

    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        var points = await _content.PointsAsync(Lang, BaseUrl);
        var videos = await _content.VideosAsync();
        var vm = new HomeVm
        {
            Featured = points.OrderByDescending(p => p.Popularity).ThenByDescending(p => p.P.Rating ?? 0)
                .ThenBy(p => p.P.Id).Take(6).ToList(),
            Tours = (await _content.ToursAsync(BaseUrl)).Take(4).ToList(),
            Notices = (await _content.NoticesAsync(Lang)).Take(3).ToList(),
            Live = videos.FirstOrDefault(v => v.LaTrucTiep) ?? videos.FirstOrDefault(),
            PointCount = points.Count,
            TermCount = await Db.ThuatNgus.CountAsync(),
        };
        return View("~/Views/Site/Home.cshtml", vm);
    }

    /// <summary>Đổi ngôn ngữ giao diện + nội dung, quay lại trang đang xem.</summary>
    [HttpGet("/ngon-ngu/{code}")]
    public async Task<IActionResult> SetLanguage(string code, string? returnUrl)
    {
        if (await Db.NgonNgus.AnyAsync(n => n.MaNgonNgu == code && n.KichHoat)) SaveLangCookie(code);
        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }

    [HttpGet("/gioi-thieu")]
    public IActionResult About() => View("~/Views/Site/About.cshtml");
}
