// Guld, sølv og bronze (2026-11-15, moved from Lager): search + tier, newest list from Lager.
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    var all = [], tier = '';
    function show() {
        var words = $('gu-q').value.toLowerCase().split(/\s+/).filter(Boolean);
        var hits = all.filter(function (e) {
            var text = (e.category + ' ' + e.issue + ' ' + e.name + ' ' + e.tier + ' ' + e.type).toLowerCase();
            return (!tier || e.tier === tier) && words.every(function (w) { return text.indexOf(w) >= 0; });
        });
        document.querySelectorAll('[data-tier]').forEach(function (b) {
            var on = b.getAttribute('data-tier') === tier;
            b.classList.toggle('k-btn-primary', on); b.setAttribute('aria-selected', on ? 'true' : 'false');
        });
        $('gu-list').innerHTML = hits.slice(0, 200).map(function (e) {
            return '<li><a href="' + esc(e.url) + '" target="_blank" rel="noopener"><b>' + e.kr + ' kr</b> · ' + esc(e.badge) + ' · ' + esc(e.category) + ' ' + esc(e.issue)
                + '<span class="k-help">' + esc(e.name) + ' · ' + esc(e.source) + (e.listings > 1 ? ' · ' + e.listings + ' annoncer' : '') + '</span></a></li>';
        }).join('') || '<li class="k-help">Intet passer. Prøv færre ord.</li>';
        $('gu-info').textContent = all.length ? hits.length + ' vist.' : '';
    }
    fetch('/api/guld', { credentials: 'same-origin' })
        .then(function (r) { if (!r.ok) throw new Error('Lager svarer ikke lige nu – prøv igen om lidt.'); return r.json(); })
        .then(function (list) {
            all = list;
            document.querySelectorAll('[data-n]').forEach(function (s) { var t = s.getAttribute('data-n'); s.textContent = all.filter(function (e) { return !t || e.tier === t; }).length; });
            if (!all.length) $('gu-info').textContent = 'Listen bygges – kom tilbage om lidt.';
            show();
        })
        .catch(function (e) { $('gu-info').textContent = e.message; });
    $('gu-q').addEventListener('input', show);
    document.querySelectorAll('[data-tier]').forEach(function (b) { b.addEventListener('click', function () { tier = b.getAttribute('data-tier'); show(); }); });
})();
