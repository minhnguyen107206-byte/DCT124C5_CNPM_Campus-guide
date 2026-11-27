using CampusGuide.Web.Data;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Controllers.Site;

/// <summary>Lớp cha của các trang khu người dùng (không cần đăng nhập): chọn ngôn ngữ (?lang= → cookie → trình duyệt → vi).</summary>
[AutoValidateAntiforgeryToken]
public abstract class SiteControllerBase : Controller
{
    protected AppDbContext Db => HttpContext.RequestServices.GetRequiredService<AppDbContext>();
    protected SiteContext Site => HttpContext.RequestServices.GetRequiredService<SiteContext>();
    protected string Lang => Site.Lang;
    protected string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var site = Site;
        site.Langs = await Db.NgonNgus.AsNoTracking().Where(n => n.KichHoat).OrderBy(n => n.ThuTu).ToListAsync();
        var codes = site.Langs.Select(l => l.MaNgonNgu).ToList();

        string? q = Request.Query["lang"];
        if (!string.IsNullOrWhiteSpace(q) && codes.Contains(q))
        {
            site.Lang = q;
            SaveLangCookie(q);
        }
        else if (Request.Cookies.TryGetValue(SiteContext.LangCookie, out var c) && codes.Contains(c)) site.Lang = c;
        else site.Lang = FromBrowser(codes);


        ViewData["Lang"] = site.Lang;
        await next();
    }

    protected void SaveLangCookie(string lang) =>
        Response.Cookies.Append(SiteContext.LangCookie, lang, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = false,
        });

    private string FromBrowser(List<string> active)
    {
        foreach (var raw in Request.Headers.AcceptLanguage.ToString().Split(','))
        {
            var tag = raw.Split(';')[0].Trim();
            if (tag.Length == 0) continue;
            var hit = active.FirstOrDefault(a => a.Equals(tag, StringComparison.OrdinalIgnoreCase))
                      ?? active.FirstOrDefault(a => a.Split('-')[0].Equals(tag.Split('-')[0], StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        return "vi";
    }

    protected void Toast(string msg) => TempData["toast"] = msg;
    protected void ToastError(string msg) => TempData["toastErr"] = msg;
}
