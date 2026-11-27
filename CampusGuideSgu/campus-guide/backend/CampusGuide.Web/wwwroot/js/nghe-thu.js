// Nghe thử bằng giọng của trình duyệt (Web Speech API) – giống cách app đọc bằng giọng của điện thoại.
// Dùng: <button type="button" data-speak data-lang="en" data-title="#TieuDe" data-text="#VanBan">Nghe thử</button>
(function () {
  'use strict';
  var LOCALE = { vi: 'vi-VN', en: 'en-US', ja: 'ja-JP', ko: 'ko-KR', 'zh-CN': 'zh-CN', fr: 'fr-FR', de: 'de-DE',
    es: 'es-ES', ru: 'ru-RU', th: 'th-TH', id: 'id-ID', lo: 'lo-LA', km: 'km-KH' };
  var current = null;

  function valueOf(sel) {
    if (!sel) return '';
    var el = document.querySelector(sel);
    if (!el) return '';
    return ('value' in el ? el.value : el.textContent || '').trim();
  }

  function reset(btn) {
    if (!btn) return;
    btn.innerHTML = btn.dataset.label;
    btn.classList.remove('active');
  }

  function pickVoice(locale) {
    var voices = window.speechSynthesis.getVoices();
    var lang = locale.toLowerCase();
    return voices.find(function (v) { return v.lang.toLowerCase() === lang; })
      || voices.find(function (v) { return v.lang.toLowerCase().startsWith(lang.split('-')[0]); });
  }

  document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-speak]');
    if (!btn) return;
    e.preventDefault();
    var box = btn.closest('form, .card') || btn.parentElement;
    var note = box.querySelector('.speak-note');
    if (!('speechSynthesis' in window)) {
      if (note) note.textContent = 'Trình duyệt này không hỗ trợ đọc văn bản.';
      return;
    }
    if (!btn.dataset.label) btn.dataset.label = btn.innerHTML;
    var synth = window.speechSynthesis;
    if (current === btn && synth.speaking) { synth.cancel(); reset(btn); current = null; return; }
    synth.cancel();
    reset(current);

    var title = valueOf(btn.dataset.title), text = valueOf(btn.dataset.text);
    var full = [title, text].filter(Boolean).join('. ');
    if (!full) { if (note) note.textContent = 'Chưa có nội dung để đọc.'; return; }

    var locale = LOCALE[btn.dataset.lang] || btn.dataset.lang || 'vi-VN';
    var u = new SpeechSynthesisUtterance(full);
    u.lang = locale;
    var voice = pickVoice(locale);
    if (voice) u.voice = voice;
    if (note) note.textContent = voice ? 'Giọng: ' + voice.name
      : 'Máy tính chưa có giọng ' + locale + ' – trình duyệt đọc bằng giọng mặc định. Trên điện thoại sẽ đúng giọng hơn.';
    u.onend = u.onerror = function () { reset(btn); if (current === btn) current = null; };
    current = btn;
    btn.innerHTML = '<i class="bi bi-stop-fill"></i> Dừng';
    btn.classList.add('active');
    synth.speak(u);
  });

  // Chrome nạp danh sách giọng bất đồng bộ
  if ('speechSynthesis' in window) window.speechSynthesis.getVoices();
})();
