// Kolibri.UI shared behaviour for every app (user rules 2026-09-27/28):
//  1. Every box can be hidden/shown, and the phone remembers it.
//     - <details> boxes remember open/closed.
//     - Cards with a heading (.k-card / .card whose first child is a title) get a ▾ toggle.
//       Opt out with data-nofold. Cards stay open until the user closes them.
//  2. "Læg på telefonen": a small button + sheet with iPhone / Android / PC steps, the box for
//     the current device already open. Hidden when the app runs from the home screen.
// Blazor re-renders pages, so both run again from a MutationObserver.
(function () {
    'use strict';
    if (window.kolibri) return;

    // Own styles, so apps without kolibri.css (R6, Club, Ladestander, Galleri, Upload ...) get the
    // same pill, sheet and ▾. Kolibri colours when the page has them, neutral fallbacks otherwise.
    (function injectStyle() {
        if (document.getElementById('k-js-style')) return;
        var c = function (name, fb) { return 'var(--k-' + name + ',' + fb + ')'; };
        var css =
            '.k-fold-head{cursor:pointer;display:flex;align-items:center;gap:.5em;user-select:none}' +
            '.k-fold-head::after{content:"▾";margin-left:auto;opacity:.6;font-size:.9em;transition:transform .15s}' +
            '.k-folded>.k-fold-head::after{transform:rotate(-90deg)}' +
            '.k-folded>:not(.k-fold-head){display:none!important}' +
            '.k-install-pill{position:fixed;z-index:900;left:12px;bottom:calc(12px + var(--k-bottom-offset,0px) + env(safe-area-inset-bottom,0px));border:1px solid ' + c('border', 'rgba(128,128,128,.35)') + ';background:' + c('card', '#fff') + ';color:' + c('text', '#1a1a1a') + ';border-radius:999px;padding:8px 14px;font:600 14px system-ui,-apple-system,Segoe UI,Roboto,sans-serif;box-shadow:0 2px 10px rgba(0,0,0,.18);cursor:pointer}' +
            '.k-install-sheet{position:fixed;inset:0;z-index:950;background:rgba(0,0,0,.45);display:flex;align-items:flex-end;justify-content:center;padding:12px}' +
            '.k-install-sheet[hidden]{display:none}' +
            '@media (min-width:640px){.k-install-sheet{align-items:center}}' +
            '.k-install-card{position:relative;width:100%;max-width:520px;max-height:88vh;overflow:auto;background:' + c('card', '#fff') + ';color:' + c('text', '#1a1a1a') + ';border-radius:18px;padding:20px 18px calc(18px + env(safe-area-inset-bottom,0px));font:16px/1.5 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;text-align:left}' +
            '.k-install-card h2{margin:0 32px 6px 0;font-size:1.2rem}' +
            '.k-install-card .k-muted{color:' + c('muted', '#666') + '}' +
            '.k-install-x{position:absolute;top:10px;right:10px;border:0;background:rgba(128,128,128,.18);color:inherit;width:34px;height:34px;border-radius:50%;cursor:pointer}' +
            '.k-install-dev{border:1px solid ' + c('border', 'rgba(128,128,128,.35)') + ';border-radius:12px;padding:8px 12px;margin-top:8px}' +
            '.k-install-dev summary{font-weight:600;cursor:pointer}' +
            '.k-install-dev ol{margin:8px 0 4px;padding-left:1.3em}.k-install-dev li{margin:4px 0}' +
            '.k-install-now{width:100%;margin-top:8px;padding:10px;border-radius:10px;border:0;background:' + c('accent', '#1f7a5c') + ';color:#fff;font:600 15px system-ui,sans-serif;cursor:pointer}' +
            '.k-install-hide{margin-top:12px;border:0;background:none;color:' + c('muted', '#666') + ';text-decoration:underline;cursor:pointer;font:inherit;font-size:14px;padding:0}' +
            '.k-footer-install[hidden]{display:none!important}' +
            '@media print{.k-install-pill,.k-install-sheet{display:none!important}}';
        var s = document.createElement('style');
        s.id = 'k-js-style';
        s.textContent = css;
        (document.head || document.documentElement).appendChild(s);
    })();

    var store = {
        get: function (k) { try { return localStorage.getItem('kolibri_' + k); } catch (e) { return null; } },
        set: function (k, v) { try { localStorage.setItem('kolibri_' + k, v); } catch (e) { } }
    };
    var text = function (el) { return (el ? el.textContent : '').trim().replace(/\s+/g, ' ').slice(0, 60); };
    var da = (document.documentElement.lang || 'da').slice(0, 2) !== 'en';
    var T = function (dk, en) { return da ? dk : en; };

    /* ---------- 1a. <details> remember ---------- */
    // Apps with their own copy of this logic (ElPriser/MinElpris ui.js) keep theirs.
    var ownDetails = !!window.epUi;
    function detailsKey(d) {
        if (d.dataset.remember) return d.dataset.remember;
        var s = d.querySelector(':scope > summary');
        return s ? location.pathname + '|' + text(s) : null;
    }
    function restoreDetails() {
        if (ownDetails) return;
        document.querySelectorAll('details:not([data-k-restored])').forEach(function (d) {
            if (d.closest('#k-install')) return;
            var key = detailsKey(d);
            if (!key) return;
            d.dataset.kRestored = '1';
            var v = store.get('open_' + key);
            if (v === '1') d.open = true; else if (v === '0') d.open = false;
        });
    }
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (ownDetails || d.tagName !== 'DETAILS' || !d.dataset.kRestored) return;
        var key = detailsKey(d);
        if (key) store.set('open_' + key, d.open ? '1' : '0');
    }, true);

    /* ---------- 1b. foldable cards ---------- */
    var HEAD = '.k-card-title, h1, h2, h3, h4';
    function cardHead(card) {
        var first = card.firstElementChild;
        return first && first.matches(HEAD) ? first : null;
    }
    function foldCards() {
        document.querySelectorAll('.k-card:not([data-k-fold]), .card:not([data-k-fold])').forEach(function (card) {
            card.dataset.kFold = '1';
            if (card.hasAttribute('data-nofold') || card.tagName === 'DETAILS' || card.classList.contains('fold') ||
                card.closest('details, #k-install, [data-nofold]')) return;
            var head = cardHead(card);
            if (!head || !card.children[1]) return;
            var key = 'card_' + location.pathname + '|' + text(head);
            head.classList.add('k-fold-head');
            head.setAttribute('role', 'button');
            head.setAttribute('tabindex', '0');
            var set = function (closed) {
                card.classList.toggle('k-folded', closed);
                head.setAttribute('aria-expanded', closed ? 'false' : 'true');
            };
            // data-fold-closed: starts closed (only the heading shows) until the visitor opens it once.
            var st = store.get(key);
            set(card.hasAttribute('data-fold-closed') ? st !== '1' : st === '0');
            var flip = function (e) {
                if (e.target.closest('a, button, input, select, textarea, label') && e.target !== head) return;
                var closed = !card.classList.contains('k-folded');
                set(closed); store.set(key, closed ? '0' : '1');
            };
            head.addEventListener('click', flip);
            head.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); flip(e); } });
        });
    }

    /* ---------- 2. install on the phone ---------- */
    var promptEvent = null;
    window.addEventListener('beforeinstallprompt', function (e) { e.preventDefault(); promptEvent = e; renderInstall(); });
    var ua = navigator.userAgent;
    var dev = {
        ios: /iphone|ipad|ipod/i.test(ua) || (/macintosh/i.test(ua) && navigator.maxTouchPoints > 1),
        pc: !/android|iphone|ipad|ipod|mobile/i.test(ua) && !(navigator.maxTouchPoints > 1 && /macintosh/i.test(ua))
    };
    var installed = function () { return window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true; };
    var appName = (document.querySelector('meta[name="application-name"]') || {}).content || document.title.split(/[–|-]/)[0].trim() || 'appen';
    fetch('/manifest.webmanifest').then(function (r) { return r.ok ? r.json() : null; })
        .then(function (m) { if (m && (m.short_name || m.name)) { appName = m.short_name || m.name; renderInstall(); } })
        .catch(function () { });

    function steps() {
        var btn = promptEvent ? '<button type="button" class="k-btn k-install-now">' + T('Installer ', 'Install ') + appName + '</button>' : '';
        return '' +
            '<details class="k-install-dev"' + (dev.ios ? ' open' : '') + '><summary>📱 iPhone</summary><ol>' +
            T('<li>Åbn siden i <b>Safari</b>.</li><li>Tryk på <b>(⋯)</b> nederst til højre og vælg <b>Del</b> ⬆︎. <span class="k-muted">(Ældre iPhone: tryk direkte på Del ⬆︎ nederst.)</span></li><li>Rul ned, vælg <b>Føj til hjemmeskærm</b> og tryk <b>Tilføj</b>.</li>',
              '<li>Open the page in <b>Safari</b>.</li><li>Tap <b>(⋯)</b> bottom right and choose <b>Share</b> ⬆︎. <span class="k-muted">(Older iPhone: tap Share ⬆︎ at the bottom.)</span></li><li>Scroll down, choose <b>Add to Home Screen</b> and tap <b>Add</b>.</li>') +
            '</ol></details>' +
            '<details class="k-install-dev"' + (!dev.ios && !dev.pc ? ' open' : '') + '><summary>🤖 Android</summary>' + (!dev.pc ? btn : '') + '<ol>' +
            T('<li>Åbn siden i <b>Chrome</b>.</li><li>Tryk på <b>⋮</b> øverst til højre.</li><li>Vælg <b>Føj til startskærm</b> og tryk <b>Tilføj</b>.</li>',
              '<li>Open the page in <b>Chrome</b>.</li><li>Tap <b>⋮</b> top right.</li><li>Choose <b>Add to Home screen</b> and tap <b>Add</b>.</li>') +
            '</ol></details>' +
            '<details class="k-install-dev"' + (dev.pc ? ' open' : '') + '><summary>💻 PC</summary>' + (dev.pc ? btn : '') + '<ol>' +
            T('<li><b>Chrome:</b> ⋮ øverst til højre → <b>Cast, gem og del</b> → <b>Installer side som app</b>.</li><li><b>Edge:</b> ··· øverst til højre → <b>Apps</b> → <b>Installer dette websted som en app</b>.</li>',
              '<li><b>Chrome:</b> ⋮ top right → <b>Cast, save and share</b> → <b>Install page as app</b>.</li><li><b>Edge:</b> ··· top right → <b>Apps</b> → <b>Install this site as an app</b>.</li>') +
            '</ol></details>';
    }

    function renderInstall() {
        var box = document.getElementById('k-install');
        if (installed()) { if (box) box.remove(); return; }
        if (!document.body) return;
        if (!box) {
            box = document.createElement('div');
            box.id = 'k-install';
            document.body.appendChild(box);
        }
        var where = dev.pc ? T('skrivebordet', 'your desktop') : T('telefonen', 'your phone');
        var hidden = store.get('install_hidden') === '1';
        var open = box.classList.contains('open');
        box.innerHTML =
            (hidden ? '' : '<button type="button" class="k-install-pill" aria-haspopup="dialog">📲 ' + T('Læg på ', 'Add to ') + where + '</button>') +
            '<div class="k-install-sheet" role="dialog" aria-modal="true" aria-label="' + T('Læg på ', 'Add to ') + where + '"' + (open ? '' : ' hidden') + '>' +
            '<div class="k-install-card"><button type="button" class="k-install-x" aria-label="' + T('Luk', 'Close') + '">✕</button>' +
            '<h2>📲 ' + T('Læg ' + appName + ' på ' + where, 'Add ' + appName + ' to ' + where) + '</h2>' +
            '<p class="k-muted">' + T('Så åbner den med ét tryk – som en app, uden at gå via browseren.', 'Then it opens with one tap – like an app, without the browser.') + '</p>' +
            steps() +
            (hidden ? '' : '<button type="button" class="k-install-hide">' + T('Skjul knappen (findes stadig nederst på siden)', 'Hide the button (still in the page footer)') + '</button>') +
            '</div></div>';
    }
    function openSheet() { var b = document.getElementById('k-install'); if (!b) return; b.classList.add('open'); b.querySelector('.k-install-sheet').hidden = false; }
    function closeSheet() { var b = document.getElementById('k-install'); if (!b) return; b.classList.remove('open'); b.querySelector('.k-install-sheet').hidden = true; }
    document.addEventListener('click', async function (e) {
        var t = e.target;
        if (t.closest('.k-install-pill, a[href="#k-install"]')) { e.preventDefault(); if (!installed()) openSheet(); return; }
        if (t.closest('.k-install-x') || t.classList.contains('k-install-sheet')) { closeSheet(); return; }
        if (t.closest('.k-install-hide')) { store.set('install_hidden', '1'); closeSheet(); renderInstall(); return; }
        if (t.closest('.k-install-now') && promptEvent) {
            promptEvent.prompt();
            var r = await promptEvent.userChoice; promptEvent = null;
            if (r && r.outcome === 'accepted') { closeSheet(); }
            renderInstall();
        }
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeSheet(); });

    // ElPriser/MinElpris already show their own install card on the page - no pill there.
    function ownInstallGuide() { return !!document.querySelector('.install-guide'); }

    /* ---------- run ---------- */
    var queued = false;
    function run() {
        queued = false;
        restoreDetails();
        foldCards();
        var box = document.getElementById('k-install');
        if (ownInstallGuide()) { if (box) box.remove(); }
        else if (!box || !document.body.contains(box)) renderInstall();
        // The footer link (KolibriFooter) only where it can open the sheet: hidden once installed, and in
        // apps with their own install card (ElPriser/MinElpris), where it did nothing (user 2026-09-28).
        var sheet = document.getElementById('k-install');
        document.querySelectorAll('.k-footer-install').forEach(function (a) { a.hidden = installed() || !sheet; });
    }
    new MutationObserver(function () { if (!queued) { queued = true; requestAnimationFrame(run); } })
        .observe(document.documentElement, { childList: true, subtree: true });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();

    window.kolibri = { store: store, openInstall: openSheet };

    /* ---------- visit counting (numbers on itmartin.dk/admin/tal) ---------- */
    // User 2026-10-06: stats for the photoserver apps too, and never count Martin's own phone/PC.
    // - itmartin.dk itself counts with its own t.js; Kontrol is Martin's alone.
    // - Apps that still carry the old inline snippet (same '_mid' id) keep counting with it - no double count.
    // - itmartin.dk/tael-ikke-mig leaves the cookie ikke_mig=1 on the whole domain: here this app's browser id is
    //   (bogshoppen.dk/tael-ikke-mig for *.bogshoppen.dk) then marked as Martin's once, so the server ignores it from both this script and an old inline snippet.
    (function countVisit() {
        var h = location.hostname;
        // bogshoppen.dk itself is the hjem app (t.js); its subdomains (Lager) count here.
        if (!/\.(itmartin|bogshoppen)\.dk$/.test(h) || /^(www|martin|kontrol)\.itmartin\.dk$|^www\.bogshoppen\.dk$/.test(h)) return;
        if (location.search.indexOf('claude_test=1') !== -1) return;
        var api = 'https://stats.itmartin.dk/api/hit', vid = '';
        try { vid = localStorage.getItem('_mid'); if (!vid) { vid = Math.random().toString(36).slice(2) + Math.random().toString(36).slice(2); localStorage.setItem('_mid', vid); } }
        catch (e) { }
        if (/(^|;\s*)ikke_mig=1/.test(document.cookie)) {
            try {
                if (vid && localStorage.getItem('_me') !== vid) {
                    fetch(api + '/mig', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ visitorId: vid }) })
                        .then(function (r) { if (r.ok) localStorage.setItem('_me', vid); }).catch(function () { });
                }
            } catch (e) { }
            return;
        }
        var inline = Array.prototype.some.call(document.scripts, function (s) { return !s.src && s.textContent.indexOf('stats.itmartin.dk/api/hit') !== -1; });
        if (inline) return;
        fetch(api, {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, keepalive: true,
            body: JSON.stringify({ path: location.pathname + location.search, title: document.title, referrer: document.referrer, visitorId: vid, host: h })
        }).catch(function () { });
    })();
})();
