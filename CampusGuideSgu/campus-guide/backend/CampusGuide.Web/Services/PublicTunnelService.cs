using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CampusGuide.Web.Services;

/// <summary>Mở "đường hầm" Cloudflare (cloudflared, miễn phí, không cần tài khoản) để điện thoại ở mạng khác
/// (4G, Wi-Fi khác) vẫn mở được link trong mã QR. Bật bằng "Tunnel:Enabled": true trong appsettings.json.
/// Chưa có cloudflared thì tự tải cloudflared.exe về thư mục tools/. Link https://xxx.trycloudflare.com
/// đổi mỗi lần chạy lại backend → phải in lại mã QR.</summary>
public partial class PublicTunnelService : BackgroundService
{
    /// <summary>Link công khai hiện tại (null khi tunnel chưa sẵn sàng hoặc đang tắt).</summary>
    public static string? Url { get; private set; }
    /// <summary>Trạng thái để hiện trên trang mã QR.</summary>
    public static string Status { get; private set; } = "Tunnel đang tắt.";
    public static bool Enabled { get; private set; }

    private const string DownloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";

    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PublicTunnelService> _log;
    private Process? _proc;

    public PublicTunnelService(IConfiguration cfg, IHttpClientFactory httpFactory, IWebHostEnvironment env, ILogger<PublicTunnelService> log)
    {
        _cfg = cfg; _httpFactory = httpFactory; _env = env; _log = log;
    }

    [GeneratedRegex(@"https://(?!api\.)[a-z0-9-]+\.trycloudflare\.com")]
    private static partial Regex UrlRegex();

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        Enabled = string.Equals(_cfg["Tunnel:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        if (!Enabled) return;

        var port = int.TryParse(_cfg["Tunnel:Port"], out var p) ? p : 5000;
        try
        {
            Status = "Đang mở tunnel...";
            var exe = await FindOrDownloadAsync(stop);
            if (exe == null) return;

            var psi = new ProcessStartInfo(exe, $"tunnel --no-autoupdate --url http://localhost:{port}")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            DataReceivedEventHandler onLine = (_, e) =>
            {
                if (e.Data == null || Url != null) return;
                if (e.Data.Contains("failed to", StringComparison.OrdinalIgnoreCase))
                    Status = "Không mở được tunnel (kiểm tra mạng internet): " + e.Data;
                var m = UrlRegex().Match(e.Data);
                if (!m.Success) return;
                Url = m.Value;
                Status = "Tunnel đang chạy: điện thoại ở mạng nào cũng quét được.";
                _log.LogWarning("Link công khai cho mã QR: {Url}", Url);
            };
            _proc.OutputDataReceived += onLine;
            _proc.ErrorDataReceived += onLine;
            _proc.Exited += (_, _) => { Url = null; if (!Status.StartsWith("Không")) Status = "Tunnel đã dừng – chạy lại backend để mở lại."; };
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            await Task.Delay(Timeout.Infinite, stop);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Status = "Không mở được tunnel: " + ex.Message;
            _log.LogError(ex, "Không mở được Cloudflare tunnel");
        }
    }

    private async Task<string?> FindOrDownloadAsync(CancellationToken stop)
    {
        var configured = _cfg["Tunnel:CloudflaredPath"];
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            configured,
            Path.Combine(_env.ContentRootPath, "tools", "cloudflared.exe"),
            Path.Combine(local, "Microsoft", "WinGet", "Links", "cloudflared.exe"),
            @"C:\Program Files (x86)\cloudflared\cloudflared.exe",
            @"C:\Program Files\cloudflared\cloudflared.exe",
        };
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) return c;

        // Có trong PATH (VD đã cài bằng winget và mở lại máy)
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var f = Path.Combine(dir.Trim(), OperatingSystem.IsWindows() ? "cloudflared.exe" : "cloudflared");
            if (File.Exists(f)) return f;
        }

        if (!OperatingSystem.IsWindows())
        {
            Status = "Chưa có cloudflared – cài cloudflared rồi chạy lại.";
            return null;
        }

        Status = "Đang tải cloudflared.exe (lần đầu, khoảng 60 MB)...";
        _log.LogWarning("Đang tải cloudflared.exe về thư mục tools/ ...");
        var target = Path.Combine(_env.ContentRootPath, "tools", "cloudflared.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(10);
        using (var res = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, stop))
        {
            res.EnsureSuccessStatusCode();
            await using var fs = File.Create(target + ".tmp");
            await res.Content.CopyToAsync(fs, stop);
        }
        File.Move(target + ".tmp", target, overwrite: true);
        return target;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
        Url = null;
        await base.StopAsync(ct);
    }
}
