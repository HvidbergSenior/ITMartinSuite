// /faste: Martin adds weekly deals that are in no leaflet. The PIN is kept on this device only (localStorage, try/catch).
(function () {
    'use strict';
    var $ = function (id) { return document.getElementById(id); };
    var DAYS = ['', 'mandag', 'tirsdag', 'onsdag', 'torsdag', 'fredag', 'lørdag', 'søndag'];
    var pin = '';
    try { pin = localStorage.getItem('tilbud:pin') || ''; } catch (e) { }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    function days(d) { return !d || !d.length ? 'hver dag' : 'hver ' + d.map(function (x) { return DAYS[x]; }).join(' og '); }

    function load() {
        fetch('/api/faste').then(function (r) { return r.json(); }).then(function (list) {
            $('faste-liste').innerHTML = list.length ? list.map(function (d) {
                return '<div class="tb-mine"><div class="tb-mine-head"><b>' + esc(d.item) + '</b>' +
                    (pin ? '<button class="tb-x" type="button" data-id="' + esc(d.id) + '">✕ Slet</button>' : '') + '</div>' +
                    '<div>' + esc(d.chain) + ' – ' + days(d.days) + (d.price ? ' – <b>' + String(d.price).replace('.', ',') + ' kr</b>' + (d.unit ? ' pr. ' + esc(d.unit) : '') : '') + '</div>' +
                    (d.needsApp ? '<div class="tb-needs">📱 Kræver ' + esc(d.needsApp) + '</div>' : '') +
                    (d.note ? '<div class="k-muted">' + esc(d.note) + '</div>' : '') + '</div>';
            }).join('') : '<p class="k-muted">Ingen faste tilbud endnu.</p>';
            $('faste-liste').querySelectorAll('.tb-x').forEach(function (b) {
                b.addEventListener('click', function () {
                    if (b.dataset.sure !== '1') { b.dataset.sure = '1'; b.textContent = 'Sikker? Tryk igen'; return; }
                    fetch('/api/faste/' + b.dataset.id, { method: 'DELETE', headers: { 'X-Pin': pin } }).then(load);
                });
            });
        });
    }

    function loggedIn(ok) {
        $('ny-kort').hidden = !ok;
        $('pin-form').hidden = ok;
        load();
    }
    function check(p) {
        return fetch('/api/faste/tjek', { method: 'POST', headers: { 'X-Pin': p } }).then(function (r) { return r.ok; });
    }
    $('pin-form').addEventListener('submit', function (e) {
        e.preventDefault();
        var p = $('pin').value.trim();
        check(p).then(function (ok) {
            $('pin-fejl').hidden = ok; $('pin-fejl').textContent = 'Forkert PIN-kode.';
            if (ok) { pin = p; try { localStorage.setItem('tilbud:pin', p); } catch (e2) { } }
            loggedIn(ok);
        });
    });

    $('ny-form').addEventListener('submit', function (e) {
        e.preventDefault();
        var body = {
            item: $('f-vare').value, chain: $('f-butik').value,
            days: Array.prototype.filter.call(document.querySelectorAll('input[name=dag]'), function (c) { return c.checked; }).map(function (c) { return +c.value; }),
            price: $('f-pris').value ? parseFloat($('f-pris').value.replace(',', '.')) : null,
            unit: $('f-enhed').value, needsApp: $('f-app').value, note: $('f-note').value
        };
        fetch('/api/faste', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Pin': pin }, body: JSON.stringify(body) })
            .then(function (r) { return r.json().catch(function () { return {}; }).then(function (j) { if (!r.ok) throw new Error(j.fejl || 'Kunne ikke gemme.'); return j; }); })
            .then(function () { $('ny-info').textContent = '✓ Gemt: ' + body.item + ' i ' + body.chain + ', ' + days(body.days) + '.'; $('ny-form').reset(); load(); })
            .catch(function (err) { $('ny-info').textContent = err.message; });
    });

    if (pin) check(pin).then(loggedIn); else load();
})();
