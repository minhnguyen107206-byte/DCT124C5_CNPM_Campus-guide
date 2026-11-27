using System.Security.Claims;
using CampusGuide.Web.Areas.Admin.Models;
using CampusGuide.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC12 – Đăng nhập, đăng xuất, đổi mật khẩu.</summary>
public class AccountController : AdminControllerBase
{
    private const int MaxFail = 5;
    private readonly AppDbContext _db;
    public AccountController(AppDbContext db) => _db = db;

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginVm { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var u = await _db.NguoiDungs.Include(x => x.VaiTro).FirstOrDefaultAsync(x => x.Email == vm.Email.Trim().ToLower());

        if (u != null && (u.TrangThai == "BiKhoa" || (u.KhoaDen.HasValue && u.KhoaDen > DateTime.UtcNow)))
        {
            ModelState.AddModelError("", u.TrangThai == "BiKhoa"
                ? "Tài khoản đã bị khóa. Liên hệ Quản trị viên."
                : $"Tài khoản tạm khóa do nhập sai nhiều lần. Thử lại sau {u.KhoaDen!.Value.ToLocalTime():HH:mm}.");
            return View(vm);
        }
        if (u == null || !BCrypt.Net.BCrypt.Verify(vm.MatKhau, u.MatKhauHash))
        {
            if (u != null)
            {
                u.SoLanSai++;
                if (u.SoLanSai >= MaxFail) { u.KhoaDen = DateTime.UtcNow.AddMinutes(15); u.SoLanSai = 0; }
                await _db.SaveChangesAsync();
            }
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng");
            return View(vm);
        }

        u.SoLanSai = 0; u.KhoaDen = null;
        await _db.SaveChangesAsync();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.MaND.ToString()),
            new(ClaimTypes.Name, u.HoTen),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.VaiTro!.MaCode),
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordVm());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var u = await _db.NguoiDungs.FindAsync(CurrentUserId);
        if (u == null) return RedirectToAction(nameof(Login));
        if (!BCrypt.Net.BCrypt.Verify(vm.MatKhauCu, u.MatKhauHash))
        {
            ModelState.AddModelError(nameof(vm.MatKhauCu), "Mật khẩu cũ không đúng");
            return View(vm);
        }
        u.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(vm.MatKhauMoi);
        await _db.SaveChangesAsync();
        Success("Đã đổi mật khẩu.");
        return RedirectToAction("Index", "Home");
    }

    /// <summary>A2 – Quên mật khẩu: bản đồ án chưa có máy chủ email, nên hướng dẫn liên hệ Quản trị viên đặt lại (UC21 – A3).</summary>
    [AllowAnonymous, HttpGet]
    public IActionResult Forgot() => View();

    [AllowAnonymous, HttpGet]
    public IActionResult Denied() => View();
}
