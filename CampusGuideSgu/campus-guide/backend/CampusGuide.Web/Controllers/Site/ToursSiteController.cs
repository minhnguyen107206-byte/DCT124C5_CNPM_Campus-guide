using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampusGuide.Web.Controllers.Site;

public class TourDetailVm
{
    public SiteTour Tour { get; set; } = null!;
    public List<SitePoint> Stops { get; set; } = new();
}

/// <summary>Tour tham quan khuôn viên (UC08 phía web) – dữ liệu từ Admin › Tour.</summary>
public class ToursSiteController : SiteControllerBase
{
    private readonly SiteContentService _content;
    public ToursSiteController(SiteContentService content) => _content = content;

    [HttpGet("/tour")]
    public async Task<IActionResult> Index() => View("~/Views/Site/Tours.cshtml", await _content.ToursAsync(BaseUrl));

    [HttpGet("/tour/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var t = (await _content.ToursAsync(BaseUrl)).FirstOrDefault(x => x.T.MaTour == id);
        if (t == null) { Response.StatusCode = 404; return View("~/Views/Site/NotFound.cshtml"); }
        var order = t.T.Diems.OrderBy(x => x.ThuTu).Select(x => x.MaDiem).ToList();
        var pts = await _content.PointsAsync(Lang, BaseUrl, order);
        var vm = new TourDetailVm
        {
            Tour = t,
            Stops = order.Select(i => pts.FirstOrDefault(p => p.P.Id == i)).Where(p => p != null).Select(p => p!).ToList(),
        };
        return View("~/Views/Site/TourDetail.cshtml", vm);
    }
}
