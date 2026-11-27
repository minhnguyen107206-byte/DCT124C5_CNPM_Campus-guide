// CampusGuide SGU – trình phát thuyết minh: phát / tạm dừng / tua / đổi tốc độ.
// Dùng chung cho trang chi tiết điểm (/diem/{id}, /diem/qr/{code}) và trang quét QR (/q/{code}).
//
// <div class="cg-player" data-ui="vi" data-voice="vi-VN" data-text="#pointText" data-title="Tên điểm" [data-src="file.mp3"]></div>
//
// • Có data-src (file âm thanh thật): dùng thẻ <audio>, tua theo giây (lùi/tiến 10 giây, kéo thanh tiến độ).
// • Không có: đọc bằng giọng của máy (Web Speech API). API này chỉ đọc được từng đoạn, không tua được,
//   nên nội dung được chia thành từng câu: kéo thanh tiến độ / bấm câu trước – câu sau / chạm vào một câu
//   trong bài để nhảy tới đó. Tạm dừng nhớ vị trí tới từng từ (nếu trình duyệt báo vị trí), phát lại đọc tiếp.
(function () {
  'use strict';

  var TEXT = {
    vi: { play: 'Phát', pause: 'Tạm dừng', replay: 'Nghe lại', prev: 'Câu trước', next: 'Câu sau', back: 'Lùi 10 giây', fwd: 'Tiến 10 giây',
          speed: 'Tốc độ đọc', seek: 'Tua', sentence: 'Câu', listen: 'Nghe thuyết minh', hint: 'Chạm vào một câu trong bài để nghe từ câu đó',
          unsupported: 'Trình duyệt không hỗ trợ đọc' },
    en: { play: 'Play', pause: 'Pause', replay: 'Replay', prev: 'Previous sentence', next: 'Next sentence', back: 'Back 10 seconds', fwd: 'Forward 10 seconds',
          speed: 'Speed', seek: 'Seek', sentence: 'Sentence', listen: 'Audio guide', hint: 'Tap any sentence to listen from there',
          unsupported: 'Your browser cannot read aloud' },
    ja: { play: '再生', pause: '一時停止', replay: 'もう一度', prev: '前の文', next: '次の文', back: '10秒戻る', fwd: '10秒進む',
          speed: '速度', seek: 'シーク', sentence: '文', listen: '音声ガイド', hint: '文をタップするとそこから再生します',
          unsupported: 'このブラウザは読み上げに対応していません' },
    ko: { play: '재생', pause: '일시정지', replay: '다시 듣기', prev: '이전 문장', next: '다음 문장', back: '10초 뒤로', fwd: '10초 앞으로',
          speed: '속도', seek: '탐색', sentence: '문장', listen: '오디오 가이드', hint: '문장을 누르면 그 부분부터 들을 수 있습니다',
          unsupported: '이 브라우저는 음성 읽기를 지원하지 않습니다' },
    zh: { play: '播放', pause: '暂停', replay: '重听', prev: '上一句', next: '下一句', back: '后退10秒', fwd: '前进10秒',
          speed: '语速', seek: '跳转', sentence: '句', listen: '收听讲解', hint: '点击任意句子即可从该处收听',
          unsupported: '此浏览器不支持朗读' }
  };
  var RATES = [0.75, 1, 1.25, 1.5, 2];

  var ICON = {
    play: '<path d="M8 5.5v13a1 1 0 0 0 1.5.86l10.5-6.5a1 1 0 0 0 0-1.72L9.5 4.64A1 1 0 0 0 8 5.5z"/>',
    pause: '<rect x="6.5" y="5" width="4" height="14" rx="1.2"/><rect x="13.5" y="5" width="4" height="14" rx="1.2"/>',
    replay: '<path d="M12 5V2L7 6l5 4V7a5 5 0 1 1-5 5H5a7 7 0 1 0 7-7z"/>',
    prev: '<path d="M6 5h2v14H6zM20 6.2v11.6a.8.8 0 0 1-1.2.7L10 12.7a.8.8 0 0 1 0-1.4l8.8-5.8a.8.8 0 0 1 1.2.7z"/>',
    next: '<path d="M16 5h2v14h-2zM4 6.2v11.6a.8.8 0 0 0 1.2.7L14 12.7a.8.8 0 0 0 0-1.4L5.2 5.5A.8.8 0 0 0 4 6.2z"/>',
    ear: '<path d="M12 3a8 8 0 0 0-8 8v6a3 3 0 0 0 3 3h1a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1H6v-1a6 6 0 0 1 12 0v1h-2a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h1a3 3 0 0 0 3-3v-6a8 8 0 0 0-8-8z"/>',
    back: '<path d="M12.5 4a8 8 0 1 1-7.9 9.3h2.1A6 6 0 1 0 12.5 6v3L8 5l4.5-4z"/><text x="12.6" y="16.2" font-size="6.6" font-weight="700" text-anchor="middle" font-family="system-ui,sans-serif">10</text>',
    fwd: '<path d="M11.5 4a8 8 0 1 0 7.9 9.3h-2.1A6 6 0 1 1 11.5 6v3L16 5l-4.5-4z"/><text x="11.4" y="16.2" font-size="6.6" font-weight="700" text-anchor="middle" font-family="system-ui,sans-serif">10</text>'
  };
  function svg(name, size) { size = size || 22; return '<svg viewBox="0 0 24 24" width="' + size + '" height="' + size + '" fill="currentColor" aria-hidden="true">' + ICON[name] + '</svg>'; }

  function fmt(sec) {
    sec = Math.max(0, Math.round(sec || 0));
    var m = Math.floor(sec / 60), s = sec % 60;
    return m + ':' + (s < 10 ? '0' : '') + s;
  }

  function isCjk(s) { return /[぀-ヿ㐀-鿿가-힯]/.test(s); }

  // Chia văn bản thành câu, giữ nguyên vị trí ký tự (để tô sáng câu đang đọc ngay trong bài).
  // Câu quá dài được cắt tiếp ở dấu phẩy / chấm phẩy (Chrome hay tự ngắt câu đọc dài quá ~15 giây).
  function splitSentences(text) {
    var out = [], start = 0, i, ch, n = text.length;
    function push(a, b) {
      while (a < b && /\s/.test(text[a])) a++;
      while (b > a && /\s/.test(text[b - 1])) b--;
      if (b <= a) return;
      if (!/[\p{L}\p{N}]/u.test(text.slice(a, b))) { if (out.length) out[out.length - 1].end = b; return; }
      out.push({ start: a, end: b });
    }
    for (i = 0; i < n; i++) {
      ch = text[i];
      if (ch === '\n') { push(start, i); start = i + 1; }
      else if ('。！？'.indexOf(ch) >= 0) { push(start, i + 1); start = i + 1; }
      else if ('.!?…'.indexOf(ch) >= 0) {
        var j = i + 1;
        while (j < n && '.!?…"\'”’)]»'.indexOf(text[j]) >= 0) j++;
        // Không ngắt ở số thập phân (3.5) hay chữ viết tắt dính liền (TP.HCM)
        if (j >= n || /\s/.test(text[j])) { push(start, j); start = j; i = j - 1; }
      }
    }
    push(start, n);

    var res = [];
    out.forEach(function (s) {
      var a = s.start;
      while (s.end - a > 220) {
        var chunk = text.slice(a, a + 220), cut = -1, m, re = /[,;:，、；：]\s*/g;
        while ((m = re.exec(chunk)) !== null) if (m.index > 60) cut = m.index + m[0].length;
        if (cut < 0) { cut = chunk.lastIndexOf(' '); if (cut < 60) cut = 220; }
        res.push({ start: a, end: a + cut });
        a += cut;
        while (a < s.end && /\s/.test(text[a])) a++;
      }
      if (a < s.end) res.push({ start: a, end: s.end });
    });
    return res;
  }

  function build(root) {
    var T = TEXT[(root.dataset.ui || 'vi').split('-')[0]] || TEXT.en;
    var src = root.dataset.src;
    var audioMode = !!src;
    if (!audioMode && !('speechSynthesis' in window)) {
      root.innerHTML = '<div class="cgp-unsupported">' + T.unsupported + '</div>';
      return;
    }

    root.innerHTML =
      '<div class="cgp-head"><span class="cgp-title">' + svg('ear', 17) + ' ' + T.listen + '</span>' +
      '<span class="cgp-time"><span class="cgp-cur">0:00</span> / <span class="cgp-total">0:00</span></span></div>' +
      '<input class="cgp-seek" type="range" min="0" max="1000" step="1" value="0" aria-label="' + T.seek + '">' +
      '<div class="cgp-ctrls">' +
        '<button type="button" class="cgp-rate" title="' + T.speed + '" aria-label="' + T.speed + '">1×</button>' +
        '<button type="button" class="cgp-btn cgp-prev" title="' + (audioMode ? T.back : T.prev) + '" aria-label="' + (audioMode ? T.back : T.prev) + '">' + svg(audioMode ? 'back' : 'prev') + '</button>' +
        '<button type="button" class="cgp-main" title="' + T.play + '" aria-label="' + T.play + '">' + svg('play') + '</button>' +
        '<button type="button" class="cgp-btn cgp-next" title="' + (audioMode ? T.fwd : T.next) + '" aria-label="' + (audioMode ? T.fwd : T.next) + '">' + svg(audioMode ? 'fwd' : 'next') + '</button>' +
        '<span class="cgp-pos"></span>' +
      '</div>' +
      (audioMode ? '' : '<div class="cgp-hint">' + T.hint + '</div>');

    var $ = function (c) { return root.querySelector(c); };
    var seek = $('.cgp-seek'), main = $('.cgp-main'), prev = $('.cgp-prev'), next = $('.cgp-next'),
        rateBtn = $('.cgp-rate'), cur = $('.cgp-cur'), total = $('.cgp-total'), pos = $('.cgp-pos');
    var rateIdx = 1, dragging = false;

    function setMain(state) {
      var label = state === 'pause' ? T.pause : state === 'replay' ? T.replay : T.play;
      main.innerHTML = svg(state);
      main.title = label; main.setAttribute('aria-label', label);
      root.classList.toggle('playing', state === 'pause');
      window.cgPlayerPlaying = state === 'pause';
      if (state === 'pause') setTimeout(window.cgPing || function () {}, 300);
    }
    function paintSeek() {
      var p = seek.max > 0 ? (seek.value / seek.max) * 100 : 0;
      seek.style.setProperty('--p', p + '%');
    }
    rateBtn.addEventListener('click', function () {
      rateIdx = (rateIdx + 1) % RATES.length;
      rateBtn.textContent = RATES[rateIdx] + '×';
      player.rate(RATES[rateIdx]);
    });
    main.addEventListener('click', function () { player.toggle(); });
    prev.addEventListener('click', function () { player.step(-1); });
    next.addEventListener('click', function () { player.step(1); });
    seek.addEventListener('input', function () { dragging = true; paintSeek(); player.preview(+seek.value); });
    seek.addEventListener('change', function () { dragging = false; player.seekTo(+seek.value); });

    var player = audioMode ? audioPlayer() : speechPlayer();
    player.init();

    // ---------- File âm thanh thật ----------
    function audioPlayer() {
      var a = new Audio();
      a.preload = 'metadata';
      a.src = src;
      function sync() {
        if (!isFinite(a.duration)) return;
        total.textContent = fmt(a.duration);
        if (!dragging) { seek.value = Math.round(a.currentTime * 10); cur.textContent = fmt(a.currentTime); paintSeek(); }
      }
      return {
        init: function () {
          a.addEventListener('loadedmetadata', function () { seek.max = Math.round(a.duration * 10) || 1000; sync(); });
          a.addEventListener('timeupdate', sync);
          a.addEventListener('play', function () { setMain('pause'); });
          a.addEventListener('pause', function () { setMain(a.ended ? 'replay' : 'play'); });
          a.addEventListener('ended', function () { setMain('replay'); });
          root.appendChild(a);
        },
        toggle: function () { if (a.paused) a.play().catch(function () {}); else a.pause(); },
        step: function (d) {
          var t = Math.min(Math.max(0, a.currentTime + d * 10), isFinite(a.duration) ? a.duration : a.currentTime + 10);
          a.currentTime = t; sync();
        },
        preview: function (v) { cur.textContent = fmt(v / 10); },
        seekTo: function (v) { a.currentTime = v / 10; sync(); },
        rate: function (r) { a.playbackRate = r; }
      };
    }

    // ---------- Giọng đọc của máy (Web Speech API), tua theo câu ----------
    function speechPlayer() {
      var synth = window.speechSynthesis;
      var lang = root.dataset.voice || 'vi-VN';
      var target = root.dataset.text ? document.querySelector(root.dataset.text) : null;
      var body = target ? target.textContent : '';
      var title = (root.dataset.title || '').trim();

      // Danh sách câu đọc: [tên điểm] + các câu trong bài. Mỗi câu có độ dài và vị trí ký tự cộng dồn.
      var items = [];
      if (title) items.push({ text: title, span: null });
      var spans = [];
      if (target && body.trim()) {
        var parts = splitSentences(body), frag = document.createDocumentFragment(), last = 0;
        parts.forEach(function (p) {
          if (p.start > last) frag.appendChild(document.createTextNode(body.slice(last, p.start)));
          var s = document.createElement('span');
          s.className = 'cgp-s';
          s.textContent = body.slice(p.start, p.end);
          frag.appendChild(s);
          spans.push(s);
          items.push({ text: s.textContent, span: s });
          last = p.end;
        });
        if (last < body.length) frag.appendChild(document.createTextNode(body.slice(last)));
        target.textContent = '';
        target.appendChild(frag);
        target.classList.add('cgp-text');
      }
      var acc = 0;
      items.forEach(function (it) { it.from = acc; acc += it.text.length + 1; });
      var totalChars = Math.max(1, acc);
      var cps = isCjk(body || title) ? 5.5 : 14;   // ký tự / giây ở tốc độ 1× – chỉ để ước lượng thời gian

      var idx = 0, offset = 0, playing = false, ended = false, gen = 0;
      var startedAt = 0, baseOffset = 0, boundary = -1, tickTimer = null, keepAlive = null;
      var rate = 1;

      function sec(chars) { return chars / (cps * rate); }
      function curChars() {
        var it = items[idx]; if (!it) return totalChars;
        var o = offset;
        if (playing) o = boundary >= 0 ? baseOffset + boundary : baseOffset + (Date.now() - startedAt) / 1000 * cps * rate;
        return it.from + Math.min(o, it.text.length);
      }
      function show() {
        total.textContent = '≈' + fmt(sec(totalChars));
        var c = curChars();
        if (!dragging) { seek.value = Math.round(c / totalChars * 1000); paintSeek(); cur.textContent = fmt(sec(c)); }
        pos.textContent = items.length ? T.sentence + ' ' + Math.min(idx + 1, items.length) + '/' + items.length : '';
      }
      function mark() {
        spans.forEach(function (s) { s.classList.remove('on'); });
        var it = items[idx];
        if (it && it.span && (playing || offset > 0 || idx > 0)) {
          it.span.classList.add('on');
          if (playing) {
            var r = it.span.getBoundingClientRect();
            if (r.top < 80 || r.bottom > window.innerHeight - 40) it.span.scrollIntoView({ block: 'center', behavior: 'smooth' });
          }
        }
      }
      function pickVoice() {
        var vs = synth.getVoices(), l = lang.toLowerCase(), b = l.split('-')[0];
        return vs.find(function (v) { return v.lang.toLowerCase().replace('_', '-') === l; })
            || vs.find(function (v) { return v.lang.toLowerCase().indexOf(b) === 0; });
      }
      function stopTimers() { clearInterval(tickTimer); clearInterval(keepAlive); tickTimer = keepAlive = null; }

      // Đọc câu idx, bắt đầu từ ký tự offset trong câu
      function speakCurrent() {
        var my = ++gen;
        synth.cancel();
        if (synth.paused) synth.resume();
        var it = items[idx];
        if (!it) { finish(); return; }
        // lùi về đầu từ để không đọc nửa chữ
        while (offset > 0 && !/\s/.test(it.text[offset - 1])) offset--;
        var txt = it.text.slice(offset);
        if (!txt.trim()) { idx++; offset = 0; speakCurrent(); return; }
        var u = new SpeechSynthesisUtterance(txt);
        u.lang = lang; u.rate = rate;
        var v = pickVoice(); if (v) u.voice = v;
        baseOffset = offset; boundary = -1;
        u.onstart = function () { if (my === gen) startedAt = Date.now(); };
        u.onboundary = function (e) { if (my === gen && typeof e.charIndex === 'number') boundary = e.charIndex; };
        u.onend = u.onerror = function (e) {
          if (my !== gen || !playing) return;
          if (e && e.type === 'error' && e.error && e.error !== 'interrupted' && e.error !== 'canceled') console.warn('TTS:', e.error);
          idx++; offset = 0;
          if (idx >= items.length) finish(); else { mark(); speakCurrent(); }
        };
        startedAt = Date.now();
        mark(); show();
        // Một số trình duyệt cần nhịp nhỏ sau cancel() mới chịu đọc tiếp
        setTimeout(function () { if (my === gen && playing) synth.speak(u); }, 60);
      }
      function play() {
        if (ended) { idx = 0; offset = 0; ended = false; }
        playing = true; setMain('pause');
        speakCurrent();
        stopTimers();
        tickTimer = setInterval(show, 250);
        // Chrome trên máy tính tự dừng sau ~15 giây nếu không "đánh thức"
        keepAlive = setInterval(function () { if (synth.speaking && !synth.paused) { synth.pause(); synth.resume(); } }, 10000);
      }
      function pause() {
        if (!playing) return;
        var it = items[idx];
        offset = it ? Math.max(0, Math.round(curChars() - it.from)) : 0;
        if (it && offset >= it.text.length - 2) { idx++; offset = 0; }
        playing = false; gen++; stopTimers();
        synth.cancel();
        setMain('play'); mark(); show();
      }
      function finish() {
        playing = false; ended = true; gen++; stopTimers();
        idx = items.length; offset = 0;
        spans.forEach(function (s) { s.classList.remove('on'); });
        setMain('replay'); show();
        idx = Math.max(0, items.length - 1);
        pos.textContent = items.length ? T.sentence + ' ' + items.length + '/' + items.length : '';
      }
      function go(i) {
        ended = false;
        idx = Math.min(Math.max(0, i), items.length - 1); offset = 0;
        if (playing) speakCurrent(); else { mark(); show(); setMain('play'); }
      }
      function indexAt(chars) {
        for (var i = items.length - 1; i >= 0; i--) if (chars >= items[i].from) return i;
        return 0;
      }

      return {
        init: function () {
          show();
          synth.getVoices();
          spans.forEach(function (s, k) {
            s.addEventListener('click', function () {
              var i = items.indexOf(items.filter(function (it) { return it.span === s; })[0]);
              go(i);
              if (!playing) play();
            });
          });
          // Rời trang / chuyển trang → ngừng đọc (tránh máy đọc tiếp khi đã sang điểm khác)
          window.addEventListener('pagehide', function () { gen++; synth.cancel(); });
        },
        toggle: function () { if (playing) pause(); else play(); },
        step: function (d) {
          if (d < 0 && playing && (Date.now() - startedAt) > 2500) { go(idx); return; } // đang đọc giữa câu → về đầu câu
          if (d < 0 && !playing && offset > 0) { go(idx); return; }
          if (d > 0 && idx >= items.length - 1) { if (playing) { gen++; finish(); synth.cancel(); } return; }
          go((ended ? items.length - 1 : idx) + d);
        },
        preview: function (v) {
          var c = v / 1000 * totalChars;
          cur.textContent = fmt(sec(c));
          pos.textContent = T.sentence + ' ' + (indexAt(c) + 1) + '/' + items.length;
        },
        seekTo: function (v) { go(indexAt(v / 1000 * totalChars)); },
        rate: function (r) {
          if (playing) { var it = items[idx]; offset = it ? Math.max(0, Math.round(curChars() - it.from)) : 0; }
          rate = r;
          if (playing) speakCurrent(); else show();
        }
      };
    }
  }

  function initAll() { document.querySelectorAll('.cg-player:not([data-ready])').forEach(function (el) { el.setAttribute('data-ready', ''); build(el); }); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initAll); else initAll();
})();
