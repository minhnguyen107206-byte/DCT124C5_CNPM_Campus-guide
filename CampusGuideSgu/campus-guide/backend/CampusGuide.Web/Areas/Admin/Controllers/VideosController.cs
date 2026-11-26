using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>Video giới thiệu và buổi phát trực tiếp hiển thị ở trang người dùng (/truc-tiep, trang chi tiết điểm).</summary>
public class VideosController : AdminControllerBase
{
    private readonly AppDbContext _db;
    public VideosController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Videos.AsNoTracking().Include(v => v.Diem)
            .OrderByDescending(v => v.LaTrucTiep).ThenBy(v => v.ThuTu).ThenByDescending(v => v.NgayTao).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PointsAsync();
        return View("Edit", new Video());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var v = await _db.Videos.FindAsync(id);
        if (v == null) return NotFound();
        if (v.BatDau != null) v.BatDau = v.BatDau.Value.ToLocalTime();
        await PointsAsync();
        return View(v);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Video vm)
    {
        vm.DuongDan = (vm.DuongDan ?? "").Trim();
        if (!Uri.TryCreate(vm.DuongDan, UriKind.Absolute, out var u) || (u.Scheme != "http" && u.Scheme != "https"))
            ModelState.AddModelError(nameof(vm.DuongDan), "Đường dẫn phải là liên kết http(s) – YouTube, Facebook hoặc file .mp4");
        ModelState.Remove(nameof(Video.Diem));
        if (!ModelState.IsValid) { await PointsAsync(); return View("Edit", vm); }

        Video v;
        if (vm.MaVideo == 0) { v = new Video(); _db.Videos.Add(v); }
        else v = await _db.Videos.FindAsync(vm.MaVideo) ?? throw new InvalidOperationException();
        v.TieuDe = vm.TieuDe.Trim(); v.MoTa = vm.MoTa?.Trim(); v.DuongDan = vm.DuongDan; v.LaTrucTiep = vm.LaTrucTiep;
        v.MaDiem = vm.MaDiem; v.HienThi = vm.HienThi; v.ThuTu = vm.ThuTu;
        v.BatDau = vm.BatDau == null ? null : ToUtc(vm.BatDau.Value);
        await _db.SaveChangesAsync();
        Success("Đã lưu video.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var v = await _db.Videos.FindAsync(id);
        if (v != null) { _db.Videos.Remove(v); await _db.SaveChangesAsync(); Success("Đã xoá video."); }
        return RedirectToAction(nameof(Index));
    }

    private async Task PointsAsync() =>
        ViewBag.Points = await _db.Diems.AsNoTracking().Where(d => d.TrangThai != TrangThaiDiem.DaAn)
            .OrderBy(d => d.TenDiem).Select(d => new { d.MaDiem, d.TenDiem }).ToListAsync();
}
