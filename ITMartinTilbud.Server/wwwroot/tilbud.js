// Tilbud: place (postcode or GPS), search, and "Mine varer" - all kept in this browser (localStorage, wrapped in
// try/catch: private windows and blocked storage must still work, just without remembering).
(function () {
    'use strict';
    var $ = function (id) { return document.getElementById(id); };
    var KEY = 'tilbud:v1';
    var state = load();
    var lastQuery = '';

    function load() {
        try { var s = JSON.parse(localStorage.getItem(KEY) || '{}'); return { place: s.place || null, km: s.km || 10, mine: s.mine || [] }; }
        catch (e) { return { place: null, km: 10, mine: [] }; }
    }
    function save() { try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { } }

    var kr = new Intl.NumberFormat('da-DK', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    var day = new Intl.DateTimeFormat('da-DK', { weekday: 'short', day: 'numeric', month: 'numeric' });
    function money(n) { return kr.format(n) + ' kr'; }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    // ---- place
    function showPlace() {
        var p = state.place;
        $('sted-nu').hidden = !p;
        if (p) $('sted-nu').innerHTML = '✅ Butikker inden for <b>' + state.km + ' km</b> fra <b>' + esc(p.name) + '</b>. <span class="k-muted">Skift herunder.</span>';
        $('km').value = String(state.km);
    }
    function placeError(msg) { $('sted-fejl').textContent = msg; $('sted-fejl').hidden = !msg; }

    $('sted-form').addEventListener('submit', function (e) {
        e.preventDefault();
        var pn = $('postnr').value.replace(/\D/g, '');
        if (pn.length !== 4) { placeError('Et postnummer har 4 tal.'); return; }
        placeError('');
        fetch('/api/sted?postnr=' + pn).then(function (r) { return r.ok ? r.json() : null; }).then(function (p) {
            if (!p) { placeError('Det postnummer kender vi ikke. Prøv igen, eller tryk 📍 Brug min placering.'); return; }
            state.place = { name: p.name, lat: p.lat, lng: p.lng }; save(); showPlace(); refresh();
        }).catch(function () { placeError('Kunne ikke slå postnummeret op. Prøv igen om lidt.'); });
    });
    $('gps').addEventListener('click', function () {
        if (!navigator.geolocation) { placeError('Din browser kan ikke finde din placering. Skriv dit postnummer i stedet.'); return; }
        placeError('');
        navigator.geolocation.getCurrentPosition(function (pos) {
            state.place = { name: 'din placering', lat: +pos.coords.latitude.toFixed(3), lng: +pos.coords.longitude.toFixed(3) };
            save(); showPlace(); refresh();
        }, function () {
            // Facebook/Messenger/Instagram's own browser blocks location - say so instead of failing silently.
            var inApp = /FBAN|FBAV|Instagram|Messenger/i.test(navigator.userAgent);
            placeError(inApp ? 'Facebooks egen browser giver ikke lov til placering. Tryk ⋯ og vælg "Åbn i Chrome" – eller skriv dit postnummer.'
                : 'Du sagde nej til placering (eller den kunne ikke findes). Skriv dit postnummer i stedet.');
        }, { timeout: 15000, maximumAge: 600000 });
    });
    $('km').addEventListener('change', function () { state.km = +$('km').value; save(); showPlace(); refresh(); });

    // ---- offers
    function getOffers(q) {
        var p = state.place;
        return fetch('/api/tilbud?q=' + encodeURIComponent(q) + '&lat=' + p.lat + '&lng=' + p.lng + '&km=' + state.km)
            .then(function (r) { return r.json().then(function (j) { if (!r.ok) throw new Error(j.fejl || 'Fejl'); return j; }); });
    }
    function offerHtml(o) {
        var unit = o.unitPrice != null ? '<span class="tb-unit">' + money(o.unitPrice).replace(' kr', '') + ' ' + esc(o.unitLabel) + '</span>' : '';
        var before = o.normalPrice && o.normalPrice > o.price ? '<span class="tb-before">før ' + money(o.normalPrice) + '</span>' : '';
        var till = o.validTo ? '<span class="k-muted">til ' + day.format(new Date(o.validTo)) + '</span>' : '';
        var img = o.image ? '<img class="tb-img" src="' + esc(o.image) + '" alt="" loading="lazy">' : '<div class="tb-img"></div>';
        return '<div class="tb-offer">' + img + '<div class="tb-offer-body">' +
            '<div class="tb-chain">' + esc(o.chain) + '</div>' +
            '<div class="tb-title">' + esc(o.title) + (o.size ? ' <span class="k-muted">· ' + esc(o.size) + '</span>' : '') + '</div>' +
            '<div class="tb-prices"><span class="tb-price">' + money(o.price) + '</span>' + unit + before + till + '</div>' +
            (o.description ? '<div class="tb-desc k-muted">' + esc(o.description) + '</div>' : '') +
            '</div></div>';
    }

    $('soeg-form').addEventListener('submit', function (e) { e.preventDefault(); search($('q').value); });
    function search(q) {
        q = q.trim();
        if (q.length < 2) return;
        if (!state.place) { placeError('Vælg først hvor du handler – postnummer eller 📍.'); $('postnr').focus(); return; }
        lastQuery = q;
        $('soeg-info').textContent = 'Henter tilbud på "' + q + '" …';
        $('resultater').innerHTML = '';
        $('gem').hidden = true;
        getOffers(q).then(function (list) {
            $('gem').hidden = false;
            $('gem').textContent = state.mine.indexOf(q.toLowerCase()) >= 0 ? '✓ Gemt' : '⭐ Gem "' + q + '"';
            if (!list.length) { $('soeg-info').textContent = 'Ingen tilbud på "' + q + '" i denne uge inden for ' + state.km + ' km. Prøv et andet ord eller længere afstand.'; return; }
            var chains = {}; list.forEach(function (o) { chains[o.chain] = 1; });
            $('soeg-info').textContent = list.length + ' tilbud fra ' + Object.keys(chains).length + ' kæder – billigste pr. kg/liter øverst.';
            var shown = 25;
            function draw() {
                $('resultater').innerHTML = list.slice(0, shown).map(offerHtml).join('') +
                    (list.length > shown ? '<button class="k-btn k-btn-secondary tb-more" type="button">Vis ' + Math.min(25, list.length - shown) + ' mere</button>' : '');
                var more = $('resultater').querySelector('.tb-more');
                if (more) more.addEventListener('click', function () { shown += 25; draw(); });
            }
            draw();
        }).catch(function (err) { $('soeg-info').textContent = err.message; });
    }
    $('gem').addEventListener('click', function () {
        var q = lastQuery.toLowerCase();
        if (q && state.mine.indexOf(q) < 0) { state.mine.push(q); save(); refresh(); }
        $('gem').textContent = '✓ Gemt';
    });

    // ---- Mine varer: the best offer per item, the 2 next on request
    function refresh() {
        var box = $('mine-liste');
        $('mine-tom').hidden = state.mine.length > 0;
        if (!state.mine.length) { box.innerHTML = ''; return; }
        if (!state.place) { box.innerHTML = '<p class="k-muted">Vælg først hvor du handler.</p>'; return; }
        box.innerHTML = state.mine.map(function (q, i) {
            return '<div class="tb-mine" id="mine-' + i + '"><div class="tb-mine-head"><b>' + esc(q) + '</b>' +
                '<button class="tb-x" type="button" data-i="' + i + '" aria-label="Fjern ' + esc(q) + '">✕ Fjern</button></div>' +
                '<div class="tb-mine-body k-muted">Henter …</div></div>';
        }).join('');
        box.querySelectorAll('.tb-x').forEach(function (b) {
            b.addEventListener('click', function () { state.mine.splice(+b.dataset.i, 1); save(); refresh(); });
        });
        // Two at a time - kind to the offer service, still quick.
        var queue = state.mine.map(function (q, i) { return [q, i]; });
        function next() {
            var job = queue.shift(); if (!job) return;
            var body = document.querySelector('#mine-' + job[1] + ' .tb-mine-body');
            getOffers(job[0]).then(function (list) {
                if (!body) return;
                if (!list.length) { body.textContent = 'Ikke på tilbud i denne uge.'; return; }
                body.classList.remove('k-muted');
                var rest = list.length - 1;
                body.innerHTML = offerHtml(list[0]) + (rest > 0
                    ? '<details class="tb-more-offers"><summary>' + (rest > 5 ? 'De 5 næste (af ' + rest + ' andre)' : rest + ' andre tilbud') + '</summary>' +
                      list.slice(1, 6).map(offerHtml).join('') +
                      (rest > 5 ? '<button class="k-btn k-btn-secondary tb-all" type="button" data-q="' + esc(job[0]) + '">Se alle ' + list.length + ' tilbud</button>' : '') +
                      '</details>' : '');
                var all = body.querySelector('.tb-all');
                if (all) all.addEventListener('click', function () { $('q').value = all.dataset.q; search(all.dataset.q); $('soeg-kort').scrollIntoView({ behavior: 'smooth' }); });
            }).catch(function (err) { if (body) body.textContent = err.message; }).then(next);
        }
        next(); next();
    }

    showPlace();
    refresh();
})();
