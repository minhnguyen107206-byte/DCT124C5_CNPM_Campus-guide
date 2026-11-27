using System.Text.RegularExpressions;

namespace CampusGuide.Web.Services;

/// <summary>Nhận dạng đường dẫn video để nhúng: YouTube (video thường / live), Facebook, file .mp4/.webm.</summary>
public static partial class VideoEmbed
{
    [GeneratedRegex(@"(?:youtube\.com/(?:watch\?(?:.*&)?v=|embed/|live/|shorts/)|youtu\.be/)([A-Za-z0-9_-]{11})")]
    private static partial Regex YouTubeRx();

    public static string? YouTubeId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = YouTubeRx().Match(url);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static bool IsFile(string? url) =>
        url != null && Regex.IsMatch(url, @"\.(mp4|webm|ogg)(\?.*)?$", RegexOptions.IgnoreCase);

    public static bool IsFacebook(string? url) =>
        url != null && url.Contains("facebook.com", StringComparison.OrdinalIgnoreCase);

    public static string FacebookEmbed(string url) =>
        "https://www.facebook.com/plugins/video.php?href=" + Uri.EscapeDataString(url) + "&show_text=false";

    public static string? Thumb(string? url) =>
        YouTubeId(url) is { } id ? $"https://i.ytimg.com/vi/{id}/hqdefault.jpg" : null;
}
