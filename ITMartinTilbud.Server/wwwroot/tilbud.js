// Tilbud: place (postcode or GPS), search, and "Mine varer" - all kept in this browser (localStorage, wrapped in
// try/catch: private windows and blocked storage must still work, just without remembering).
(function () {
    'use strict';
    var $ = function (id) { return document.getElementById(id); };
    var KEY = 'tilbud:v1';
    var state = load();
    var lastQuery = '';

    function load() {
        try { var s = JSON.parse(localStorage.getItem(KEY) || '{}'); return { place: s.place || null, km: s.km || 10, mine: s.mine || [], apps: s.apps || [] }; }
        catch (e) { return { place: null, km: 10, mine: [], apps: [] }; }
    }
    function save() { try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { } }

    var kr = new Intl.NumberFormat('da-DK', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    var day = new Intl.DateTimeFormat('da-DK', { weekday: 'short', day: 'numeric', month: 'numeric' });
    function money(n) { return kr.format(n) + ' kr'; }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    // ---- the shops' own apps: their coupons are personal, so we can only remind
    var APP = { 'Lidl': 'Lidl', 'Kvickly': 'Coop', 'SuperBrugsen': 'Coop', 'Brugsen': 'Coop', "Dagli'Brugsen": 'Coop', '365discount': 'Coop',
        'Coop 365': 'Coop', 'Netto': 'Salling', 'føtex': 'Salling', 'Bilka': 'Salling', 'REMA 1000': 'REMA 1000' };
    var APP_NAME = { 'Lidl': 'Lidl Plus', 'Coop': 'Coop-appen', 'Salling': 'Salling Group-appen', 'REMA 1000': 'REMA 1000-appen' };
    document.querySelectorAll('.tb-apps input').forEach(function (cb) {
        cb.checked = state.apps.indexOf(cb.value) >= 0;
        cb.addEventListener('change', function () {
            state.apps = Array.prototype.filter.call(document.querySelectorAll('.tb-apps input'), function (c) { return c.checked; }).map(function (c) { return c.value; });
            save(); refresh();
        });
    });

    // ---- sharing: the phone's own share sheet (SMS, Messenger, mail), or copy when there is none
    var shareCount = 0, shared = {};
    function shareBtn(text, q) {
        var id = 's' + (++shareCount);
        shared[id] = { text: text, url: location.origin + '/' + (q ? '?q=' + encodeURIComponent(q) : '') };
        return '<button class="tb-share" type="button" data-s="' + id + '">📤 Del</button>';
    }
    document.addEventListener('click', function (e) {
        var b = e.target.closest && e.target.closest('.tb-share'); if (!b) return;
        var s = shared[b.dataset.s]; if (!s) return;
        if (navigator.share) { navigator.share({ title: 'Tilbud', text: s.text, url: s.url }).catch(function () { }); return; }
        var all = s.text + ' ' + s.url;
        (navigator.clipboard ? navigator.clipboard.writeText(all) : Promise.reject()).then(function () { b.textContent = '✓ Kopieret'; },
            function () { window.prompt('Kopiér teksten:', all); });
    });

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
        // Browsers give the location only on a secure (https) address - on http://10.0.0.200:9150 it is always "no".
        if (!window.isSecureContext) { placeError('Placering virker kun på den sikre adresse (https://tilbud.itmartin.dk). Skriv dit postnummer i stedet.'); return; }
        if (!navigator.geolocation) { placeError('Din browser kan ikke finde din placering. Skriv dit postnummer i stedet.'); return; }
        placeError('');
        navigator.geolocation.getCurrentPosition(function (pos) {
            state.place = { name: 'din placering', lat: +pos.coords.latitude.toFixed(3), lng: +pos.coords.longitude.toFixed(3) };
            save(); showPlace(); refresh();
        }, function () {
            // Facebook/Messenger/Instagram's own browser blocks location - say so instead of failing silently.
            var inApp = /FBAN|FBAV|Instagram|Messenger/i.test(navigator.userAgent);
            placeError(inApp ? 'Facebooks egen browser giver ikke lov til placering. Tryk ⋯ og vælg "Åbn i Chrome" – eller skriv dit postnummer.'
                : 'Telefonen gav ikke din placering. Tjek at Placering er slået til for browseren (Indstillinger → Apps → Chrome → Tilladelser → Placering) – eller skriv dit postnummer i stedet.');
        }, { timeout: 15000, maximumAge: 600000 });
    });
    $('km').addEventListener('change', function () { state.km = +$('km').value; save(); showPlace(); refresh(); });

    // ---- offers
    function getOffers(q) {
        var p = state.place;
        return fetch('/api/tilbud?q=' + encodeURIComponent(q) + '&lat=' + p.lat + '&lng=' + p.lng + '&km=' + state.km)
            .then(function (r) { return r.json().then(function (j) { if (!r.ok) throw new Error(j.fejl || 'Fejl'); return j; }); });
    }
    function offerHtml(o, q) {
        var unit = o.unitPrice != null ? '<span class="tb-unit">' + money(o.unitPrice).replace(' kr', '') + ' ' + esc(o.unitLabel) + '</span>' : '';
        var before = o.normalPrice && o.normalPrice > o.price ? '<span class="tb-before">før ' + money(o.normalPrice) + '</span>' : '';
        var till = o.validTo ? '<span class="k-muted">til ' + day.format(new Date(o.validTo)) + '</span>' : '';
        var img = o.image ? '<img class="tb-img" src="' + esc(o.image) + '" alt="" loading="lazy">' : '<div class="tb-img"></div>';
        return '<div class="tb-offer">' + img + '<div class="tb-offer-body">' +
            '<div class="tb-chain">' + esc(o.chain) + '</div>' +
            '<div class="tb-title">' + esc(o.title) + (o.size ? ' <span class="k-muted">· ' + esc(o.size) + '</span>' : '') + '</div>' +
            '<div class="tb-prices"><span class="tb-price">' + money(o.price) + '</span>' + unit + before + till + '</div>' +
            (o.description ? '<div class="tb-desc k-muted">' + esc(o.description) + '</div>' : '') +
            (APP[o.chain] && state.apps.indexOf(APP[o.chain]) >= 0 ? '<div class="tb-app">📱 Åbn ' + APP_NAME[APP[o.chain]] + ' – der kan være en kupon oveni.</div>' : '') +
            shareBtn(o.title + ' – ' + money(o.price) + (o.unitPrice != null ? ' (' + money(o.unitPrice).replace(' kr', '') + ' ' + o.unitLabel + ')' : '') +
                ' i ' + o.chain + (o.validTo ? ', til ' + day.format(new Date(o.validTo)) : '') + '.', q || lastQuery) +
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
        $('kinder').innerHTML = '';
        $('gem').hidden = true;
        getOffers(q).then(function (all) {
            if (!all.length) { $('soeg-info').textContent = 'Ingen tilbud på "' + q + '" i denne uge inden for ' + state.km + ' km. Prøv et andet ord eller længere afstand.'; return; }
            // Categories from the words that hold the search (user 2026-10-06: "push you toward the correct category"):
            // "mælk" -> mælk · skummetmælk · kakaomælk · tykmælk. Tapping one shows only that kind, and ⭐ saves that kind.
            var w = q.toLowerCase().split(/\s+/)[0];
            function kindOf(o) {
                var tok = o.title.toLowerCase().split(/[\s,.;:()&/+*]+/).filter(function (t) { return t.indexOf(w) >= 0; })[0];
                return tok ? tok.replace(/-/g, '').replace(/[^a-zæøå0-9]+$/, '') : '';
            }
            var kinds = {}; all.forEach(function (o) { var k = kindOf(o); if (k) kinds[k] = (kinds[k] || 0) + 1; });
            var kindList = Object.keys(kinds).sort(function (a, b) { return (a === w ? -1 : b === w ? 1 : kinds[b] - kinds[a]); });
            var picked = '';
            function show() {
                var list = picked ? all.filter(function (o) { return kindOf(o) === picked; }) : all;
                var chosen = picked || q.toLowerCase();
                lastQuery = chosen;
                $('gem').hidden = false;
                $('gem').textContent = state.mine.indexOf(chosen) >= 0 ? '✓ Gemt' : '⭐ Gem "' + chosen + '"';
                var chains = {}; list.forEach(function (o) { chains[o.chain] = 1; });
                $('soeg-info').textContent = list.length + ' tilbud fra ' + Object.keys(chains).length + (Object.keys(chains).length === 1 ? ' kæde' : ' kæder') + ' – billigste pr. kg/liter øverst.' +
                    (kindList.length > 1 && !picked ? ' Vælg hvilken slags herunder.' : '');
                render(list);
            }
            $('kinder').innerHTML = kindList.length > 1
                ? '<button class="tb-chip tb-chip--on" type="button" data-k="">Alle (' + all.length + ')</button>' +
                  kindList.slice(0, 12).map(function (k) { return '<button class="tb-chip" type="button" data-k="' + esc(k) + '">' + esc(k) + ' (' + kinds[k] + ')</button>'; }).join('')
                : '';
            $('kinder').querySelectorAll('.tb-chip').forEach(function (c) {
                c.addEventListener('click', function () {
                    picked = c.dataset.k;
                    $('kinder').querySelectorAll('.tb-chip').forEach(function (x) { x.classList.toggle('tb-chip--on', x === c); x.setAttribute('aria-pressed', x === c); });
                    show();
                });
            });
            show();
        }).catch(function (err) { $('soeg-info').textContent = err.message; });
    }
    function render(list) {
        var q = lastQuery;
        {
            var shown = 25;
            // Flavoured kinds (kakaomælk when you searched mælk) are folded away at the end.
            var main = list.filter(function (o) { return !o.variant; }), variants = list.filter(function (o) { return o.variant; });
            if (!main.length) { main = list; variants = []; }
            function draw() {
                $('resultater').innerHTML = main.slice(0, shown).map(function (o) { return offerHtml(o, q); }).join('') +
                    (main.length > shown ? '<button class="k-btn k-btn-secondary tb-more" type="button">Vis ' + Math.min(25, main.length - shown) + ' mere</button>' : '') +
                    (variants.length ? '<details class="tb-more-offers"><summary>Andre varianter (' + variants.length + '), fx ' + esc(variants[0].title) + '</summary>' +
                        variants.map(function (o) { return offerHtml(o, q); }).join('') + '</details>' : '');
                var more = $('resultater').querySelector('.tb-more');
                if (more) more.addEventListener('click', function () { shown += 25; draw(); });
            }
            draw();
        }
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
                var oh = function (o) { return offerHtml(o, job[0]); };
                body.innerHTML = oh(list[0]) + (rest > 0
                    ? '<details class="tb-more-offers"><summary>' + (rest > 5 ? 'De 5 næste (af ' + rest + ' andre)' : rest + ' andre tilbud') + '</summary>' +
                      list.slice(1, 6).map(oh).join('') +
                      (rest > 5 ? '<button class="k-btn k-btn-secondary tb-all" type="button" data-q="' + esc(job[0]) + '">Se alle ' + list.length + ' tilbud</button>' : '') +
                      '</details>' : '');
                var all = body.querySelector('.tb-all');
                if (all) all.addEventListener('click', function () { $('q').value = all.dataset.q; search(all.dataset.q); $('soeg-kort').scrollIntoView({ behavior: 'smooth' }); });
            }).catch(function (err) { if (body) body.textContent = err.message; }).then(next);
        }
        next(); next();
    }

    // ---- madspild (Salling): the box shows only when the server has a key (404 = not switched on)
    var waste = null;
    function wasteHtml(c) {
        var img = c.image ? '<img class="tb-img" src="' + esc(c.image) + '" alt="" loading="lazy">' : '<div class="tb-img"></div>';
        var pct = c.percentDiscount ? '<span class="tb-unit">−' + Math.round(c.percentDiscount) + ' %</span>' : '';
        var before = c.originalPrice ? '<span class="tb-before">før ' + money(c.originalPrice) + '</span>' : '';
        var stock = c.stock ? '<span class="k-muted">' + c.stock + ' ' + esc(c.stockUnit || '') + ' tilbage</span>' : '';
        return '<div class="tb-offer">' + img + '<div class="tb-offer-body"><div class="tb-chain">' + esc(c.store) + '</div>' +
            '<div class="tb-title">' + esc(c.title) + '</div>' +
            '<div class="tb-prices"><span class="tb-price">' + money(c.newPrice) + '</span>' + pct + before + stock + '</div>' +
            (c.address ? '<div class="tb-desc k-muted">' + esc(c.address) + '</div>' : '') +
            shareBtn('Madspild: ' + c.title + ' – ' + money(c.newPrice) + (c.originalPrice ? ' (før ' + money(c.originalPrice) + ')' : '') + ' i ' + c.store + '.', '') +
            '</div></div>';
    }
    function drawWaste() {
        if (!waste) return;
        var f = $('mf').value.trim().toLowerCase();
        var list = f ? waste.filter(function (c) { return (c.title + ' ' + c.store).toLowerCase().indexOf(f) >= 0; }) : waste;
        $('mf-info').textContent = list.length + ' varer sat ned' + (f ? ' med "' + f + '"' : '') + ' inden for ' + Math.min(state.km, 20) + ' km.';
        $('mf-liste').innerHTML = list.slice(0, 40).map(wasteHtml).join('');
    }
    function loadWaste() {
        if (!state.place) { $('mf-info').textContent = 'Vælg først hvor du handler.'; return; }
        $('mf-info').textContent = 'Henter madspild …';
        var p = state.place;
        fetch('/api/madspild?lat=' + p.lat + '&lng=' + p.lng + '&km=' + Math.min(state.km, 20)).then(function (r) {
            if (r.status === 404) { $('madspild-kort').hidden = true; return null; }
            return r.json().then(function (j) { if (!r.ok) throw new Error(j.fejl || 'Fejl'); return j; });
        }).then(function (list) { if (list) { waste = list; drawWaste(); } })
          .catch(function (err) { $('mf-info').textContent = err.message; });
    }
    $('mf-hent').addEventListener('click', loadWaste);
    $('mf').addEventListener('input', drawWaste);
    fetch('/api/madspild').then(function (r) { $('madspild-kort').hidden = r.status === 404; }).catch(function () { });

    showPlace();
    refresh();
    // A shared link (/?q=kaffe) opens straight on that search.
    var shareQ = new URLSearchParams(location.search).get('q');
    if (shareQ) { $('q').value = shareQ; if (state.place) search(shareQ); }
})();
