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
    // Which days: "kun fre. 9.10", "fra tirs. 7.10 til søn. 12.10" or "til søn. 12.10".
    function when(o) {
        if (!o.validTo) return '';
        var from = o.validFrom ? new Date(o.validFrom) : null, to = new Date(o.validTo), now = new Date();
        if (from && from.toDateString() === to.toDateString()) return 'kun ' + day.format(to);
        if (from && from > now) return 'fra ' + day.format(from) + ' til ' + day.format(to);
        return 'til ' + day.format(to);
    }
    function where(o) {
        return o.nearestStore ? esc(o.nearestStore) + (o.nearestKm != null ? ' (' + String(o.nearestKm).replace('.', ',') + ' km)' : '') : '';
    }
    // Compare like with like: per kg/litre when both have it in the same unit, else the shelf price.
    function measure(o, ref) { return ref && ref.unitLabel && o.unitLabel === ref.unitLabel && o.unitPrice != null ? o.unitPrice : o.price; }
    function diffText(o, ref) {
        var d = measure(o, ref) - measure(ref, ref);
        var unit = ref.unitLabel && o.unitLabel === ref.unitLabel ? ' ' + ref.unitLabel : ' kr';
        return (d >= 0 ? '+' : '−') + kr.format(Math.abs(d)) + unit.replace(' kr/', ' kr/');
    }

    // alt = the cheapest offer that needs no app: shown on every app-only offer (user: "you don't have to buy there").
    function offerHtml(o, q, alt) {
        var unit = o.unitPrice != null ? '<span class="tb-unit">' + money(o.unitPrice).replace(' kr', '') + ' ' + esc(o.unitLabel) + '</span>' : '';
        var before = o.normalPrice && o.normalPrice > o.price ? '<span class="tb-before">før ' + money(o.normalPrice) + '</span>' : '';
        var till = o.validTo ? '<span class="k-muted">' + when(o) + '</span>' : '';
        var img = o.image ? '<img class="tb-img" src="' + esc(o.image) + '" alt="" loading="lazy" onerror="this.hidden=true">' : '<div class="tb-img"></div>';
        return '<div class="tb-offer">' + img + '<div class="tb-offer-body">' +
            '<div class="tb-chain">' + esc(o.chain) + '</div>' +
            '<div class="tb-title">' + esc(o.title) + (o.size ? ' <span class="k-muted">· ' + esc(o.size) + '</span>' : '') + '</div>' +
            '<div class="tb-prices"><span class="tb-price">' + money(o.price) + '</span>' + unit + before + till + '</div>' +
            (o.needsApp ? '<div class="tb-needs">📱 Dette er et tilbud i ' + esc(o.needsApp) + (o.condition ? ' (' + esc(o.condition) + ')' : '') + '.</div>' +
                (alt && alt !== o ? '<div class="tb-alt">👉 Du behøver ikke appen: <b>' + esc(alt.chain) + '</b> har ' + esc(alt.title) + ' til <b>' + money(alt.price) + '</b>' +
                    ' (' + diffText(alt, o) + ', ' + when(alt) + ')' + (alt.nearestStore ? ' – ' + where(alt) : '') + '.</div>' : '')
                : o.condition ? '<div class="tb-needs">⚠️ ' + esc(o.condition) + '</div>' : '') +
            (o.nearestStore ? '<div class="tb-where">📍 Nærmeste: ' + where(o) + '</div>' : '') +
            (o.description ? '<div class="tb-desc k-muted">' + esc(o.description) + '</div>' : '') +
            (APP[o.chain] && state.apps.indexOf(APP[o.chain]) >= 0 ? '<div class="tb-app">📱 Åbn ' + APP_NAME[APP[o.chain]] + ' – der kan være en kupon oveni.</div>' : '') +
            shareBtn(o.title + ' – ' + money(o.price) + (o.unitPrice != null ? ' (' + money(o.unitPrice).replace(' kr', '') + ' ' + o.unitLabel + ')' : '') +
                ' i ' + o.chain + (o.validTo ? ', ' + when(o) : '') + (o.needsApp ? ' (kræver ' + o.needsApp + ')' : '') + '.', q || lastQuery) +
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
            // User 2026-10-06: the shops' apps are too complicated for many - offers that need one are shown last, folded.
            var appOnly = main.filter(function (o) { return o.needsApp; });
            if (appOnly.length < main.length) main = main.filter(function (o) { return !o.needsApp; }); else appOnly = [];
            function draw() {
                $('resultater').innerHTML = main.slice(0, shown).map(function (o) { return offerHtml(o, q); }).join('') +
                    (main.length > shown ? '<button class="k-btn k-btn-secondary tb-more" type="button">Vis ' + Math.min(25, main.length - shown) + ' mere</button>' : '') +
                    (appOnly.length ? '<details class="tb-more-offers"><summary>📱 Kun med butikkens app (' + appOnly.length + ')</summary>' +
                        appOnly.map(function (o) { return offerHtml(o, q, main[0]); }).join('') + '</details>' : '') +
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

    // ---- Mine varer, the simple answer (user 2026-10-06): where is it cheapest, which days, which store - without an
    // app first; an app-only offer is mentioned only when it is cheaper; and the other shops, so you see what you save.
    function summary(list, q) {
        var main = list.filter(function (o) { return !o.variant; }); if (!main.length) main = list;
        var free = main.filter(function (o) { return !o.needsApp; });
        var best = free[0] || main[0];
        var app = main.filter(function (o) { return o.needsApp && measure(o, best) < measure(best, best); })[0];
        var seen = {}; seen[best.chain] = 1;
        var others = free.filter(function (o) { if (seen[o.chain]) return false; seen[o.chain] = 1; return true; }).slice(0, 4);
        var img = best.image ? '<img class="tb-img" src="' + esc(best.image) + '" alt="" loading="lazy" onerror="this.hidden=true">' : '';
        return '<div class="tb-best">' + img + '<div class="tb-offer-body">' +
            '<div class="tb-prices"><span class="tb-price">' + money(best.price) + '</span>' +
            (best.unitPrice != null ? '<span class="tb-unit">' + kr.format(best.unitPrice) + ' ' + esc(best.unitLabel) + '</span>' : '') +
            (best.normalPrice && best.normalPrice > best.price ? '<span class="tb-before">før ' + money(best.normalPrice) + '</span>' : '') + '</div>' +
            '<div class="tb-best-where"><b>' + esc(best.chain) + '</b> – ' + esc(best.title) + (best.size ? ' · ' + esc(best.size) : '') + '</div>' +
            (best.nearestStore ? '<div class="tb-where">📍 ' + where(best) + '</div>' : '') +
            (best.validTo ? '<div class="tb-where">📅 ' + when(best).replace(/^./, function (c) { return c.toUpperCase(); }) + '</div>' : '') +
            (best.needsApp ? '<div class="tb-needs">📱 Kræver ' + esc(best.needsApp) + ' – der er intet tilbud uden app lige nu</div>' : '') +
            (best.condition && !best.needsApp ? '<div class="tb-needs">⚠️ ' + esc(best.condition) + '</div>' : '') +
            '</div></div>' +
            (app ? '<div class="tb-appline">📱 Med ' + esc(app.needsApp) + ': ' + money(app.price) + ' i ' + esc(app.chain) +
                ' (' + diffText(app, best) + ', ' + when(app) + (app.condition ? ', ' + esc(app.condition) : '') + ')</div>' : '') +
            (others.length ? '<div class="tb-others"><b>Andre steder:</b> ' + others.map(function (o) {
                return esc(o.chain) + ' ' + money(o.price) + ' <span class="k-muted">(' + diffText(o, best) + ')</span>';
            }).join(' · ') + '</div>' : '<div class="tb-others k-muted">Ingen andre butikker har den på tilbud lige nu.</div>') +
            '<div class="tb-row">' + shareBtn('Billigst ' + q + ': ' + best.title + ' – ' + money(best.price) + ' i ' + best.chain +
                (best.nearestStore ? ', ' + best.nearestStore : '') + (best.validTo ? ', ' + when(best) : '') + '.', q) +
            '<button class="tb-share tb-all" type="button" data-q="' + esc(q) + '">Se alle ' + list.length + ' tilbud</button></div>';
    }

    // ---- Mine varer: the best offer per item
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
                body.innerHTML = summary(list, job[0]);
                var all = body.querySelector('.tb-all');
                if (all) all.addEventListener('click', function () { $('q').value = all.dataset.q; search(all.dataset.q); $('soeg-kort').scrollIntoView({ behavior: 'smooth' }); });
            }).catch(function (err) { if (body) body.textContent = err.message; }).then(next);
        }
        next(); next();
    }

    // ---- madspild (Salling): the box shows only when the server has a key (404 = not switched on)
    var waste = null;
    // The server searches 5, 10 or 20 km for madspild (Salling's 100 requests a day) - say what it really searched.
    function wasteKm() { return state.km <= 5 ? 5 : state.km <= 10 ? 10 : 20; }
    function wasteHtml(c) {
        var img = c.image ? '<img class="tb-img" src="' + esc(c.image) + '" alt="" loading="lazy" onerror="this.hidden=true">' : '<div class="tb-img"></div>';
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
        $('mf-info').textContent = list.length + ' varer sat ned' + (f ? ' med "' + f + '"' : '') + ' inden for ' + wasteKm() + ' km.';
        $('mf-liste').innerHTML = list.slice(0, 40).map(wasteHtml).join('');
    }
    function loadWaste() {
        if (!state.place) { $('mf-info').textContent = 'Vælg først hvor du handler.'; return; }
        $('mf-info').textContent = 'Henter madspild …';
        var p = state.place;
        fetch('/api/madspild?lat=' + p.lat + '&lng=' + p.lng + '&km=' + wasteKm()).then(function (r) {
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
