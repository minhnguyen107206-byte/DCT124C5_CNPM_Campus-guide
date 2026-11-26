using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Services;

public record LangResult(string Lang, bool Translated, bool HasAudio, string? Error);

/// <summary>UC16 – Tự động dịch (MyMemory) và sinh audio nếu dịch vụ giọng đọc có tạo file.
/// Bản hiện tại dùng giọng của thiết bị (DeviceTtsService) nên không có file audio: app đọc văn bản trên điện thoại.</summary>
public class TranslationPipeline
{
    private readonly AppDbContext _db;
    private readonly ITranslationService _tr;
    private readonly ITtsService _tts;
    private readonly IStorageService _store;
    private readonly GlossaryService _glossary;
    private readonly ILogger<TranslationPipeline> _log;

    public TranslationPipeline(AppDbContext db, ITranslationService tr, ITtsService tts, IStorageService store,
        GlossaryService glossary, ILogger<TranslationPipeline> log)
    {
        _db = db; _tr = tr; _tts = tts; _store = store; _glossary = glossary; _log = log;
    }

    /// <summary>Dịch bản gốc của một điểm sang các ngôn ngữ (null = tất cả đang bật) rồi sinh audio.</summary>
    public async Task<List<LangResult>> TranslatePointAsync(int maDiem, IEnumerable<string>? langs = null)
    {
        var results = new List<LangResult>();
        var goc = await _db.NoiDungs.Include(n => n.Audios)
            .FirstOrDefaultAsync(n => n.MaDiem == maDiem && n.TrangThai == TrangThaiNoiDung.Goc);
        if (goc == null || string.IsNullOrWhiteSpace(goc.VanBan))
            throw new InvalidOperationException("Điểm chưa có nội dung gốc tiếng Việt (UC15).");

        // audio tiếng Việt cho bản gốc (nếu chưa có hoặc nội dung đã đổi)
        if (_tts.ProducesAudio && (!goc.Audios.Any() || goc.Audios.Max(a => a.NgaySinh) < goc.NgayCapNhat))
        {
            try { await GenerateAudioAsync(goc); results.Add(new LangResult("vi", true, true, null)); }
            catch (Exception ex) { results.Add(new LangResult("vi", true, false, ex.Message)); }
        }

        var active = await _db.NgonNgus.Where(n => n.KichHoat && !n.LaNgonNguGoc).Select(n => n.MaNgonNgu).ToListAsync();
        var targets = langs == null ? active : active.Intersect(langs).ToList();

        foreach (var lang in targets)
        {
            var nd = await _db.NoiDungs.Include(n => n.Audios)
                .FirstOrDefaultAsync(n => n.MaDiem == maDiem && n.MaNgonNgu == lang);
            if (nd == null)
            {
                nd = new NoiDung { MaDiem = maDiem, MaNgonNgu = lang };
                _db.NoiDungs.Add(nd);
            }
            try
            {
                var gl = await _glossary.ForLanguageAsync(lang);
                nd.TieuDe = await _tr.TranslateAsync(goc.TieuDe, lang, gl);
                nd.VanBan = await _tr.TranslateAsync(goc.VanBan, lang, gl);
                nd.PhienBanGoc = goc.PhienBanGoc;
                nd.TrangThai = TrangThaiNoiDung.ChoDuyet;
                nd.LyDoTuChoi = null;
                nd.NgayCapNhat = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Dịch lỗi {Lang}", lang);
                nd.TrangThai = TrangThaiNoiDung.LoiDich;
                await _db.SaveChangesAsync();
                results.Add(new LangResult(lang, false, false, ex.Message));
                continue;
            }
            try
            {
                var audio = await GenerateAudioAsync(nd);
                results.Add(new LangResult(lang, true, audio != null, null));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Sinh audio lỗi {Lang}", lang);
                results.Add(new LangResult(lang, true, false, ex.Message));
            }
        }
        return results;
    }

    /// <summary>Sinh audio cho một nội dung; giữ các bản cũ trong bảng Audio, bản mới nhất được dùng.
    /// Trả về null khi dùng giọng của thiết bị (không có file audio trên máy chủ).</summary>
    public async Task<Audio?> GenerateAudioAsync(NoiDung nd, int? maGiong = null)
    {
        if (!_tts.ProducesAudio) return null;

        var giong = maGiong.HasValue
            ? await _db.GiongDocs.FirstOrDefaultAsync(g => g.MaGiong == maGiong.Value && g.MaNgonNgu == nd.MaNgonNgu)
            : await _db.GiongDocs.Where(g => g.MaNgonNgu == nd.MaNgonNgu).OrderByDescending(g => g.MacDinh).FirstOrDefaultAsync();
        if (giong == null) throw new InvalidOperationException($"Chưa có giọng đọc cho ngôn ngữ {nd.MaNgonNgu} (UC22).");

        var text = string.IsNullOrWhiteSpace(nd.TieuDe) ? nd.VanBan : $"{nd.TieuDe}. {nd.VanBan}";
        var res = await _tts.SynthesizeAsync(text, giong.TenGiongAPI);
        if (res == null) return null;
        var file = $"p{nd.MaDiem}_{nd.MaNgonNgu}_{DateTime.UtcNow:yyyyMMddHHmmss}.{res.Extension}";
        var url = await _store.SaveAsync(res.Data, "audio", file);

        var audio = new Audio { MaNoiDung = nd.MaNoiDung, MaGiong = giong.MaGiong, DuongDan = url, ThoiLuong = res.DurationSeconds };
        _db.Audios.Add(audio);
        await _db.SaveChangesAsync();
        return audio;
    }
}
