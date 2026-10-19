using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Services;

public class StatsFilter
{
    public DateTime From { get; set; } = DateTime.UtcNow.Date.AddDays(-29);
    public DateTime To { get; set; } = DateTime.UtcNow.Date;
    public int? MaDiem { get; set; }
    public string? Lang { get; set; }
    public string? Source { get; set; }
}

public record NameCount(string Name, int Count);
public record PointRow(int Id, string Name, int Listens, int Qr, double? Rating, int RatingCount);

public class StatsResult
{
    public int TotalListens { get; set; }
    public int QrScans { get; set; }
    public int Devices { get; set; }
    public double? AvgRating { get; set; }
    public List<NameCount> ByDay { get; set; } = new();
    public List<NameCount> ByLang { get; set; } = new();
    public List<NameCount> BySource { get; set; } = new();
    public List<PointRow> TopPoints { get; set; } = new();
}

/// <summary>UC24 – Thống kê lượt nghe, quét QR, đánh giá; xuất Excel.</summary>
public class StatsService
{
    private readonly AppDbContext _db;
    public StatsService(AppDbContext db) => _db = db;

    public async Task<StatsResult> ComputeAsync(StatsFilter f)
    {
        var from = f.From.Date;
        var to = f.To.Date.AddDays(1);
        var q = _db.LuotNghes.AsNoTracking().Where(l => l.ThoiGian >= from && l.ThoiGian < to);
        if (f.MaDiem.HasValue) q = q.Where(l => l.MaDiem == f.MaDiem);
        if (!string.IsNullOrEmpty(f.Lang)) q = q.Where(l => l.MaNgonNgu == f.Lang);
        if (!string.IsNullOrEmpty(f.Source)) q = q.Where(l => l.Nguon == f.Source);

        // lấy về bộ nhớ: dữ liệu đồ án nhỏ, tránh khác biệt SQL giữa SQLite và PostgreSQL
        var rows = await q.Select(l => new { l.MaDiem, l.MaNgonNgu, l.Nguon, l.MaThietBi, l.ThoiGian }).ToListAsync();
        var names = await _db.Diems.AsNoTracking().ToDictionaryAsync(d => d.MaDiem, d => d.TenDiem);
        var rq = _db.DanhGias.AsNoTracking().Where(r => r.ThoiGian >= from && r.ThoiGian < to);
        if (f.MaDiem.HasValue) rq = rq.Where(r => r.MaDiem == f.MaDiem);   // đánh giá cũng theo bộ lọc điểm
        var ratings = await rq.Select(r => new { r.MaDiem, r.SoSao }).ToListAsync();

        var res = new StatsResult
        {
            TotalListens = rows.Count,
            QrScans = rows.Count(r => r.Nguon == NguonNghe.QR),
            Devices = rows.Select(r => r.MaThietBi).Distinct().Count(),
            AvgRating = ratings.Count > 0 ? Math.Round(ratings.Average(r => r.SoSao), 2) : null,
        };
        for (var d = from; d < to; d = d.AddDays(1))
            res.ByDay.Add(new NameCount(d.ToString("dd/MM"), rows.Count(r => r.ThoiGian.Date == d)));
        res.ByLang = rows.GroupBy(r => r.MaNgonNgu).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList();
        res.BySource = rows.GroupBy(r => r.Nguon).Select(g => new NameCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList();
        res.TopPoints = rows.GroupBy(r => r.MaDiem).Select(g =>
        {
            var rs = ratings.Where(x => x.MaDiem == g.Key).ToList();
            return new PointRow(g.Key, names.GetValueOrDefault(g.Key, $"#{g.Key}"), g.Count(),
                g.Count(x => x.Nguon == NguonNghe.QR), rs.Count > 0 ? Math.Round(rs.Average(x => x.SoSao), 1) : null, rs.Count);
        }).OrderByDescending(p => p.Listens).Take(10).ToList();
        return res;
    }

    public async Task<byte[]> ExportExcelAsync(StatsFilter f)
    {
        var s = await ComputeAsync(f);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Tong quan");
        ws.Cell(1, 1).Value = "BÁO CÁO THỐNG KÊ CAMPUSGUIDE";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Từ {f.From:dd/MM/yyyy} đến {f.To:dd/MM/yyyy}";
        ws.Cell(4, 1).Value = "Tổng lượt nghe"; ws.Cell(4, 2).Value = s.TotalListens;
        ws.Cell(5, 1).Value = "Lượt quét QR"; ws.Cell(5, 2).Value = s.QrScans;
        ws.Cell(6, 1).Value = "Số thiết bị"; ws.Cell(6, 2).Value = s.Devices;
        ws.Cell(7, 1).Value = "Điểm đánh giá trung bình"; if (s.AvgRating.HasValue) ws.Cell(7, 2).Value = s.AvgRating.Value; else ws.Cell(7, 2).Value = "-";
        ws.Columns().AdjustToContents();

        Sheet(wb, "Theo diem", new[] { "Điểm", "Lượt nghe", "Quét QR", "Đánh giá TB", "Số đánh giá" },
            s.TopPoints.Select(p => new object?[] { p.Name, p.Listens, p.Qr, p.Rating, p.RatingCount }));
        Sheet(wb, "Theo ngay", new[] { "Ngày", "Lượt nghe" }, s.ByDay.Select(d => new object?[] { d.Name, d.Count }));
        Sheet(wb, "Theo ngon ngu", new[] { "Ngôn ngữ", "Lượt nghe" }, s.ByLang.Select(d => new object?[] { d.Name, d.Count }));
        Sheet(wb, "Theo nguon", new[] { "Nguồn", "Lượt nghe" }, s.BySource.Select(d => new object?[] { d.Name, d.Count }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Sheet(XLWorkbook wb, string name, string[] headers, IEnumerable<object?[]> rows)
    {
        var ws = wb.Worksheets.Add(name);
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
            ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        }
        int r = 2;
        foreach (var row in rows)
        {
            for (int c = 0; c < row.Length; c++)
                ws.Cell(r, c + 1).Value = row[c] switch
                {
                    null => "",
                    int i => i,
                    double d => d,
                    _ => row[c]!.ToString(),
                };
            r++;
        }
        ws.Columns().AdjustToContents();
    }
}
