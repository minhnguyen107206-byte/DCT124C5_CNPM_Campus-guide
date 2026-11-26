namespace CampusGuide.Web.Services;

public record TtsResult(byte[] Data, string Extension, int DurationSeconds);
public record VoiceInfo(string Name, string Gender);

public interface ITtsService
{
    /// <summary>false = máy chủ không sinh file audio; app đọc văn bản bằng giọng của điện thoại.</summary>
    bool ProducesAudio { get; }
    /// <returns>null khi không sinh audio trên máy chủ.</returns>
    Task<TtsResult?> SynthesizeAsync(string text, string voiceName);
    Task<List<VoiceInfo>> ListVoicesAsync(string langCode);
}

/// <summary>
/// Giọng đọc của thiết bị: máy chủ không gọi dịch vụ đọc nào (đã bỏ Google Cloud).
/// App Flutter đọc văn bản thuyết minh bằng giọng có sẵn của điện thoại (flutter_tts),
/// trang Admin nghe thử bằng giọng của trình duyệt (Web Speech API).
/// Bảng GiongDoc lưu mã vùng ngôn ngữ (vi-VN, en-US, ...) dùng cho việc đọc đó.
/// </summary>
public class DeviceTtsService : ITtsService
{
    public bool ProducesAudio => false;

    public Task<TtsResult?> SynthesizeAsync(string text, string voiceName) => Task.FromResult<TtsResult?>(null);

    public Task<List<VoiceInfo>> ListVoicesAsync(string langCode) =>
        Task.FromResult(new List<VoiceInfo> { new(langCode, "Nu") });
}
