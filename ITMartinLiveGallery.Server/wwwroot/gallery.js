(function () {
  var slug = location.pathname.replace(/^\/+/, '').split('?')[0];
  var params = new URLSearchParams(location.search);
  var pin = params.get('pin') || sessionStorage.getItem('gallery_pin_' + slug) || '';
  if (params.get('pin')) sessionStorage.setItem('gallery_pin_' + slug, pin);

  var grid = document.getElementById('grid');
  var empty = document.getElementById('empty');
  var titleEl = document.getElementById('eventTitle');
  var fp = '';
  var timer = null;

  // Same setTimeout-recursion + fingerprint-skip pattern as ITMartinLive.Server's
  // live.js - cheap way to avoid re-rendering the whole grid every poll when
  // nothing changed, without needing SignalR (which the Cloudflare Tunnel this
  // runs behind can't carry - see feedback memory on why).
  function poll() {
    fetch('/api/photos/' + encodeURIComponent(slug) + '?pin=' + encodeURIComponent(pin))
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (data) {
        if (data) {
          var thisFp = data.photos.length + '|' + (data.photos[0] ? data.photos[0].id : '');
          if (thisFp !== fp) { fp = thisFp; render(data); }
        }
      })
      .catch(function () { /* ignore, retry next tick */ })
      .finally(function () { timer = setTimeout(poll, 4000); });
  }

  function render(data) {
    if (data.title) titleEl.textContent = data.title;
    empty.style.display = data.photos.length === 0 ? 'block' : 'none';
    grid.innerHTML = data.photos.map(function (p) {
      var thumb = p.thumbUrl || p.url;
      var badge = p.isVideo ? '<span class="badge">▶</span>' : '';
      var who = p.uploaderName ? '<span class="who">' + escapeHtml(p.uploaderName) + '</span>' : '';
      return '<div class="cell" onclick="openLightbox(\'' + p.url + '\', ' + p.isVideo + ')">' +
             '<img src="' + thumb + '" loading="lazy" />' + badge + who + '</div>';
    }).join('');
  }

  function escapeHtml(s) {
    return s.replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  window.openLightbox = function (url, isVideo) {
    var box = document.getElementById('lightbox');
    var img = document.getElementById('lightboxImg');
    var vid = document.getElementById('lightboxVideo');
    if (isVideo) {
      img.style.display = 'none';
      vid.style.display = 'block';
      vid.src = url;
      vid.play();
    } else {
      vid.style.display = 'none';
      vid.pause();
      img.style.display = 'block';
      img.src = url;
    }
    box.classList.add('open');
  };
  window.closeLightbox = function () {
    document.getElementById('lightbox').classList.remove('open');
    document.getElementById('lightboxVideo').pause();
  };

  // ── Upload ──────────────────────────────────────────────────────────────
  var form = document.getElementById('uploadForm');
  var fileInput = document.getElementById('fileInput');
  var filepickLabel = document.getElementById('filepickLabel');
  var status = document.getElementById('uploadStatus');
  var uploadBtn = document.getElementById('uploadBtn');

  fileInput.addEventListener('change', function () {
    var n = fileInput.files.length;
    filepickLabel.textContent = n > 0 ? n + ' fil(er) valgt' : '📷 Vælg eller tag billeder/film';
  });

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var files = fileInput.files;
    if (!files.length) { status.textContent = 'Vælg mindst én fil først'; return; }

    uploadBtn.disabled = true;
    var name = document.getElementById('nameInput').value;
    var done = 0, failed = 0;
    status.textContent = 'Deler ' + files.length + ' fil(er)...';

    Array.prototype.forEach.call(files, function (file) {
      var fd = new FormData();
      fd.append('pin', pin);
      fd.append('name', name);
      fd.append('file', file);
      fetch('/api/upload/' + encodeURIComponent(slug), { method: 'POST', body: fd })
        .then(function (r) { if (!r.ok) failed++; })
        .catch(function () { failed++; })
        .finally(function () {
          done++;
          status.textContent = 'Deler... (' + done + '/' + files.length + ')';
          if (done === files.length) {
            status.textContent = failed
              ? failed + ' af ' + files.length + ' fejlede - prøv igen'
              : 'Delt! Tak 🎉';
            uploadBtn.disabled = false;
            fileInput.value = '';
            filepickLabel.textContent = '📷 Vælg eller tag billeder/film';
            fp = ''; // force an immediate re-render on next poll
            clearTimeout(timer);
            setTimeout(poll, 300);
            setTimeout(function () { status.textContent = ''; }, 3000);
          }
        });
    });
  });

  poll();
})();
