using System.Security.Claims;
using CampusGuide.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGuide.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "BienTap")]
[AutoValidateAntiforgeryToken]
public abstract class AdminControllerBase : Controller
{
    protected int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    protected bool IsQuanTri => User.IsInRole(VaiTroTen.QuanTriVien);
    protected string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    protected void Success(string msg) => TempData["ok"] = msg;
    protected void Fail(string msg) => TempData["err"] = msg;

    /// <summary>Form datetime-local gửi giờ địa phương; lưu UTC.</summary>
    protected static DateTime ToUtc(DateTime local) =>
        local.Kind == DateTimeKind.Utc ? local : DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
}

