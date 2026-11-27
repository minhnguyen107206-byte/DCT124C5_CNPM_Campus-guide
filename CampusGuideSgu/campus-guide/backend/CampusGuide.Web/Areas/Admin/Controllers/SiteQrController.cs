using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>Mã QR của trang web người dùng: in ra dán ở cổng trường, quầy lễ tân, tờ rơi…
/// Khách quét bằng camera điện thoại là mở thẳng Trang chủ (không cần cài app, không cần đăng nhập).
/// Địa chỉ dùng chung với mã QR của các điểm (QrService.BaseUrl: PublicBaseUrl → link tunnel → IP LAN).</summary>
public class SiteQrController : AdminControllerBase
{
    private readonly QrService _qr;
    public SiteQrController(QrService qr) => _qr = qr;

    private string Link => _qr.BaseUrl().TrimEnd('/') + "/";

    public IActionResult Index()
    {
        ViewBag.Link = Link;
        ViewBag.Reachable = _qr.ReachableFromPhones;
        ViewBag.Tunnel = PublicTunnelService.Url != null;
        return View();
    }

    [HttpGet]
    public IActionResult Png(bool download = false)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(Link, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(14);
        Response.Headers.CacheControl = "no-store";   // link tunnel đổi mỗi lần chạy → luôn sinh mới
        return download ? File(png, "image/png", "QR_CampusGuide_TrangChu.png") : File(png, "image/png");
    }
}
