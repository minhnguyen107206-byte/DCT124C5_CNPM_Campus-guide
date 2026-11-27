using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CampusGuide.Web.Services;

/// <summary>UC14 – Sinh mã QR. Nội dung QR là liên kết {địa chỉ máy chủ}/q/{MaQR}: app đọc phần mã cuối;
/// điện thoại chưa cài app sẽ mở trang nghe thuyết minh trên trình duyệt (QrLandingController).
/// Địa chỉ máy chủ: PublicBaseUrl nếu đã cấu hình (không phải localhost); nếu không thì tự lấy địa chỉ IP
/// Wi-Fi/LAN của máy đang chạy backend – vì "localhost" trong mã QR sẽ trỏ vào chính điện thoại nên không mở được.</summary>
public class QrService
{
    private readonly AppDbContext _db;
    private readonly string _configured;
    private readonly IHttpContextAccessor _http;

    public QrService(AppDbContext db, IConfiguration cfg, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
        _configured = (cfg["PublicBaseUrl"] ?? "").Trim().TrimEnd('/');
    }

    /// <summary>Địa chỉ gốc dùng trong mã QR, VD http://192.168.1.10:5000.</summary>
    public string BaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_configured) && !IsLocal(_configured)) return _configured;
        if (PublicTunnelService.Url != null) return PublicTunnelService.Url;

        var req = _http.HttpContext?.Request;
        // Admin đang mở bằng IP LAN (VD http://192.168.1.10:5000/admin) → dùng luôn địa chỉ đó
        if (req != null && !IsLocalHost(req.Host.Host)) return $"{req.Scheme}://{req.Host}";

        var port = req?.Host.Port
                   ?? (Uri.TryCreate(_configured, UriKind.Absolute, out var u) ? u.Port : 5000);
        var ip = LanIPv4();
        if (ip != null) return $"http://{ip}:{port}";
        return string.IsNullOrWhiteSpace(_configured) ? $"http://localhost:{port}" : _configured;
    }

    /// <summary>false khi mã QR vẫn chứa localhost (máy không có mạng LAN) – điện thoại sẽ không mở được.</summary>
    public bool ReachableFromPhones => !IsLocal(BaseUrl());

    public string LinkFor(string code) => $"{BaseUrl()}/q/{code}";

    public async Task<string> EnsureCodeAsync(Diem d, bool regenerate = false)
    {
        if (d.TrangThai == TrangThaiDiem.DaAn) throw new InvalidOperationException("Điểm đã xoá, không thể sinh mã QR.");
        if (!string.IsNullOrEmpty(d.MaQR) && !regenerate) return d.MaQR;
        string code;
        do { code = "CG" + Convert.ToHexString(Guid.NewGuid().ToByteArray())[..8]; }
        while (await _db.Diems.AnyAsync(x => x.MaQR == code));
        d.MaQR = code;
        await _db.SaveChangesAsync();
        return code;
    }

    public byte[] Png(string code, int pixelsPerModule = 12)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(LinkFor(code), QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    private static bool IsLocal(string url) =>
        !Uri.TryCreate(url, UriKind.Absolute, out var u) || IsLocalHost(u.Host);

    private static bool IsLocalHost(string host) =>
        host is "localhost" or "127.0.0.1" or "::1" or "[::1]" or "0.0.0.0" or "";

    /// <summary>IPv4 trong mạng LAN của máy: ưu tiên card mạng có gateway (Wi-Fi/Ethernet thật), bỏ card ảo, loopback.</summary>
    public static string? LanIPv4()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Select(n => new
                {
                    Props = n.GetIPProperties(),
                    Virtual = n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                              || n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                              || n.Name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase)
                              || n.Name.StartsWith("docker", StringComparison.OrdinalIgnoreCase)
                              || n.Name.StartsWith("bridge", StringComparison.OrdinalIgnoreCase)
                              || n.Name.StartsWith("utun", StringComparison.OrdinalIgnoreCase),
                })
                .SelectMany(n => n.Props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                    .Select(a => new
                    {
                        Ip = a.Address.ToString(),
                        n.Virtual,
                        HasGateway = n.Props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                                                       && !g.Address.Equals(IPAddress.Any)),
                    }))
                .Where(x => !x.Ip.StartsWith("169.254."))
                .OrderBy(x => x.Virtual)
                .ThenByDescending(x => x.HasGateway)
                .ThenByDescending(x => x.Ip.StartsWith("192.168.") || x.Ip.StartsWith("10.") || x.Ip.StartsWith("172."))
                .ToList();
            return candidates.FirstOrDefault()?.Ip;
        }
        catch
        {
            return null;
        }
    }
}
