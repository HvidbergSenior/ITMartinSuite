// Ny i kiosken (2026-11-15, moved from Lager): one price per blad - the same name again updates it.
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function msg(t, bad) { var m = $('ki-msg'); m.textContent = t || ''; m.classList.toggle('is-bad', !!bad); }
    var prices = [];
    function load() {
        fetch('/api/kiosk', { credentials: 'same-origin' })
            .then(function (r) { if (!r.ok) throw new Error('Lager svarer ikke lige nu – prøv igen om lidt.'); return r.json(); })
            .then(function (list) {
                prices = list;
                $('ki-list').innerHTML = list.map(function (p) {
                    return '<li><b>' + esc(p.series) + ' – ' + p.kr + ' kr</b>' + (p.note ? '<span class="k-help">' + esc(p.note) + '</span>' : '')
                        + '<span class="ki-btns"><button type="button" class="k-btn k-btn-sm" data-ret="' + p.id + '">Ret</button>'
                        + ' <button type="button" class="k-btn k-btn-sm k-btn-ghost" data-slet="' + p.id + '">Slet</button></span></li>';
                }).join('') || '<li class="k-help">Der er ingen priser endnu.</li>';
            })
            .catch(function (e) { msg(e.message, true); });
    }
    function post(url, body) {
        return fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}) })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); });
    }
    $('ki-form').addEventListener('submit', function (e) {
        e.preventDefault();
        post('/api/kiosk', { blad: $('ki-blad').value, pris: $('ki-pris').value, hvorfra: $('ki-hvor').value })
            .then(function (x) { if (!x.ok) { msg(x.j.fejl || 'Det virkede ikke.', true); return; } msg(x.j.besked); $('ki-form').reset(); load(); })
            .catch(function () { msg('Det virkede ikke – prøv igen.', true); });
    });
    $('ki-list').addEventListener('click', function (e) {
        var ret = e.target.getAttribute('data-ret'), slet = e.target.getAttribute('data-slet');
        if (ret) {
            var p = prices.filter(function (x) { return String(x.id) === ret; })[0];
            if (p) { $('ki-blad').value = p.series; $('ki-pris').value = p.kr; $('ki-hvor').value = p.note; $('ki-pris').focus(); }
        } else if (slet) {
            post('/api/kiosk/' + slet + '/slet').then(function (x) { if (!x.ok) { msg(x.j.fejl || 'Det virkede ikke.', true); return; } msg(x.j.besked); load(); });
        }
    });
    load();
})();
