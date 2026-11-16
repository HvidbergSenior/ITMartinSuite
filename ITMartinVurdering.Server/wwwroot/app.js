// Vurdering - one thing at a time: Ok (the suggestion), own price, or Spring over. One click = one function.
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    // Mørk / Lys like the other helpers: the button says what you GET.
    function sync() { var b = $('themeToggleBtn'); if (b) b.textContent = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'Mørk' : 'Lys'; }
    document.addEventListener('click', function (e) {
        if (!e.target.closest || !e.target.closest('#themeToggleBtn')) return;
        var next = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem('vurdering_theme', next); } catch (err) { }
        sync();
    });
    sync();

    if (!$('vu-card')) return;   // the login page

    var pile = 'vurderes', filter = '', skip = [], item = null, query = '';
    try { pile = localStorage.getItem('vurdering_pile') || 'vurderes'; filter = localStorage.getItem('vurdering_filter') || ''; } catch (e) { }

    function msg(t, bad) { var m = $('vu-msg'); m.textContent = t || ''; m.classList.toggle('is-bad', !!bad); }

    function showPile() {
        document.querySelectorAll('.vu-piles [data-pile]').forEach(function (b) {
            var on = b.getAttribute('data-pile') === pile; b.classList.toggle('active', on); b.setAttribute('aria-selected', on ? 'true' : 'false');
        });
        document.querySelectorAll('.vu-filter [data-filter]').forEach(function (b) {
            var on = b.getAttribute('data-filter') === filter; b.classList.toggle('active', on); b.setAttribute('aria-selected', on ? 'true' : 'false');
        });
    }
    function showUsage() {
        document.querySelectorAll('.vu-usage [data-usage]').forEach(function (b) {
            var on = !!item && b.getAttribute('data-usage') === item.usage;
            b.classList.toggle('k-btn-primary', on); b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }

    function load() {
        msg('Henter …');
        // Søg (2026-11-16): while a search is on, the card shows its hits one at a time (Spring over = the next hit).
        var url = query ? '/api/soeg?q=' + encodeURIComponent(query) + '&spring=' + skip.join(',')
            : '/api/naeste?bunke=' + pile + '&brug=' + filter + '&spring=' + skip.join(',');
        fetch(url, { credentials: 'same-origin' })
            .then(function (r) { if (!r.ok) throw new Error('Lager svarer ikke lige nu – prøv igen om lidt.'); return r.json(); })
            .then(function (q) {
                $('vu-n-vurderes').textContent = q.assess; $('vu-n-ingen').textContent = q.noPrice;
                item = q.items && q.items[0];
                $('vu-card').hidden = !item; $('vu-empty').hidden = !!item || !!query;
                var unsure = q.unsure ? ' ' + q.unsure + ' usikre læsninger passer også – dem retter du i Lager under »Ret de svære«.' : '';
                msg(!item && query ? (skip.length ? 'Ikke flere, der passer på »' + query + '«.' : 'Fandt ingen sikre ting, der passer på »' + query + '«.') + unsure : (query && unsure && !skip.length ? unsure.trim() : ''));
                if (!item) return;
                $('vu-photo').src = '/api/foto/' + item.id + '?v=' + Date.now();
                endMark();
                $('vu-hidden-tag').hidden = !item.photoHidden;
                $('vu-hide-photo').textContent = item.photoHidden ? 'Vis billedet igen' : 'Skjul billedet';
                $('vu-uncover').hidden = !item.covered;
                $('vu-recut').hidden = !item.onPile;
                $('vu-line').textContent = item.line;
                $('vu-facts').textContent = ['nr. ' + item.id, item.kind, item.condition ? 'stand: ' + item.condition : '', item.box ? 'kasse ' + item.box : ''].filter(Boolean).join(' · ');
                var ok = $('vu-ok');
                if (item.hintKr) { ok.hidden = false; ok.textContent = 'Ok – ' + item.hintKr + ' kr'; $('vu-hint').innerHTML = (item.marketKr ? 'Fundet andre steder: ca. <b>' + item.marketKr + ' kr</b> → vores pris (' + esc(item.hintRule) + '): <b>' + item.hintKr + ' kr</b>.' : 'Forslag: <b>' + item.hintKr + ' kr</b>.'); }
                else { ok.hidden = true; $('vu-hint').innerHTML = item.similarKr ? 'Intet forslag. Noget, der ligner, koster omkring <b>' + item.similarKr + ' kr</b>.' : 'Intet forslag – der blev ikke fundet noget andre steder.'; }
                // The price written on the thing (2026-11-15: "agression book says 69 on it").
                var st = $('vu-sticker'), use = $('vu-use-sticker');
                st.hidden = use.hidden = !item.stickerKr;
                if (item.stickerKr) { st.innerHTML = 'Der står <b>' + item.stickerKr + ' kr</b> på den (et prismærke på billedet).'; use.textContent = 'Brug prisen på mærket – ' + item.stickerKr + ' kr'; }
                var nw = $('vu-new');
                if (item.newKr) { nw.hidden = false; nw.innerHTML = 'Ny hos ' + esc(item.newShop || 'boghandlen') + ': <b>' + item.newKr + ' kr</b> – til sammenligning.'; }
                else { nw.hidden = true; nw.textContent = ''; }
                showUsage();
                // Sjælden - vurderes manuelt (2026-11-16): the mark, the person's comment, and auction in Lager.
                $('vu-rare-tag').hidden = !item.rare;
                $('vu-note').value = item.note || '';
                $('vu-rare-toggle').textContent = item.rare ? 'Ikke sjælden' : 'Marker som sjælden';
                $('vu-auction').href = 'https://lager.bogshoppen.dk/vare/' + item.id;
                $('vu-price').value = '';
                // The same thing first; other issues of the series apart, as a pointer only (2026-11-16: "it finds three wrong titles").
                var all = item.finds || [];
                var same = all.filter(function (f) { return !f.similar; }), other = all.filter(function (f) { return f.similar; });
                function li(f) { return '<li><a class="k-btn k-btn-sm" href="' + esc(f.url) + '" target="_blank" rel="noopener nofollow">' + esc(f.source) + (f.isNew ? ' (ny)' : '') + ': ' + f.kr + ' kr</a> <span class="k-help">' + esc(f.name) + '</span></li>'; }
                $('vu-finds-n').textContent = same.length + (other.length ? ' + ' + other.length + ' andre numre' : '');
                $('vu-finds-box').hidden = !all.length;
                $('vu-finds').innerHTML = same.length ? same.map(li).join('') : '<li class="k-help">Netop denne ting er ikke fundet til salg.</li>';
                // Folded away (user 2026-11-16 "fold"): only opened when a guide is wanted.
                $('vu-similar-box').hidden = !other.length; $('vu-similar-box').open = false; $('vu-similar-n').textContent = other.length;
                $('vu-similar').innerHTML = other.map(li).join('');
            })
            .catch(function (e) { msg(e.message, true); });
    }

    function save(url, body) {
        msg('Gemmer …');
        fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) { if (!x.ok) { msg(x.j.fejl || 'Det virkede ikke.', true); return; } load(); setTimeout(function () { msg(x.j.besked); }, 300); })
            .catch(function () { msg('Det virkede ikke – prøv igen.', true); });
    }

    $('vu-ok').addEventListener('click', function () { if (item) save('/api/ok', { id: item.id, forslag: item.hintKr }); });
    $('vu-use-sticker').addEventListener('click', function () { if (item && item.stickerKr) save('/api/pris', { id: item.id, pris: String(item.stickerKr) }); });
    $('vu-own').addEventListener('submit', function (e) { e.preventDefault(); if (item) save('/api/pris', { id: item.id, pris: $('vu-price').value }); });
    // "Find priser nu" (2026-11-15, moved from Lager): every site is asked again - takes up to a minute.
    $('vu-find').addEventListener('click', function () {
        if (!item) return;
        var b = $('vu-find'); b.disabled = true; b.textContent = 'Spørger siderne … (op til et minut)'; msg('');
        fetch('/api/find', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: item.id }) })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) { if (!x.ok) { msg(x.j.fejl || 'Det virkede ikke.', true); return; } load(); setTimeout(function () { msg(x.j.besked); }, 300); })
            .catch(function () { msg('Det virkede ikke – prøv igen.', true); })
            .finally(function () { b.disabled = false; b.textContent = 'Find priser nu'; });
    });
    $('vu-skip').addEventListener('click', function () { if (item) { skip.push(item.id); load(); } });
    $('vu-note-save').addEventListener('click', function () {
        if (!item) return;
        fetch('/api/note', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: item.id, note: $('vu-note').value }) })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) { if (!x.ok) { msg(x.j.fejl || 'Det virkede ikke.', true); return; } item.note = $('vu-note').value; msg(x.j.besked); })
            .catch(function () { msg('Det virkede ikke – prøv igen.', true); });
    });
    $('vu-rare-toggle').addEventListener('click', function () { if (item) save('/api/sjaelden', { id: item.id, sjaelden: !item.rare }); });
    document.querySelectorAll('.vu-usage [data-usage]').forEach(function (b) {
        b.addEventListener('click', function () {
            if (!item) return;
            var u = b.getAttribute('data-usage');
            fetch('/api/brug', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: item.id, brug: u }) })
                .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
                .then(function (x) { if (!x.ok) { msg(x.j.fejl || 'Det virkede ikke.', true); return; } item.usage = u; showUsage(); msg(x.j.besked); })
                .catch(function () { msg('Det virkede ikke – prøv igen.', true); });
        });
    });
    document.querySelectorAll('.vu-filter [data-filter]').forEach(function (b) {
        b.addEventListener('click', function () { filter = b.getAttribute('data-filter'); skip = []; try { localStorage.setItem('vurdering_filter', filter); } catch (e) { } showPile(); load(); });
    });
    document.querySelectorAll('.vu-piles [data-pile]').forEach(function (b) {
        b.addEventListener('click', function () { pile = b.getAttribute('data-pile'); skip = []; try { localStorage.setItem('vurdering_pile', pile); } catch (e) { } showPile(); load(); });
    });

    // Kassér (2026-11-16): the thing leaves the stock (kept in Lager as Ikke købt); Fortryd brings it back.
    var discarded = null;
    $('vu-discard').addEventListener('click', function () {
        if (!item) return;
        var it = item;
        fetch('/api/kasser', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: it.id }) })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (x) {
                if (!x.ok) { msg(x.j.fejl || 'Det virkede ikke.', true); return; }
                discarded = { id: it.id, was: x.j.was };
                $('vu-undo-text').textContent = 'Kasseret: ' + it.line + '.'; $('vu-undo-row').hidden = false;
                skip.push(it.id); load();
            })
            .catch(function () { msg('Det virkede ikke – prøv igen.', true); });
    });
    $('vu-undo').addEventListener('click', function () {
        if (!discarded) return;
        var d = discarded; discarded = null; $('vu-undo-row').hidden = true;
        skip = skip.filter(function (x) { return x !== d.id; });
        save('/api/kasser/fortryd', { id: d.id, was: d.was });
    });

    // The photo (2026-11-16): drag a box over a price mark -> Lager paints over it; hide a bad photo from bogshoppen.dk.
    var marking = false, start = null, area = null, mode = 'mark';
    function endMark() {
        if (mode === 'cut' && item) $('vu-photo').src = '/api/foto/' + item.id + '?v=' + Date.now();
        $('vu-find-this').hidden = true; $('vu-photo-wrap').classList.remove('is-cut');
        marking = false; start = null; area = null; mode = 'mark';
        $('vu-recut').hidden = !(item && item.onPile);
        $('vu-mark-box').hidden = true; $('vu-mark-help').hidden = true; $('vu-mark-save').hidden = true; $('vu-mark-cancel').hidden = true;
        $('vu-photo-wrap').classList.remove('is-marking');
        $('vu-mark').hidden = !(item && item.ownPhoto);
    }
    function at(e) {
        var r = $('vu-photo').getBoundingClientRect();
        return { x: Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)), y: Math.min(1, Math.max(0, (e.clientY - r.top) / r.height)) };
    }
    function draw() {
        var img = $('vu-photo'), box = $('vu-mark-box'), w = img.clientWidth, h = img.clientHeight;
        box.style.left = (img.offsetLeft + area.l * w) + 'px'; box.style.top = (img.offsetTop + area.t * h) + 'px';
        box.style.width = ((area.r - area.l) * w) + 'px'; box.style.height = ((area.b - area.t) * h) + 'px';
        box.hidden = false;
    }
    function startMark(m) {
        mode = m; marking = true; $('vu-mark').hidden = true; $('vu-recut').hidden = true; $('vu-mark-cancel').hidden = false;
        $('vu-mark-help').innerHTML = m === 'cut'
            ? 'Her er hele billedet af bunken. <b>Træk en firkant rundt om tingen</b> – tryk så <b>Lav nyt billede</b>.'
            : '<b>Træk en firkant</b> hen over prismærket med fingeren eller musen – tryk så <b>Mal over mærket</b>.';
        $('vu-mark-save').textContent = m === 'cut' ? 'Lav nyt billede' : 'Mal over mærket';
        $('vu-mark-help').hidden = false;
        $('vu-photo-wrap').classList.add('is-marking');
    }
    $('vu-mark').addEventListener('click', function () { startMark('mark'); });
    // Which one is it? (user 2026-11-16: "but how can i see which one it is") - Lager does not keep where on the pile it was,
    // so: its name big, the reader's note (often "under X", "nederst til venstre"), the old cut-out, and the pile photo large.
    $('vu-recut').addEventListener('click', function () {
        if (!item) return;
        $('vu-find-line').textContent = item.line;
        $('vu-find-note').textContent = item.note ? 'Note: ' + item.note : '';
        var old = $('vu-old-cut'); old.hidden = !item.ownPhoto; if (item.ownPhoto) old.src = '/api/foto/' + item.id + '?v=' + Date.now();
        $('vu-find-this').hidden = false;
        $('vu-photo-wrap').classList.add('is-cut');
        startMark('cut');
        // The reader's own frame (2026-11-16, new photos): drawn at once, "Lav nyt billede" ready - drag a new one to change it.
        var pa = item.pileArea, img = $('vu-photo');
        img.onload = function () {
            img.onload = null;
            if (!pa || mode !== 'cut') return;
            area = { l: pa.left, t: pa.top, r: pa.right, b: pa.bottom }; draw();
            $('vu-mark-save').hidden = false;
            $('vu-mark-help').innerHTML = '<b>Rammen</b> er der, hvor tingen blev læst. Passer den, så tryk <b>Lav nyt billede</b> – ellers træk en ny firkant.';
        };
        img.src = '/api/bunkefoto/' + item.id;
    });
    $('vu-mark-cancel').addEventListener('click', endMark);
    $('vu-photo').addEventListener('pointerdown', function (e) {
        if (!marking) return; e.preventDefault(); start = at(e); $('vu-photo').setPointerCapture(e.pointerId);
    });
    $('vu-photo').addEventListener('pointermove', function (e) {
        if (!marking || !start) return; var p = at(e);
        area = { l: Math.min(start.x, p.x), t: Math.min(start.y, p.y), r: Math.max(start.x, p.x), b: Math.max(start.y, p.y) };
        draw();
    });
    $('vu-photo').addEventListener('pointerup', function () {
        if (!marking || !start) return; start = null;
        $('vu-mark-save').hidden = !(area && area.r - area.l > 0.005 && area.b - area.t > 0.005);
    });
    $('vu-mark-save').addEventListener('click', function () {
        if (item && area) save(mode === 'cut' ? '/api/udsnit' : '/api/maerke', { id: item.id, venstre: area.l, top: area.t, hoejre: area.r, bund: area.b });
    });
    $('vu-uncover').addEventListener('click', function () { if (item) save('/api/maerke/fortryd', { id: item.id }); });
    $('vu-hide-photo').addEventListener('click', function () { if (item) save('/api/skjulfoto', { id: item.id, skjul: !item.photoHidden }); });

    $('vu-search').addEventListener('submit', function (e) {
        e.preventDefault(); query = $('vu-q').value.trim(); skip = []; $('vu-q-clear').hidden = !query; load();
    });
    $('vu-q-clear').addEventListener('click', function () { query = ''; $('vu-q').value = ''; skip = []; $('vu-q-clear').hidden = true; load(); });

    showPile(); load();
})();
