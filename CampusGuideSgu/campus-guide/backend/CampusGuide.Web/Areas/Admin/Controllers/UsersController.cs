using System.Security.Cryptography;
using CampusGuide.Web.Areas.Admin.Models;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>UC21 – Quản lý tài khoản và phân quyền (chỉ Quản trị viên).</summary>
[Authorize(Policy = "QuanTri")]
public class UsersController : AdminControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewBag.Roles = await _db.VaiTros.ToListAsync();
        return View(await _db.NguoiDungs.AsNoTracking().Include(u => u.VaiTro).OrderBy(u => u.HoTen).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadRoles();
        return View("Edit", new UserVm { MaVaiTro = (await _db.VaiTros.FirstAsync(v => v.MaCode == VaiTroTen.BienTapVien)).MaVaiTro });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var u = await _db.NguoiDungs.FindAsync(id);
        if (u == null) return NotFound();
        await LoadRoles();
        return View(new UserVm { MaND = u.MaND, HoTen = u.HoTen, Email = u.Email, MaVaiTro = u.MaVaiTro });
    }

    [HttpPost]
    public async Task<IActionResult> Save(UserVm vm)
    {
        var email = vm.Email.Trim().ToLowerInvariant();
        if (await _db.NguoiDungs.AnyAsync(u => u.Email == email && u.MaND != vm.MaND))
            ModelState.AddModelError(nameof(vm.Email), "Email đã được sử dụng (E1)");
        var qtvRole = await _db.VaiTros.FirstAsync(v => v.MaCode == VaiTroTen.QuanTriVien);
        if (vm.MaND != 0 && vm.MaVaiTro != qtvRole.MaVaiTro && await IsLastActiveAdmin(vm.MaND))
            ModelState.AddModelError(nameof(vm.MaVaiTro), "Không thể hạ vai trò Quản trị viên cuối cùng (E3)");
        if (!ModelState.IsValid) { await LoadRoles(); return View("Edit", vm); }

        if (vm.MaND == 0)
        {
            var temp = TempPassword();
            _db.NguoiDungs.Add(new NguoiDung { HoTen = vm.HoTen.Trim(), Email = email, MaVaiTro = vm.MaVaiTro, MatKhauHash = BCrypt.Net.BCrypt.HashPassword(temp) });
            await _db.SaveChangesAsync();
            // Bản đồ án chưa có máy chủ email: hiển thị mật khẩu tạm để QTV gửi cho người dùng
            Success($"Đã tạo tài khoản {email}. Mật khẩu tạm: {temp} (yêu cầu người dùng đổi sau khi đăng nhập).");
        }
        else
        {
            var u = await _db.NguoiDungs.FindAsync(vm.MaND) ?? throw new KeyNotFoundException();
            u.HoTen = vm.HoTen.Trim(); u.Email = email; u.MaVaiTro = vm.MaVaiTro;
            await _db.SaveChangesAsync();
            Success("Đã cập nhật tài khoản. Quyền mới có hiệu lực từ lần đăng nhập kế tiếp.");
        }
        return RedirectToAction(nameof(Index));
    }

    // A2: khóa / mở khóa
    [HttpPost]
    public async Task<IActionResult> ToggleLock(int id)
    {
        var u = await _db.NguoiDungs.FindAsync(id);
        if (u == null) return NotFound();
        if (u.TrangThai == "HoatDong")
        {
            if (id == CurrentUserId || await IsLastActiveAdmin(id)) { Fail("Không thể tự khóa mình hoặc khóa Quản trị viên cuối cùng (E3)."); return RedirectToAction(nameof(Index)); }
            u.TrangThai = "BiKhoa";
        }
        else { u.TrangThai = "HoatDong"; u.SoLanSai = 0; u.KhoaDen = null; }
        await _db.SaveChangesAsync();
        Success(u.TrangThai == "BiKhoa" ? $"Đã khóa {u.Email}." : $"Đã mở khóa {u.Email}.");
        return RedirectToAction(nameof(Index));
    }

    // A3: đặt lại mật khẩu
    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var u = await _db.NguoiDungs.FindAsync(id);
        if (u == null) return NotFound();
        var temp = TempPassword();
        u.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(temp); u.SoLanSai = 0; u.KhoaDen = null;
        await _db.SaveChangesAsync();
        Success($"Mật khẩu tạm mới của {u.Email}: {temp}");
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsLastActiveAdmin(int maND)
    {
        var admins = await _db.NguoiDungs.Include(u => u.VaiTro)
            .Where(u => u.VaiTro!.MaCode == VaiTroTen.QuanTriVien && u.TrangThai == "HoatDong").Select(u => u.MaND).ToListAsync();
        return admins.Count == 1 && admins[0] == maND;
    }

    private static string TempPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        static string pick(string set, int n) => new(Enumerable.Range(0, n).Select(_ => set[RandomNumberGenerator.GetInt32(set.Length)]).ToArray());
        return pick(chars, 6) + pick(digits, 3);
    }

    private async Task LoadRoles() => ViewBag.Roles = new SelectList(await _db.VaiTros.ToListAsync(), "MaVaiTro", "TenVaiTro");
}
