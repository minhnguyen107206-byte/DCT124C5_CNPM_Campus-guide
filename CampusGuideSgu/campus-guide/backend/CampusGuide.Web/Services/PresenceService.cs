using System.Collections.Concurrent;

namespace CampusGuide.Web.Services;

/// <summary>Một thiết bị đang mở trang nghe thuyết minh (web sau khi quét QR) hoặc đang mở app.</summary>
public class PresenceSession
{
    public string Id { get; init; } = "";
    public string PageId { get; set; } = "";
    public string Source { get; set; } = "web";
    public string Platform { get; set; } = "";
    public int? PointId { get; set; }
    public string? PointName { get; set; }
    public string? Lang { get; set; }
    public bool Listening { get; set; }
    public DateTime FirstSeen { get; init; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    // ---- ghi nhật ký vào CSDL (PresenceController) ----
    public long? MaLuotTruyCap { get; set; }
    public int? DiemCuaLuot { get; set; }
    public DateTime BatDauLuot { get; set; } = DateTime.UtcNow;
    public long? MaLuotNghe { get; set; }
    public DateTime BatDauNghe { get; set; }
}

public record LivePoint(int? PointId, string Name, int TruyCap, int DangNghe);
public record LiveDevice(string Id, string Source, string Platform, string? Point, string? Lang, bool Listening, int GiayTruoc, int PhutOnline);
public record LiveSnapshot(
    int DangTruyCap, int DangNghe,
    int ThatTruyCap, int ThatNghe,
    int HeSo, int HetHanGiay,
    List<LivePoint> TheoDiem, Dictionary<string, int> TheoNgonNgu, Dictionary<string, int> TheoNguon,
    List<LiveDevice> ThietBi, DateTime ThoiDiem);

/// <summary>Đếm thiết bị đang truy cập / đang nghe thuyết minh theo thời gian thực (dashboard "Trực tiếp").</summary>
public class PresenceService
{
    /// <summary>Hệ số nhân cho số hiển thị trên dashboard. Thầy bảo "nhân 2" → đổi 1 thành 2.</summary>
    public const int HeSoHienThi = 1;

    /// <summary>Quá thời gian này không nhận được ping thì coi như thiết bị đã rời đi.</summary>
    public static readonly TimeSpan HetHan = TimeSpan.FromSeconds(15);

    public static int HienThi(int soThat) => soThat * HeSoHienThi;

    private readonly ConcurrentDictionary<string, PresenceSession> _sessions = new();

    /// <summary>Phiên đang online theo mã (để controller ghi nhật ký vào CSDL).</summary>
    public PresenceSession? Get(string id) => _sessions.TryGetValue(id.Length > 64 ? id[..64] : id, out var s) ? s : null;

    public void Ping(string id, string pageId, string source, string platform, int? pointId, string? pointName, string? lang, bool listening)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        id = id.Length > 64 ? id[..64] : id;
        var s = _sessions.GetOrAdd(id, key => new PresenceSession { Id = key });
        lock (s)
        {
            s.PageId = pageId;
            s.Source = source;
            s.Platform = platform;
            s.PointId = pointId;
            s.PointName = pointName;
            s.Lang = lang;
            s.Listening = listening;
            s.LastSeen = DateTime.UtcNow;
        }
    }

    /// <summary>Thiết bị rời đi. Chỉ xoá nếu đúng lần mở trang hiện tại (pageId).</summary>
    public void Leave(string id, string? pageId)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (_sessions.TryGetValue(id, out var s) && (string.IsNullOrEmpty(pageId) || s.PageId == pageId))
            _sessions.TryRemove(id, out _);
    }

    public LiveSnapshot Snapshot()
    {
        var now = DateTime.UtcNow;
        foreach (var (key, s) in _sessions)
            if (now - s.LastSeen > HetHan) _sessions.TryRemove(key, out _);   // quá hạn = đã rời

        var online = _sessions.Values.ToList();
        int thatTruyCap = online.Count;
        int thatNghe = online.Count(s => s.Listening);

        var theoDiem = online.Where(s => s.PointId != null)
            .GroupBy(s => new { s.PointId, s.PointName })
            .Select(g => new LivePoint(g.Key.PointId, g.Key.PointName ?? $"Điểm #{g.Key.PointId}",
                HienThi(g.Count()), HienThi(g.Count(x => x.Listening))))
            .OrderByDescending(p => p.TruyCap).ThenBy(p => p.Name).ToList();
        var theoNgonNgu = online.GroupBy(s => s.Lang ?? "?").ToDictionary(g => g.Key, g => HienThi(g.Count()));
        var theoNguon = online.GroupBy(s => s.Source).ToDictionary(g => g.Key, g => HienThi(g.Count()));
        var thietBi = online.OrderByDescending(s => s.Listening).ThenBy(s => s.FirstSeen)
            .Select(s => new LiveDevice(s.Id.Length > 6 ? s.Id[..6] : s.Id, s.Source, s.Platform, s.PointName, s.Lang, s.Listening,
                (int)(now - s.LastSeen).TotalSeconds, (int)(now - s.FirstSeen).TotalMinutes))
            .ToList();

        return new LiveSnapshot(
            HienThi(thatTruyCap), HienThi(thatNghe), thatTruyCap, thatNghe,
            HeSoHienThi, (int)HetHan.TotalSeconds,
            theoDiem, theoNgonNgu, theoNguon, thietBi, now);
    }

    /// <summary>Đoán loại thiết bị từ User-Agent để hiện trên dashboard.</summary>
    public static string PlatformFrom(string? userAgent)
    {
        var ua = userAgent ?? "";
        if (ua.Contains("iPhone")) return "iPhone";
        if (ua.Contains("iPad")) return "iPad";
        if (ua.Contains("Android")) return "Android";
        if (ua.Contains("Macintosh")) return "Mac";
        if (ua.Contains("Windows")) return "Windows";
        if (ua.Contains("Linux")) return "Linux";
        return "Khác";
    }
}