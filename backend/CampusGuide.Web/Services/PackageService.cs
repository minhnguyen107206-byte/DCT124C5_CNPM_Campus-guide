using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Services;

public record ReadinessRow(string Lang, string LangName, int Total, int Ready, List<string> Missing);

/// <summary>UC19 – Xuất bản gói offline: tour.json + ảnh + audio của các điểm, nén zip, tính SHA-256.</summary>
public class PackageService
{
    private readonly AppDbContext _db;
    private readonly IStorageService _store;
    private readonly ContentResolver _resolver;

    public PackageService(AppDbContext db, IStorageService store, ContentResolver resolver)
    {
        _db = db; _store = store; _resolver = resolver;
    }

    private Task<Tour?> LoadTour(int maTour) => _db.Tours
        .Include(t => t.Diems).ThenInclude(td => td.Diem!).ThenInclude(d => d.NoiDungs).ThenInclude(n => n.Audios)
        .Include(t => t.Diems).ThenInclude(td => td.Diem!).ThenInclude(d => d.LoaiDiem)
        .FirstOrDefaultAsync(t => t.MaTour == maTour);

    public async Task<List<ReadinessRow>> ReadinessAsync(int maTour)
    {
        var tour = await LoadTour(maTour) ?? throw new KeyNotFoundException();
        var langs = await _db.NgonNgus.Where(n => n.KichHoat).OrderBy(n => n.ThuTu).ToListAsync();
        var rows = new List<ReadinessRow>();
        foreach (var l in langs)
        {
            var missing = tour.Diems.Select(td => td.Diem!)
                .Where(d => !d.NoiDungs.Any(n => n.MaNgonNgu == l.MaNgonNgu && n.Audios.Any() &&
                    (n.TrangThai == TrangThaiNoiDung.DaDuyet || n.TrangThai == TrangThaiNoiDung.Goc)))
                .Select(d => d.TenDiem).ToList();
            rows.Add(new ReadinessRow(l.MaNgonNgu, l.TenNgonNgu, tour.Diems.Count, tour.Diems.Count - missing.Count, missing));
        }
        return rows;
    }

    /// <param name="allowFallback">true: điểm thiếu bản dịch dùng ngôn ngữ dự phòng (E1)</param>
    public async Task<GoiOffline> PublishAsync(int maTour, string lang, bool allowFallback, string baseUrl)
    {
        var tour = await LoadTour(maTour) ?? throw new KeyNotFoundException();
        if (tour.TrangThai != TrangThaiDiem.DaXuatBan) throw new InvalidOperationException("Tour chưa được xuất bản (UC18).");

        var ready = (await ReadinessAsync(maTour)).FirstOrDefault(r => r.Lang == lang)
            ?? throw new InvalidOperationException($"Ngôn ngữ '{lang}' không tồn tại hoặc đang tắt.");
        if (ready.Missing.Count > 0 && !allowFallback)
            throw new InvalidOperationException("Thiếu bản dịch đã duyệt hoặc audio: " + string.Join(", ", ready.Missing));

        var fallback = await _resolver.FallbackLangAsync();
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var points = new List<object>();
            foreach (var td in tour.Diems.OrderBy(x => x.ThuTu))
            {
                var dto = _resolver.ToDto(td.Diem!, lang, fallback, "");
                string? audioFile = null, imageFile = null;
                if (dto.AudioUrl != null)
                {
                    var bytes = await _store.ReadAsync(dto.AudioUrl);
                    if (bytes != null)
                    {
                        audioFile = $"audio/{Path.GetFileName(dto.AudioUrl)}";
                        await WriteEntry(zip, audioFile, bytes);
                    }
                }
                if (dto.Image != null)
                {
                    var bytes = await _store.ReadAsync(dto.Image);
                    if (bytes != null)
                    {
                        imageFile = $"images/{Path.GetFileName(dto.Image)}";
                        await WriteEntry(zip, imageFile, bytes);
                    }
                }
                points.Add(new { order = td.ThuTu, point = dto with { AudioUrl = audioFile, Image = imageFile } });
            }
            var manifest = new
            {
                tourId = tour.MaTour, name = tour.TenTour, description = tour.MoTa, lang,
                distance = tour.QuangDuong, minutes = tour.ThoiLuong, createdAt = DateTime.UtcNow, points,
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            await WriteEntry(zip, "tour.json", json);
        }
        var data = ms.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

        var version = (await _db.GoiOfflines.Where(g => g.MaTour == maTour && g.MaNgonNgu == lang)
            .MaxAsync(g => (int?)g.PhienBan) ?? 0) + 1;
        var url = await _store.SaveAsync(data, "packages", $"tour{maTour}_{lang}_v{version}.zip");
        var goi = new GoiOffline
        {
            MaTour = maTour, MaNgonNgu = lang, PhienBan = version, DungLuong = data.LongLength,
            Checksum = checksum, DuongDan = url,
        };
        _db.GoiOfflines.Add(goi);
        tour.CanXuatBanLai = false;

        // chỉ giữ 2 phiên bản gần nhất
        var old = await _db.GoiOfflines.Where(g => g.MaTour == maTour && g.MaNgonNgu == lang && g.PhienBan <= version - 2).ToListAsync();
        foreach (var o in old) { _store.Delete(o.DuongDan); _db.GoiOfflines.Remove(o); }
        await _db.SaveChangesAsync();
        return goi;
    }

    private static async Task WriteEntry(ZipArchive zip, string name, byte[] data)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var s = e.Open();
        await s.WriteAsync(data);
    }
}
