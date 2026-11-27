using CampusGuide.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Controllers.Site;

/// <summary>Tra cứu thuật ngữ, từ viết tắt, tên phòng ban (từ điển Admin › Thuật ngữ, có bản dịch theo ngôn ngữ).</summary>
public class GlossarySiteController : SiteControllerBase
{
    [HttpGet("/thuat-ngu")]
    public async Task<IActionResult> Index(string? q)
    {
        var list = await Db.ThuatNgus.AsNoTracking().Include(t => t.BanDichs).OrderBy(t => t.TuGoc).ToListAsync();
        var key = Api.AppApiController.Normalize(q ?? "");
        if (key.Length > 0)
            list = list.Where(t => Api.AppApiController.Normalize(t.TuGoc + " " + t.GhiChu + " " +
                                                                   string.Join(' ', t.BanDichs.Select(b => b.BanDich))).Contains(key)).ToList();
        ViewData["Q"] = q;
        return View("~/Views/Site/Glossary.cshtml", list);
    }
}
