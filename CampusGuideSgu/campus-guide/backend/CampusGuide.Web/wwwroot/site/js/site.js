// CampusGuide SGU – khu người dùng. Không phụ thuộc thư viện; chạy sau khi trang tải (defer).
(function () {
  'use strict';

  // Toast tự ẩn
  var toast = document.querySelector('.cg-toast');
  if (toast) setTimeout(function () { toast.classList.add('hide'); setTimeout(function () { toast.remove(); }, 500); }, 3800);

  // Ảnh lazy-load hiện dần khi tải xong
  document.querySelectorAll('img.lazy-fade').forEach(function (img) {
    if (img.complete && img.naturalWidth) img.classList.add('loaded');
    else { img.addEventListener('load', function () { img.classList.add('loaded'); }); img.addEventListener('error', function () { img.remove(); }); }
  });

  // Video: chỉ tải trình phát YouTube/Facebook khi người dùng bấm (tiết kiệm dữ liệu di động)
  document.addEventListener('click', function (e) {
    var b = e.target.closest('[data-embed]');
    if (!b) return;
    var f = document.createElement('iframe');
    f.src = b.dataset.embed;
    f.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share';
    f.allowFullscreen = true;
    f.title = b.dataset.title || 'video';
    var box = b.closest('.vframe');
    box.innerHTML = '';
    box.appendChild(f);
  });

  // Hiện / ẩn mật khẩu
  document.querySelectorAll('.pw-wrap button').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var i = btn.parentElement.querySelector('input');
      i.type = i.type === 'password' ? 'text' : 'password';
      btn.querySelector('i').className = i.type === 'password' ? 'bi bi-eye' : 'bi bi-eye-slash';
    });
  });

  // Nghe thuyết minh: xem wwwroot/site/js/player.js (phát / tạm dừng / tua / tốc độ)

  // Báo "đang xem" cho dashboard Admin › Trực tiếp + nhật ký truy cập (giống trang quét QR /q/{code})
  var pres = document.getElementById('presence');
  if (pres) {
    var sid;
    try { sid = sessionStorage.getItem('cg_sid'); if (!sid) { sid = 'web-' + Math.random().toString(36).slice(2) + Date.now().toString(36); sessionStorage.setItem('cg_sid', sid); } }
    catch (e) { sid = 'web-' + Math.random().toString(36).slice(2); }
    var page = pres.dataset.page, point = pres.dataset.point ? parseInt(pres.dataset.point, 10) : null, lang = pres.dataset.lang;
    var timer = null, gone = true, last = 0;
    var ping = window.cgPing = function () {
      var body = { sessionId: sid, pageId: page, source: 'web', pointId: point, lang: lang,
                   listening: !!(window.cgPlayerPlaying || (window.speechSynthesis && speechSynthesis.speaking)), platform: null };
      fetch('/api/presence/ping', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), keepalive: true }).catch(function () {});
    };
    var hidden = function () { return document.visibilityState === 'hidden'; };
    var leave = function () {
      if (gone) return;
      gone = true; clearInterval(timer); timer = null;
      var url = '/api/presence/leave?sid=' + encodeURIComponent(sid) + '&page=' + encodeURIComponent(page);
      try { if (navigator.sendBeacon && navigator.sendBeacon(url)) return; } catch (e) {}
      fetch(url, { method: 'POST', keepalive: true }).catch(function () {});
    };
    var tick = function () { var now = Date.now(); if (hidden() || now - last > 9000) { leave(); return; } last = now; ping(); };
    var start = function () { if (hidden()) return; gone = false; last = Date.now(); ping(); clearInterval(timer); timer = setInterval(tick, 5000); };
    start();
    document.addEventListener('visibilitychange', function () { if (hidden()) leave(); else if (gone) start(); });
    window.addEventListener('pagehide', leave);
    window.addEventListener('pageshow', function (e) { if (e.persisted || gone) start(); });
    ['focus', 'touchstart', 'click', 'scroll'].forEach(function (ev) { window.addEventListener(ev, function () { if (gone) start(); }, { passive: true }); });
  }

  // Chia sẻ
  document.querySelectorAll('[data-share]').forEach(function (b) {
    b.addEventListener('click', function () {
      var data = { title: document.title, url: location.href.split('?')[0] };
      if (navigator.share) navigator.share(data).catch(function () {});
      else if (navigator.clipboard) navigator.clipboard.writeText(data.url).then(function () { b.querySelector('span').textContent = b.dataset.copied; });
    });
  });
})();
