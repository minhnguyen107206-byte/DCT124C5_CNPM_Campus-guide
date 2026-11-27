using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Controllers.Site;

public class PointListVm
{
    public List<SitePoint> Points { get; set; } = new();
    public List<LoaiDiem> Types { get; set; } = new();
    public int? Loai { get; set; }
    public string? Q { get; set; }
}

public class PointDetailVm
{
    public SitePoint Point { get; set; } = null!;
    public List<Video> Videos { get; set; } = new();
    public List<SiteTour> Tours { get; set; } = new();
    public List<SitePoint> Nearby { get; set; } = new();
    public bool FromQr { get; set; }
    public int? MyRating { get; set; }
}

/// <summary>Điểm tham quan, bản đồ, mã QR (UC02, UC03, UC05 phía web).</summary>
public class PointsSiteController : SiteControllerBase
{
    private readonly SiteContentService _content;
    private readonly QrService _qr;
    private readonly TranslationPipeline _pipeline;
    private readonly ILogger<PointsSiteController> _log;

    public PointsSiteController(SiteContentService content, QrService qr,
                                TranslationPipeline pipeline, ILogger<PointsSiteController> log)
    {
        _content = content; _qr = qr; _pipeline = pipeline; _log = log;
    }

    [HttpGet("/diem")]
    public async Task<IActionResult> Index(int? loai, string? q)
    {
        var all = await _content.PointsAsync(Lang, BaseUrl);
        var list = all.AsEnumerable();
        if (loai != null) list = list.Where(p => p.MaLoai == loai);
        var key = Api.AppApiController.Normalize(q ?? "");
        if (key.Length > 0)
            list = list.Where(p => Api.AppApiController.Normalize($"{p.P.Name} {p.P.Title} {p.P.Keywords} {p.P.Type}").Contains(key));
        var vm = new PointListVm
        {
            Points = list.OrderBy(p => p.P.Id).ToList(), Types = await _content.TypesAsync(), Loai = loai, Q = q,
        };
        return View("~/Views/Site/Points.cshtml", vm);
    }

    [HttpGet("/diem/{id:int}")]
    public async Task<IActionResult> Detail(int id, string? from)
    {
        var sp = await _content.PointAsync(id, Lang, BaseUrl);
        if (sp == null) return NotFound404();

        // Chưa có bản dịch cho ngôn ngữ này → dịch máy ngay (như trang quét QR), lưu vào hàng chờ duyệt của Admin
        if (Lang != "vi" && sp.P.Lang != Lang && !sp.Machine)
        {
            try
            {
                await _pipeline.TranslatePointAsync(id, new[] { Lang });
                sp = await _content.PointAsync(id, Lang, BaseUrl) ?? sp;
            }
            catch (Exception ex) { _log.LogWarning(ex, "Dịch điểm {Id} sang {Lang} lỗi", id, Lang); }
        }

        var vm = new PointDetailVm { Point = sp, Videos = await _content.VideosAsync(id), FromQr = from == "qr" };
        vm.Tours = (await _content.ToursAsync(BaseUrl)).Where(t => t.T.Diems.Any(x => x.MaDiem == id)).ToList();
        var others = await _content.PointsAsync(Lang, BaseUrl);
        vm.Nearby = others.Where(o => o.P.Id != id)
            .OrderBy(o => Distance(sp.P.Lat, sp.P.Lng, o.P.Lat, o.P.Lng)).Take(4).ToList();

        if (DeviceId(false) is string dev)
            vm.MyRating = await Db.DanhGias.AsNoTracking().Where(r => r.MaThietBi == dev && r.MaDiem == id)
                .Select(r => (int?)r.SoSao).FirstOrDefaultAsync();
        return View("~/Views/Site/PointDetail.cshtml", vm);
    }

    /// <summary>Mở điểm theo mã QR (máy quét trên trang /quet, hoặc liên kết /diem/qr/CG0001).</summary>
    [HttpGet("/diem/qr/{code}")]
    public async Task<IActionResult> ByQr(string code)
    {
        code = (code ?? "").Trim().TrimEnd('/').Split('/').Last().Split('?')[0];
        var d = await Db.Diems.AsNoTracking().FirstOrDefaultAsync(x => x.MaQR == code && x.TrangThai == TrangThaiDiem.DaXuatBan);
        if (d == null) { ToastError(Site.T("qr_invalid")); return Redirect("/quet"); }
        return Redirect($"/diem/{d.MaDiem}?from=qr");
    }

