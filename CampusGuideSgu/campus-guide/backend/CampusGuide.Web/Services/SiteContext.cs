using CampusGuide.Web.Models;

namespace CampusGuide.Web.Services;

/// <summary>Thông tin dùng chung cho mọi trang của khu người dùng (layout, menu): ngôn ngữ đang chọn,
/// danh sách ngôn ngữ đang bật (lấy từ Admin › Ngôn ngữ).
/// Được SiteControllerBase điền trước mỗi request.</summary>
public class SiteContext
{
    public const string LangCookie = "cg_lang";

    public string Lang { get; set; } = "vi";
    public List<NgonNgu> Langs { get; set; } = new();
    public NgonNgu? Current => Langs.FirstOrDefault(l => l.MaNgonNgu == Lang);

    /// <summary>Chuỗi giao diện theo ngôn ngữ đang chọn.</summary>
    public string T(string key) => UiText.Get(Lang, key);
    public string T(string key, params object[] args) => string.Format(UiText.Get(Lang, key), args);

    /// <summary>Mã vùng cho giọng đọc của trình duyệt (Web Speech API).</summary>
    public static string Locale(string lang) => lang switch
    {
        "vi" => "vi-VN", "en" => "en-US", "ja" => "ja-JP", "ko" => "ko-KR", "zh-CN" => "zh-CN",
        "fr" => "fr-FR", "de" => "de-DE", "es" => "es-ES", "ru" => "ru-RU", "th" => "th-TH", "id" => "id-ID",
        _ => lang,
    };

    /// <summary>Icon Bootstrap Icons theo biểu tượng của loại điểm (LoaiDiem.BieuTuong).</summary>
    public static string Icon(string? bieuTuong) => bieuTuong switch
    {
        "gate" => "bi-signpost-2",
        "building" => "bi-building",
        "hall" => "bi-easel2",
        "library" => "bi-book",
        "food" => "bi-cup-hot",
        "office" => "bi-briefcase",
        "parking" => "bi-p-square",
        "sport" => "bi-dribbble",
        "landmark" => "bi-flag",
        _ => "bi-geo-alt",
    };

    /// <summary>Màu nền thẻ theo loại điểm (ảnh minh hoạ khi điểm chưa có hình).</summary>
    public static string Tone(string? bieuTuong) => bieuTuong switch
    {
        "gate" => "tone-blue", "building" => "tone-indigo", "hall" => "tone-purple", "library" => "tone-teal",
        "food" => "tone-orange", "office" => "tone-slate", "parking" => "tone-gray", "sport" => "tone-green",
        _ => "tone-pink",
    };

    public static string Local(DateTime utc, string fmt = "dd/MM/yyyy HH:mm") =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString(fmt);

}
