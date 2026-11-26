using System.Net;
using System.Text;
using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Controllers;

/// <summary>
/// UC14 – Trang mở ra khi quét QR bằng camera thường (chưa cài app): xem và nghe thuyết minh ngay trên trình duyệt,
/// chọn ngôn ngữ (mặc định theo ngôn ngữ của điện thoại), đọc to bằng giọng của máy (Web Speech API).
/// Ngôn ngữ chưa có bản dịch đã duyệt: hiện bản dịch máy đang chờ duyệt; chưa dịch lần nào thì dịch ngay (MyMemory)
/// và lưu vào hàng chờ duyệt, để biên tập viên duyệt sau ở trang Admin.
/// </summary>
public class QrLandingController : Controller
{
    private readonly AppDbContext _db;
    private readonly ContentResolver _resolver;
    private readonly TranslationPipeline _pipeline;
    private readonly ILogger<QrLandingController> _log;

    public QrLandingController(AppDbContext db, ContentResolver resolver, TranslationPipeline pipeline, ILogger<QrLandingController> log)
    {
        _db = db; _resolver = resolver; _pipeline = pipeline; _log = log;
    }

    [HttpGet("/q/{code}")]
    public async Task<IActionResult> Index(string code, string? lang)
    {
        code = (code ?? "").Trim();
        var langs = await _db.NgonNgus.AsNoTracking().Where(n => n.KichHoat).OrderBy(n => n.ThuTu).ToListAsync();
        var d = await _resolver.PublishedPoints().FirstOrDefaultAsync(x => x.MaQR == code);
        if (d == null)
        {
            var exists = await _db.Diems.AsNoTracking().AnyAsync(x => x.MaQR == code);
            return Page("CampusGuide", exists
                ? "Điểm này đang tạm ẩn hoặc chưa được xuất bản. Vui lòng thử lại sau."
                : "Mã QR không hợp lệ. Hãy quét mã dán tại các toà nhà, phòng ban của trường.", null, 404);
        }

        lang = PickLanguage(lang, langs.Select(l => l.MaNgonNgu).ToList());
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var p = _resolver.ToDto(d, lang, await _resolver.FallbackLangAsync(), baseUrl);

        // Chưa có bản dịch đã duyệt cho ngôn ngữ này → dùng bản dịch máy (dịch ngay nếu chưa có)
        var machine = false;
        if (lang != "vi" && p.Lang != lang)
        {
            var may = await MachineTranslationAsync(d.MaDiem, lang);
            if (may != null)
            {
                p = p with { Title = may.TieuDe, Text = may.VanBan, Lang = lang, IsFallback = false, AudioUrl = null };
                machine = true;
            }
        }

        var sb = new StringBuilder();
        sb.Append("<nav class=\"langs\">");
        foreach (var l in langs)
        {
            var cls = l.MaNgonNgu == lang ? " class=\"on\"" : "";
            sb.Append($"<a{cls} href=\"/q/{Uri.EscapeDataString(code)}?lang={Uri.EscapeDataString(l.MaNgonNgu)}\">{H(l.Co)} {H(l.TenNgonNgu)}</a>");
        }
        sb.Append("</nav>");
        if (p.Image != null) sb.Append($"<img class=\"hero\" src=\"{H(p.Image)}\" alt=\"\">");
        sb.Append($"<h1>{H(p.Title)}</h1>");
        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Floor)) meta.Add(H(p.Floor));
        if (!string.IsNullOrWhiteSpace(p.Hours)) meta.Add("🕒 " + H(p.Hours));
        if (meta.Count > 0) sb.Append($"<p class=\"meta\">{string.Join(" · ", meta)}</p>");
        if (p.IsFallback) sb.Append("<p class=\"note\">Chưa có bản dịch cho ngôn ngữ này – đang hiển thị ngôn ngữ dự phòng.</p>");
        if (machine) sb.Append($"<p class=\"note\">{H(MachineLabel(lang))}</p>");
        // Trình phát thuyết minh dùng chung với khu người dùng (wwwroot/site/js/player.js): phát / tạm dừng / tua / tốc độ
        if (p.AudioUrl != null || !string.IsNullOrWhiteSpace(p.Text))
            sb.Append($"<div class=\"cg-player\" data-ui=\"{H(lang)}\" data-voice=\"{H(Locale(p.Lang))}\" data-text=\"#txt\" data-title=\"{H(p.Title)}\" data-src=\"{H(p.AudioUrl)}\"></div>");
        sb.Append($"<div id=\"txt\" lang=\"{H(p.Lang)}\">{H(p.Text)}</div>");
        sb.Append(MapSection(d, lang));
        sb.Append(PresenceScript(d.MaDiem, lang, code));
        sb.Append("<p class=\"app\">Cài ứng dụng <b>CampusGuide</b> để được dẫn đường, tự nghe thuyết minh khi đi đến gần và đi theo tour.</p>");
        return Page(p.Title, null, sb.ToString(), 200);
    }

    /// <summary>
    /// Bản dịch máy của điểm theo ngôn ngữ: lấy bản đang chờ duyệt (cùng phiên bản bản gốc);
    /// chưa có thì dịch ngay bằng TranslationPipeline (lưu trạng thái Chờ duyệt). Lỗi (mất mạng, hết hạn mức) → null.
    /// </summary>
    private async Task<NoiDung?> MachineTranslationAsync(int maDiem, string lang)
    {
        NoiDung? Find(List<NoiDung> list)
        {
            var goc = list.FirstOrDefault(n => n.TrangThai == TrangThaiNoiDung.Goc);
            return list.FirstOrDefault(n => n.MaNgonNgu == lang && n.TrangThai == TrangThaiNoiDung.ChoDuyet
                                            && !string.IsNullOrWhiteSpace(n.VanBan)
                                            && (goc == null || n.PhienBanGoc == goc.PhienBanGoc));
        }

        var nds = await _db.NoiDungs.AsNoTracking().Where(n => n.MaDiem == maDiem).ToListAsync();
        var found = Find(nds);
        if (found != null) return found;
        try
        {
            await _pipeline.TranslatePointAsync(maDiem, new[] { lang });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Dịch khi quét QR lỗi (điểm {MaDiem}, {Lang})", maDiem, lang);
            return null;
        }
        nds = await _db.NoiDungs.AsNoTracking().Where(n => n.MaDiem == maDiem).ToListAsync();
        return Find(nds);
    }

    /// <summary>Bản đồ vị trí điểm + nút chỉ đường đi bộ từ vị trí hiện tại của người quét (GPS của điện thoại).
    /// Đường đi lấy từ OSRM (routing.openstreetmap.de, miễn phí); lỗi thì vẽ đường thẳng. Luôn có nút mở Google Maps.
    /// Lưu ý: trình duyệt chỉ cho lấy vị trí trên https (VD link Cloudflare tunnel) – trên http thì dùng nút Google Maps.</summary>
    private static string MapSection(Diem d, string lang)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var lat = d.ViDo.ToString(inv);
        var lng = d.KinhDo.ToString(inv);
        var t = MapText(lang);
        var gmaps = $"https://www.google.com/maps/dir/?api=1&destination={lat},{lng}&travelmode=walking";
        return $$"""
            <section class="map-card">
              <h2>🗺️ {{H(t.Title)}}</h2>
              <div id="map"></div>
              <div id="route-info" class="route-info" hidden></div>
              <div class="map-btns">
                <button id="go" type="button">🧭 {{H(t.Go)}}</button>
                <a class="gmaps" href="{{H(gmaps)}}" target="_blank" rel="noopener">📍 Google Maps</a>
              </div>
              <p id="geo-msg" class="note" hidden></p>
            </section>
            <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
            <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
            <script>
            (function () {
              var dest = [{{lat}}, {{lng}}];
              var T = { you: {{Js(t.You)}}, here: {{Js(t.Here)}}, finding: {{Js(t.Finding)}}, noGeo: {{Js(t.NoGeo)}}, denied: {{Js(t.Denied)}},
                        insecure: {{Js(t.Insecure)}}, arrived: {{Js(t.Arrived)}}, walk: {{Js(t.Walk)}}, min: {{Js(t.Min)}} };
              var map = L.map('map', { maxZoom: 21 }).setView(dest, 18);
              var road = L.tileLayer('https://mt{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}', { subdomains: '0123', maxZoom: 21, attribution: '© Google' });
              var sat = L.tileLayer('https://mt{s}.google.com/vt/lyrs=y&x={x}&y={y}&z={z}', { subdomains: '0123', maxZoom: 21, attribution: '© Google' });
              sat.addTo(map);
              L.control.layers({ '🛰️': sat, '🗺️': road }).addTo(map);
              L.marker(dest).addTo(map).bindPopup(T.here + ': ' + document.querySelector('h1').textContent).openPopup();

              var me = null, acc = null, line = null, watch = null, lastRoute = 0, info = document.getElementById('route-info'), msg = document.getElementById('geo-msg');
              function show(t) { msg.textContent = t; msg.hidden = false; }
              function dist(a, b) { return map.distance(a, b); }
              function fmt(m) { return m >= 1000 ? (m / 1000).toFixed(1) + ' km' : Math.round(m) + ' m'; }

              function drawRoute(from) {
                var url = 'https://routing.openstreetmap.de/routed-foot/route/v1/driving/' + from[1] + ',' + from[0] + ';' + dest[1] + ',' + dest[0] + '?overview=full&geometries=geojson';
                fetch(url).then(function (r) { return r.json(); }).then(function (j) {
                  var rt = j.routes && j.routes[0];
                  if (!rt) throw 0;
                  var pts = rt.geometry.coordinates.map(function (c) { return [c[1], c[0]]; });
                  pts.unshift(from); pts.push(dest);
                  setLine(pts, rt.distance);
                }).catch(function () { setLine([from, dest], dist(from, dest)); });
              }
              function setLine(pts, meters) {
                if (line) map.removeLayer(line);
                line = L.polyline(pts, { color: '#2f7cf6', weight: 6, opacity: .9, dashArray: null }).addTo(map);
                var mins = Math.max(1, Math.round(meters / 75));   // đi bộ ~4,5 km/h
                info.innerHTML = '🚶 ' + T.walk + ' <b>' + fmt(meters) + '</b> · ~<b>' + mins + '</b> ' + T.min;
                info.hidden = false;
              }

              function onPos(pos) {
                var p = [pos.coords.latitude, pos.coords.longitude];
                if (!me) {
                  me = L.circleMarker(p, { radius: 8, color: '#fff', weight: 3, fillColor: '#2f7cf6', fillOpacity: 1 }).addTo(map).bindTooltip(T.you, { permanent: true, direction: 'top' });
                  acc = L.circle(p, { radius: pos.coords.accuracy, color: '#2f7cf6', weight: 1, fillOpacity: .1 }).addTo(map);
                  map.fitBounds(L.latLngBounds([p, dest]).pad(0.25));
                } else { me.setLatLng(p); acc.setLatLng(p).setRadius(pos.coords.accuracy); }
                msg.hidden = true;
                if (dist(p, dest) <= Math.max({{d.BanKinh}}, 15)) { info.innerHTML = '🎉 ' + T.arrived; info.hidden = false; if (line) map.removeLayer(line); line = null; return; }
                var now = Date.now();
                if (now - lastRoute > 15000) { lastRoute = now; drawRoute(p); }   // tính lại đường mỗi 15 giây khi đang đi
                else if (line) { var ll = line.getLatLngs(); ll[0] = L.latLng(p); line.setLatLngs(ll); }
              }
              function onErr(e) { show(e && e.code === 1 ? T.denied : T.noGeo); }

              document.getElementById('go').addEventListener('click', function () {
                if (!window.isSecureContext) { show(T.insecure); return; }
                if (!navigator.geolocation) { show(T.noGeo); return; }
                show(T.finding);
                if (watch !== null) navigator.geolocation.clearWatch(watch);
                lastRoute = 0;
                watch = navigator.geolocation.watchPosition(onPos, onErr, { enableHighAccuracy: true, maximumAge: 5000, timeout: 20000 });
              });
            })();
            </script>
            """;
    }

    /// <summary>Báo "đang ở đây" cho dashboard Trực tiếp (PresenceController: /api/presence/ping mỗi 5 giây,
    /// /api/presence/leave khi rời trang). Listening = đang bấm nghe thuyết minh.</summary>
    private static string PresenceScript(int maDiem, string lang, string code) => $$"""
        <script>
        (function () {
          var sid;
          try { sid = sessionStorage.getItem('cg_sid'); if (!sid) { sid = 'web-' + Math.random().toString(36).slice(2) + Date.now().toString(36); sessionStorage.setItem('cg_sid', sid); } }
          catch (e) { sid = 'web-' + Math.random().toString(36).slice(2); }
          var page = 'qr:' + {{Js(code)}};
          function ping() {
            var body = { sessionId: sid, pageId: page, source: 'web', pointId: {{maDiem}}, lang: {{Js(lang)}},
                         listening: !!(window.cgPlayerPlaying || (window.speechSynthesis && speechSynthesis.speaking)), platform: null };
            fetch('/api/presence/ping', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), keepalive: true }).catch(function () {});
          }
          var timer = null, gone = true, last = 0;
          function isHidden() { return document.visibilityState === 'hidden'; }
          function tick() {
            var now = Date.now();
            // Trang bị ẩn, hoặc bộ đếm giờ bị trễ (iPhone cho trang chạy ngầm chậm lại khi đã thoát ra) → coi như đã rời đi
            if (isHidden() || now - last > 9000) { leave(); return; }
            last = now; ping();
          }
          function start() {
            if (isHidden()) return;
            gone = false; last = Date.now(); ping();
            clearInterval(timer); timer = setInterval(tick, 5000);
          }
          function leave() {
            if (gone) return;
            gone = true; clearInterval(timer); timer = null;
            var url = '/api/presence/leave?sid=' + encodeURIComponent(sid) + '&page=' + encodeURIComponent(page);
            try { if (navigator.sendBeacon && navigator.sendBeacon(url)) return; } catch (e) {}
            fetch(url, { method: 'POST', keepalive: true }).catch(function () {});
          }
          start();
          window.cgPing = ping;   // player.js gọi khi bắt đầu nghe
          // Thoát ra màn hình chính, chuyển app, khoá máy, đóng tab → báo rời đi ngay; quay lại trang → báo có mặt lại.
          document.addEventListener('visibilitychange', function () { if (isHidden()) leave(); else if (gone) start(); });
          window.addEventListener('pagehide', leave);
          window.addEventListener('beforeunload', leave);
          window.addEventListener('blur', function () { setTimeout(function () { if (isHidden()) leave(); }, 500); });
          window.addEventListener('pageshow', function (e) { if (e.persisted || gone) start(); });
          ['focus', 'touchstart', 'click', 'scroll'].forEach(function (ev) { window.addEventListener(ev, function () { if (gone) start(); }, { passive: true }); });
        })();
        </script>
        """;

    private record MapLabels(string Title, string Go, string You, string Here, string Finding, string NoGeo, string Denied,
                             string Insecure, string Arrived, string Walk, string Min);

    private static MapLabels MapText(string lang) => lang switch
    {
        "en" => new("Map & directions", "Directions from my location", "You", "Destination", "Finding your location...",
                    "Cannot get your location. Use Google Maps below.", "Location permission denied – allow it in browser settings or use Google Maps.",
                    "This page is not https so the browser blocks GPS – tap Google Maps to get directions.", "You have arrived!", "Walking", "min"),
        "ja" => new("地図と道案内", "現在地から道案内", "現在地", "目的地", "現在地を取得中...",
                    "現在地を取得できません。Google マップをご利用ください。", "位置情報が拒否されました。設定で許可するか Google マップをご利用ください。",
                    "https ではないため GPS が使えません。Google マップをタップしてください。", "到着しました！", "徒歩", "分"),
        "ko" => new("지도 및 길찾기", "현재 위치에서 길찾기", "현재 위치", "목적지", "위치를 찾는 중...",
                    "위치를 가져올 수 없습니다. Google 지도를 이용하세요.", "위치 권한이 거부되었습니다. 설정에서 허용하거나 Google 지도를 이용하세요.",
                    "https가 아니어서 GPS를 사용할 수 없습니다. Google 지도를 누르세요.", "도착했습니다!", "도보", "분"),
        "zh-CN" => new("地图与导航", "从我的位置导航", "我的位置", "目的地", "正在定位...",
                    "无法获取位置，请使用 Google 地图。", "定位权限被拒绝，请在设置中允许或使用 Google 地图。",
                    "此页面不是 https，浏览器禁止 GPS，请点击 Google 地图导航。", "您已到达！", "步行", "分钟"),
        _ => new("Bản đồ & chỉ đường", "Chỉ đường từ vị trí của tôi", "Bạn ở đây", "Điểm đến", "Đang lấy vị trí của bạn...",
                    "Không lấy được vị trí. Hãy dùng nút Google Maps bên dưới.", "Bạn đã chặn quyền vị trí – cho phép trong cài đặt trình duyệt hoặc dùng Google Maps.",
                    "Trang đang mở bằng http nên trình duyệt chặn GPS – bấm Google Maps để chỉ đường.", "Bạn đã đến nơi!", "Đi bộ", "phút"),
    };

    private static string Js(string s) => System.Text.Json.JsonSerializer.Serialize(s);

    private static string MachineLabel(string lang) => lang switch
    {
        "en" => "Machine translation – pending review.",
        "ja" => "機械翻訳です（確認待ち）。",
        "ko" => "기계 번역입니다 (검토 대기 중).",
        "zh-CN" => "机器翻译（待审核）。",
        _ => "Bản dịch tự động, đang chờ duyệt.",
    };

    /// <summary>?lang= → ngôn ngữ của trình duyệt (Accept-Language) → tiếng Việt.</summary>
    private string PickLanguage(string? lang, List<string> active)
    {
        if (!string.IsNullOrWhiteSpace(lang) && active.Contains(lang)) return lang;
        foreach (var raw in Request.Headers.AcceptLanguage.ToString().Split(','))
        {
            var tag = raw.Split(';')[0].Trim();
            if (tag.Length == 0) continue;
            var hit = active.FirstOrDefault(a => a.Equals(tag, StringComparison.OrdinalIgnoreCase))
                      ?? active.FirstOrDefault(a => a.Split('-')[0].Equals(tag.Split('-')[0], StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        return "vi";
    }

    private static string Locale(string lang) => lang switch
    {
        "vi" => "vi-VN", "en" => "en-US", "ja" => "ja-JP", "ko" => "ko-KR", "zh-CN" => "zh-CN",
        "fr" => "fr-FR", "de" => "de-DE", "es" => "es-ES", "ru" => "ru-RU", "th" => "th-TH", "id" => "id-ID",
        _ => lang,
    };

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private ContentResult Page(string title, string? message, string? body, int status)
    {
        Response.StatusCode = status;
        var content = body ?? $"<h1>CampusGuide</h1><p class=\"note\">{H(message)}</p>";
        var html = $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/site/css/player.css"><title>{{H(title)}} – CampusGuide</title>
            <style>
            body{font-family:system-ui,-apple-system,sans-serif;max-width:600px;margin:0 auto;padding:16px 16px 40px;line-height:1.6;color:#1b2a41;background:#f6f8fc}
            .brand{display:inline-block;background:#2f5597;color:#fff;border-radius:12px;padding:2px 12px;font-size:13px;font-weight:600}
            .langs{display:flex;flex-wrap:wrap;gap:6px;margin:14px 0}
            .langs a{padding:4px 10px;border-radius:16px;background:#e8eefb;color:#2f5597;text-decoration:none;font-size:14px}
            .langs a.on{background:#2f5597;color:#fff}
            .hero{width:100%;border-radius:12px;max-height:240px;object-fit:cover}
            h1{font-size:1.5rem;margin:12px 0 4px}
            .meta{color:#5b6b82;margin:0 0 8px}
            .note{background:#fff4e5;color:#8a4b00;border-radius:8px;padding:8px 12px}
            .cg-player{--cg-primary:#2f5597;--cg-violet:#5b3fb5;margin:8px 0 12px}
            #txt{white-space:pre-line;background:#fff;border-radius:12px;padding:14px 16px;box-shadow:0 1px 3px rgba(16,24,40,.08)}
            .app{color:#5b6b82;font-size:14px;margin-top:20px}
            .map-card{background:#fff;border-radius:14px;padding:14px;margin-top:16px;box-shadow:0 1px 3px rgba(16,24,40,.08)}
            .map-card h2{font-size:1.1rem;margin:0 0 10px}
            #map{height:320px;border-radius:12px;z-index:0}
            .route-info{margin-top:10px;background:#eaf2ff;color:#1d3c78;border-radius:10px;padding:8px 12px}
            .map-btns{display:flex;gap:8px;margin-top:10px}
            .map-btns #go{flex:1;padding:12px;border:0;border-radius:10px;background:#16a34a;color:#fff;font-size:1rem;font-weight:600}
            .map-btns .gmaps{padding:12px 14px;border-radius:10px;background:#e8eefb;color:#2f5597;text-decoration:none;font-weight:600;white-space:nowrap}
            </style></head><body>
            <span class="brand">CampusGuide · Trường Đại học Sài Gòn</span>
            {{content}}
            <script src="/site/js/player.js" defer></script>
            </body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }
}
