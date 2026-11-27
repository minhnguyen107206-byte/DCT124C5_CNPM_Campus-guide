using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampusGuide.Web.Controllers.Site;

public class LiveVm
{
    public List<Video> Videos { get; set; } = new();
    public int Online { get; set; }
    public int Listening { get; set; }
    public List<LivePoint> TopPoints { get; set; } = new();
}

/// <summary>Thông báo (Admin › Thông báo khu vực) và trang Trực tiếp: video/livestream (Admin › Video) + số khách đang tham quan.</summary>
public class NewsSiteController : SiteControllerBase
{
    private readonly SiteContentService _content;
    private readonly PresenceService _presence;
    public NewsSiteController(SiteContentService content, PresenceService presence) { _content = content; _presence = presence; }

    [HttpGet("/thong-bao")]
    public async Task<IActionResult> Notices() => View("~/Views/Site/Notices.cshtml", await _content.NoticesAsync(Lang));

    [HttpGet("/truc-tiep")]
    public async Task<IActionResult> Live()
    {
        var snap = _presence.Snapshot();
        return View("~/Views/Site/Live.cshtml", new LiveVm
        {
            Videos = await _content.VideosAsync(), Online = snap.DangTruyCap, Listening = snap.DangNghe,
            TopPoints = snap.TheoDiem.Where(p => p.PointId != null).OrderByDescending(p => p.TruyCap).Take(5).ToList(),
        });
    }

    /// <summary>Số khách đang tham quan (trang Trực tiếp tự cập nhật mỗi 10 giây).</summary>
    [HttpGet("/truc-tiep/so-lieu")]
    public IActionResult LiveData()
    {
        var s = _presence.Snapshot();
        return Json(new
        {
            online = s.DangTruyCap, listening = s.DangNghe,
            top = s.TheoDiem.Where(p => p.PointId != null).OrderByDescending(p => p.TruyCap).Take(5)
                .Select(p => new { id = p.PointId, name = p.Name, count = p.TruyCap }),
        });
    }
}