    /// <summary>Ảnh mã QR của điểm (cùng nội dung với mã in ở Admin › Điểm › QR).</summary>
    [HttpGet("/diem/{id:int}/qr.png")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> QrPng(int id)
    {
        var code = await Db.Diems.AsNoTracking()
            .Where(x => x.MaDiem == id && x.TrangThai == TrangThaiDiem.DaXuatBan).Select(x => x.MaQR).FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(code)) return NotFound();
        return File(_qr.Png(code, 8), "image/png");
    }

    [HttpGet("/ban-do")]
    public async Task<IActionResult> Map(int? loai)
    {
        var vm = new PointListVm
        {
            Points = await _content.PointsAsync(Lang, BaseUrl), Types = await _content.TypesAsync(), Loai = loai,
        };
        return View("~/Views/Site/Map.cshtml", vm);
    }

    /// <summary>Trang "Scan QR": máy quét QR bằng camera + mã QR có sẵn của trang web (quét bằng điện thoại là mở CampusGuide).</summary>
    [HttpGet("/quet")]
    public IActionResult Scan()
    {
        ViewData["SiteLink"] = SiteLink;
        return View("~/Views/Site/Scan.cshtml");
    }

    [HttpPost("/quet")]
    public IActionResult ScanManual(string code) =>
        string.IsNullOrWhiteSpace(code) ? Redirect("/quet") : Redirect("/diem/qr/" + Uri.EscapeDataString(code.Trim()));

    /// <summary>Ảnh mã QR trỏ về Trang chủ (cùng địa chỉ với QR các điểm: PublicBaseUrl → tunnel → IP Wi-Fi).</summary>
    [HttpGet("/qr.png")]
    public IActionResult SiteQrPng()
    {
        using var gen = new QRCoder.QRCodeGenerator();
        using var data = gen.CreateQrCode(SiteLink, QRCoder.QRCodeGenerator.ECCLevel.M);
        Response.Headers.CacheControl = "no-store";   // link tunnel đổi mỗi lần chạy
        return File(new QRCoder.PngByteQRCode(data).GetGraphic(12), "image/png");
    }

    private string SiteLink => _qr.BaseUrl().TrimEnd('/') + "/";

    /// <summary>Khách đánh giá điểm, không cần đăng nhập (dùng chung bảng DanhGia với app;
    /// mỗi trình duyệt có một mã thiết bị "web-..." lưu trong cookie nên đánh giá lại sẽ ghi đè).</summary>
    [HttpPost("/diem/{id:int}/danh-gia")]
    public async Task<IActionResult> Rate(int id, int stars, string? comment)
    {
        if (stars is < 1 or > 5 || !await Db.Diems.AnyAsync(d => d.MaDiem == id)) return Redirect($"/diem/{id}");
        var dev = DeviceId(true)!;
        if (!await Db.ThietBis.AnyAsync(t => t.MaThietBi == dev))
            Db.ThietBis.Add(new ThietBi { MaThietBi = dev, NenTang = "web", MaNgonNgu = Lang });
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (comment?.Length > 500) comment = comment[..500];
        var old = await Db.DanhGias.FirstOrDefaultAsync(x => x.MaThietBi == dev && x.MaDiem == id);
        if (old == null) Db.DanhGias.Add(new DanhGia { MaThietBi = dev, MaDiem = id, SoSao = stars, NhanXet = comment });
        else { old.SoSao = stars; old.NhanXet = comment; old.ThoiGian = DateTime.UtcNow; }
        await Db.SaveChangesAsync();
        Toast(Site.T("rate_thanks"));
        return Redirect($"/diem/{id}#danh-gia");
    }

    /// <summary>Mã thiết bị ẩn danh của trình duyệt (cookie cg_dev), tạo mới khi create = true.</summary>
    private string? DeviceId(bool create)
    {
        if (Request.Cookies.TryGetValue("cg_dev", out var v) && v.StartsWith("web-") && v.Length <= 36) return v;
        if (!create) return null;
        v = "web-" + Guid.NewGuid().ToString("N")[..28];
        Response.Cookies.Append("cg_dev", v, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(2), IsEssential = true, HttpOnly = true, SameSite = SameSiteMode.Lax,
        });
        return v;
    }

    private IActionResult NotFound404()
    {
        Response.StatusCode = 404;
        return View("~/Views/Site/NotFound.cshtml");
    }

    private static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        double dx = (lng2 - lng1) * Math.Cos(lat1 * Math.PI / 180), dy = lat2 - lat1;
        return dx * dx + dy * dy;
    }
}
