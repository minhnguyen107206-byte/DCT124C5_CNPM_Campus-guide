namespace CampusGuide.Web.Services;

/// <summary>Lưu file (ảnh, audio, gói offline). Bản này lưu trên ổ đĩa, phục vụ qua /media/...</summary>
public interface IStorageService
{
    /// <returns>Đường dẫn tương đối dạng /media/{folder}/{file}</returns>
    Task<string> SaveAsync(byte[] data, string folder, string fileName);
    Task<byte[]?> ReadAsync(string relativeUrl);
    void Delete(string? relativeUrl);
    string PhysicalPath(string relativeUrl);
}

public class LocalStorageService : IStorageService
{
    private readonly string _webRoot;

    public LocalStorageService(IWebHostEnvironment env)
    {
        _webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        Directory.CreateDirectory(Path.Combine(_webRoot, "media"));
    }

    public async Task<string> SaveAsync(byte[] data, string folder, string fileName)
    {
        var safe = string.Concat(fileName.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        var dir = Path.Combine(_webRoot, "media", folder);
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, safe), data);
        return $"/media/{folder}/{safe}";
    }

    public async Task<byte[]?> ReadAsync(string relativeUrl)
    {
        var p = PhysicalPath(relativeUrl);
        return File.Exists(p) ? await File.ReadAllBytesAsync(p) : null;
    }

    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl)) return;
        var p = PhysicalPath(relativeUrl);
        if (File.Exists(p)) File.Delete(p);
    }

    public string PhysicalPath(string relativeUrl)
    {
        var rel = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_webRoot, rel));
        if (!full.StartsWith(Path.GetFullPath(_webRoot))) throw new InvalidOperationException("Đường dẫn không hợp lệ");
        return full;
    }
}
