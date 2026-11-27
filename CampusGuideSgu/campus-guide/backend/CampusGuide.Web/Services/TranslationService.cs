using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CampusGuide.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Services;

public interface ITranslationService
{
    /// <summary>Tên dịch vụ, hiện ở trang Tổng quan.</summary>
    string Name { get; }
    /// <summary>true = dịch giả lập "[en] văn bản gốc" (không gọi mạng).</summary>
    bool IsDemo { get; }
    /// <param name="glossary">từ gốc → bản dịch (rỗng = giữ nguyên tên gốc)</param>
    Task<string> TranslateAsync(string text, string targetLang, IReadOnlyDictionary<string, string>? glossary = null);
}

/// <summary>
/// Dịch bằng MyMemory (https://mymemory.translated.net) – miễn phí, không cần tài khoản hay API key.
/// Hạn mức: khoảng 5.000 ký tự/ngày; điền <c>Translation:Email</c> trong appsettings để tăng lên khoảng 50.000 ký tự/ngày.
/// Mỗi lần gọi tối đa 500 byte nên văn bản được cắt theo câu. Thuật ngữ (UC23) được thay bằng bản dịch chuẩn trước khi gửi.
/// Hết hạn mức trong ngày thì tự chuyển sang bộ dịch dự phòng miễn phí (Google Translate, không cần key) đến hết ngày;
/// đặt "Translation:Fallback": "None" để tắt dự phòng.
/// </summary>
public class MyMemoryTranslationService : ITranslationService
{
    private const int MaxBytes = 450;
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string? _email;
    private readonly bool _fallback;
    // Ngày MyMemory báo hết hạn mức: trong ngày đó gọi thẳng bộ dự phòng, sang ngày mới thì thử lại MyMemory.
    private static DateTime? _quotaHitDate;

    public MyMemoryTranslationService(IHttpClientFactory f, IConfiguration cfg)
    {
        _http = f.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _url = (cfg["Translation:MyMemoryUrl"] ?? "https://api.mymemory.translated.net").TrimEnd('/');
        _email = cfg["Translation:Email"];
        _fallback = !string.Equals(cfg["Translation:Fallback"], "None", StringComparison.OrdinalIgnoreCase);
    }

    public string Name =>
        (string.IsNullOrWhiteSpace(_email) ? "MyMemory (chưa điền email – khoảng 5.000 ký tự/ngày)" : "MyMemory (khoảng 50.000 ký tự/ngày)")
        + (!_fallback ? "" : _quotaHitDate == DateTime.Today ? " – hôm nay đã hết hạn mức, đang dùng Google Translate dự phòng" : " + Google Translate dự phòng khi hết hạn mức");
    public bool IsDemo => false;

    public async Task<string> TranslateAsync(string text, string targetLang, IReadOnlyDictionary<string, string>? glossary = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (targetLang == "vi") return text;

        var (prepared, kept) = GlossaryText.Prepare(text, glossary);
        var result = new StringBuilder();
        foreach (var paragraph in prepared.Split('\n'))
        {
            if (result.Length > 0) result.Append('\n');
            if (string.IsNullOrWhiteSpace(paragraph)) continue;
            var parts = new List<string>();
            foreach (var chunk in SplitByBytes(paragraph.Trim(), MaxBytes))
                parts.Add(await CallAsync(chunk, targetLang));
            result.Append(string.Join(' ', parts));
        }
        return GlossaryText.Restore(result.ToString().Trim(), kept);
    }

    private async Task<string> CallAsync(string chunk, string targetLang)
    {
        if (_fallback && _quotaHitDate == DateTime.Today) return await GoogleFreeAsync(chunk, targetLang);
        try
        {
            return await MyMemoryAsync(chunk, targetLang);
        }
        catch (QuotaException) when (_fallback)
        {
            _quotaHitDate = DateTime.Today;
            return await GoogleFreeAsync(chunk, targetLang);
        }
    }

    private class QuotaException(string message) : InvalidOperationException(message);

