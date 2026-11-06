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

    var pile = 'vurderes', filter = '', skip = [], item = null;
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
        fetch('/api/naeste?bunke=' + pile + '&brug=' + filter + '&spring=' + skip.join(','), { credentials: 'same-origin' })
            .then(function (r) { if (!r.ok) throw new Error('Lager svarer ikke lige nu – prøv igen om lidt.'); return r.json(); })
            .then(function (q) {
                $('vu-n-vurderes').textContent = q.assess; $('vu-n-ingen').textContent = q.noPrice;
                item = q.items && q.items[0];
                $('vu-card').hidden = !item; $('vu-empty').hidden = !!item;
                msg('');
                if (!item) return;
                $('vu-photo').src = '/api/foto/' + item.id;
                $('vu-line').textContent = item.line;
                $('vu-facts').textContent = [item.kind, item.condition ? 'stand: ' + item.condition : '', item.box ? 'kasse ' + item.box : ''].filter(Boolean).join(' · ');
                var ok = $('vu-ok');
                if (item.hintKr) { ok.hidden = false; ok.textContent = 'Ok – ' + item.hintKr + ' kr'; $('vu-hint').innerHTML = 'Forslag: <b>' + item.hintKr + ' kr</b> – 10 % under det, andre sider tager.'; }
                else { ok.hidden = true; $('vu-hint').innerHTML = item.similarKr ? 'Intet forslag. Noget, der ligner, koster omkring <b>' + item.similarKr + ' kr</b>.' : 'Intet forslag – der blev ikke fundet noget andre steder.'; }
                var nw = $('vu-new');
                if (item.newKr) { nw.hidden = false; nw.innerHTML = 'Ny hos ' + esc(item.newShop || 'boghandlen') + ': <b>' + item.newKr + ' kr</b> – til sammenligning.'; }
                else { nw.hidden = true; nw.textContent = ''; }
                showUsage();
                $('vu-price').value = '';
                var finds = item.finds || [];
                $('vu-finds-n').textContent = finds.length;
                $('vu-finds-box').hidden = !finds.length;
                $('vu-finds').innerHTML = finds.map(function (f) {
                    return '<li><a class="k-btn k-btn-sm" href="' + esc(f.url) + '" target="_blank" rel="noopener nofollow">' + esc(f.source) + (f.isNew ? ' (ny)' : '') + ': ' + f.kr + ' kr</a> <span class="k-help">' + esc(f.name) + '</span></li>';
                }).join('');
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
    $('vu-own').addEventListener('submit', function (e) { e.preventDefault(); if (item) save('/api/pris', { id: item.id, pris: $('vu-price').value }); });
    $('vu-skip').addEventListener('click', function () { if (item) { skip.push(item.id); load(); } });
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

    showPile(); load();
})();
