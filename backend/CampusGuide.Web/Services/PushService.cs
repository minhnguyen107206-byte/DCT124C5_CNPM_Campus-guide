using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace CampusGuide.Web.Services;

public record PushMessage(string Token, string Title, string Body, Dictionary<string, string>? Data = null);

public interface IPushService
{
    bool Enabled { get; }
    /// <returns>Số thông báo gửi thành công</returns>
    Task<int> SendAsync(IReadOnlyList<PushMessage> messages);
}

/// <summary>
/// Gửi thông báo đẩy qua Firebase Cloud Messaging (HTTP v1, dùng file service account).
/// Chưa cấu hình Firebase:CredentialsPath thì chỉ ghi log – app vẫn nhận thông báo khi mở lại (UC10, E1).
/// </summary>
public class FirebasePushService : IPushService
{
    private static readonly object Lock = new();
    private readonly ILogger<FirebasePushService> _log;
    private readonly bool _enabled;

    public FirebasePushService(IConfiguration cfg, ILogger<FirebasePushService> log)
    {
        _log = log;
        var path = cfg["Firebase:CredentialsPath"];
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            lock (Lock)
            {
                if (FirebaseApp.DefaultInstance == null)
                    FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(path) });
            }
            _enabled = true;
        }
    }

    public bool Enabled => _enabled;

    public async Task<int> SendAsync(IReadOnlyList<PushMessage> messages)
    {
        if (messages.Count == 0) return 0;
        if (!_enabled)
        {
            _log.LogInformation("[Push DEMO] Bỏ qua gửi {Count} thông báo (chưa cấu hình Firebase).", messages.Count);
            return 0;
        }
        int ok = 0;
        foreach (var batch in messages.Chunk(500))
        {
            var res = await FirebaseMessaging.DefaultInstance.SendEachAsync(batch.Select(m => new Message
            {
                Token = m.Token,
                Notification = new Notification { Title = m.Title, Body = m.Body },
                Data = m.Data,
            }).ToList());
            ok += res.SuccessCount;
        }
        return ok;
    }
}