    /// <summary>Bộ dịch dự phòng: endpoint miễn phí của Google Translate (client=gtx), không cần API key.</summary>
    private async Task<string> GoogleFreeAsync(string chunk, string targetLang)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=vi&dt=t" +
                  $"&tl={Uri.EscapeDataString(targetLang)}&q={Uri.EscapeDataString(chunk)}";
        using var resp = await _http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"MyMemory đã hết hạn mức và bộ dịch dự phòng cũng lỗi {(int)resp.StatusCode}. Thử lại ngày mai.");
        // Kết quả dạng [[["câu đã dịch","câu gốc",...], ...], ...]
        using var doc = JsonDocument.Parse(body);
        var sb = new StringBuilder();
        foreach (var seg in doc.RootElement[0].EnumerateArray())
            if (seg.ValueKind == JsonValueKind.Array && seg[0].ValueKind == JsonValueKind.String)
                sb.Append(seg[0].GetString());
        var text = sb.ToString().Trim();
        if (text.Length == 0) throw new InvalidOperationException("Bộ dịch dự phòng không trả bản dịch.");
        return text;
    }

    private async Task<string> MyMemoryAsync(string chunk, string targetLang)
    {
        var url = $"{_url}/get?q={Uri.EscapeDataString(chunk)}&langpair=vi|{Uri.EscapeDataString(targetLang)}" +
                  (string.IsNullOrWhiteSpace(_email) ? "" : $"&de={Uri.EscapeDataString(_email)}");
        using var resp = await _http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 429)
            throw new InvalidOperationException($"MyMemory lỗi {(int)resp.StatusCode}: {Short(body)}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var status = root.TryGetProperty("responseStatus", out var s)
            ? (s.ValueKind == JsonValueKind.Number ? s.GetInt32() : int.TryParse(s.GetString(), out var n) ? n : 0) : 200;
        var translated = root.TryGetProperty("responseData", out var d) && d.ValueKind == JsonValueKind.Object
                         && d.TryGetProperty("translatedText", out var t) ? t.GetString() : null;
        var quotaFinished = root.TryGetProperty("quotaFinished", out var q) && q.ValueKind == JsonValueKind.True;

        if (quotaFinished || status == 429 || (translated?.StartsWith("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase) ?? false))
            throw new QuotaException("MyMemory đã hết hạn mức dịch miễn phí hôm nay. Điền Translation:Email trong appsettings.Development.json để tăng hạn mức, hoặc thử lại ngày mai.");
        if (status != 200 || string.IsNullOrWhiteSpace(translated))
        {
            var detail = root.TryGetProperty("responseDetails", out var det) ? det.ToString() : Short(body);
            throw new InvalidOperationException($"MyMemory lỗi {status}: {detail}");
        }
        return WebUtility.HtmlDecode(translated).Trim();
    }

    /// <summary>Cắt theo câu (rồi theo từ nếu một câu quá dài) để mỗi đoạn không vượt quá maxBytes UTF-8.</summary>
    public static IEnumerable<string> SplitByBytes(string text, int maxBytes)
    {
        var sb = new StringBuilder();
        foreach (var sentence in Regex.Split(text, @"(?<=[\.\!\?。！？])\s+"))
        {
            foreach (var piece in Encoding.UTF8.GetByteCount(sentence) <= maxBytes ? new[] { sentence } : SplitWords(sentence, maxBytes))
            {
                if (sb.Length > 0 && Encoding.UTF8.GetByteCount(sb + " " + piece) > maxBytes)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(piece);
            }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static IEnumerable<string> SplitWords(string sentence, int maxBytes)
    {
        var sb = new StringBuilder();
        foreach (var w in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sb.Length > 0 && Encoding.UTF8.GetByteCount(sb + " " + w) > maxBytes) { yield return sb.ToString(); sb.Clear(); }
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(w);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static string Short(string s) => s.Length > 300 ? s[..300] : s;
}

/// <summary>Dịch giả lập ("Translation:Provider": "Demo") – dùng khi không có Internet, VD lúc demo mất mạng.</summary>
public class DemoTranslationService : ITranslationService
{
    public string Name => "Giả lập (Translation:Provider = Demo)";
    public bool IsDemo => true;

    public Task<string> TranslateAsync(string text, string targetLang, IReadOnlyDictionary<string, string>? glossary = null) =>
        Task.FromResult(string.IsNullOrWhiteSpace(text) || targetLang == "vi" ? text : $"[{targetLang}] {GlossaryText.Apply(text, glossary)}");
}

/// <summary>Thay thuật ngữ bằng bản dịch chuẩn (cụm dài trước để tránh thay chồng).
/// Quy tắc UC23: bản dịch để trống nghĩa là giữ nguyên tên gốc tiếng Việt khi dịch.</summary>
public static class GlossaryText
{
    public static string Apply(string text, IReadOnlyDictionary<string, string>? glossary)
    {
        if (glossary == null) return text;
        foreach (var (term, tr) in glossary.OrderByDescending(g => g.Key.Length))
            if (!string.IsNullOrWhiteSpace(tr))
                text = Regex.Replace(text, Regex.Escape(term), tr, RegexOptions.IgnoreCase);
        return text;
    }

    public static (string Prepared, Dictionary<string, string> Kept) Prepare(string text, IReadOnlyDictionary<string, string>? glossary)
    {
        var kept = new Dictionary<string, string>();
        if (glossary == null) return (text, kept);
        int idx = 0;
        foreach (var (term, tr) in glossary.OrderByDescending(g => g.Key.Length))
        {
            if (string.IsNullOrWhiteSpace(term)) continue;
            if (!string.IsNullOrWhiteSpace(tr))
            {
                text = Regex.Replace(text, Regex.Escape(term), tr, RegexOptions.IgnoreCase);
            }
            else if (Regex.IsMatch(text, Regex.Escape(term), RegexOptions.IgnoreCase))
            {
                var token = $"CGKEEP{idx++}Z";
                kept[token] = term;
                text = Regex.Replace(text, Regex.Escape(term), token, RegexOptions.IgnoreCase);
            }
        }
        return (text, kept);
    }

    public static string Restore(string text, IReadOnlyDictionary<string, string> kept)
    {
        foreach (var (token, original) in kept)
            text = Regex.Replace(text, Regex.Escape(token), original, RegexOptions.IgnoreCase);
        return text;
    }
}

/// <summary>Lấy bảng thuật ngữ (UC23) theo ngôn ngữ đích.</summary>
public class GlossaryService
{
    private readonly AppDbContext _db;
    public GlossaryService(AppDbContext db) => _db = db;

    public async Task<Dictionary<string, string>> ForLanguageAsync(string lang)
    {
        var terms = await _db.ThuatNgus.Include(t => t.BanDichs).AsNoTracking().ToListAsync();
        return terms.ToDictionary(
            t => t.TuGoc,
            t => t.BanDichs.FirstOrDefault(b => b.MaNgonNgu == lang)?.BanDich ?? "");
    }
}
