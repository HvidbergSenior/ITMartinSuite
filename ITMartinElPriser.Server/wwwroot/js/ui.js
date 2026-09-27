// Small per-phone UI memory: the chosen chip on Planlæg, and which fold-out cards are open.
window.epUi = {
    get: function (key) {
        try { return localStorage.getItem('ep_ui_' + key); } catch (e) { return null; }
    },
    set: function (key, value) {
        try { localStorage.setItem('ep_ui_' + key, value); } catch (e) { }
    },
    // Every show/hide box remembers open/closed on the phone (user rule 2026-09-27). A box names
    // itself with data-remember; any other box is keyed by page + its summary text.
    // Blazor adds boxes on every page change, so this also runs from a MutationObserver below.
    keyOf: function (d) {
        if (d.dataset.remember) return d.dataset.remember;
        var s = d.querySelector(':scope > summary');
        return s ? location.pathname + '|' + s.textContent.trim().replace(/\s+/g, ' ').slice(0, 60) : null;
    },
    restoreDetails: function () {
        document.querySelectorAll('details:not([data-restored])').forEach(function (d) {
            var key = epUi.keyOf(d);
            if (!key) return;
            d.dataset.restored = '1';
            var v = epUi.get('open_' + key);
            if (v === '1') d.open = true;
            else if (v === '0') d.open = false;
        });
    }
};
document.addEventListener('toggle', function (e) {
    var d = e.target;
    if (d.tagName !== 'DETAILS' || !d.dataset.restored) return;
    var key = epUi.keyOf(d);
    if (key) epUi.set('open_' + key, d.open ? '1' : '0');
}, true);
new MutationObserver(function () { epUi.restoreDetails(); }).observe(document.documentElement, { childList: true, subtree: true });
epUi.restoreDetails();

// The phone's position for finding the grid company. Resolves to null when refused or slow.
epUi.locate = function () {
    return new Promise(function (resolve) {
        if (!navigator.geolocation) { resolve(null); return; }
        navigator.geolocation.getCurrentPosition(
            function (p) { resolve({ lat: p.coords.latitude, lon: p.coords.longitude }); },
            function () { resolve(null); },
            { enableHighAccuracy: false, timeout: 10000, maximumAge: 600000 });
    });
};

// A link to /groen#kwh opens that card - once per page visit, so later re-renders never jump.
var epHashDone = '';
function epOpenHash() {
    var id = location.hash.slice(1);
    if (!id || epHashDone === location.pathname + '#' + id) return;
    var d = document.getElementById(id);
    if (d && d.tagName === 'DETAILS') { d.open = true; d.scrollIntoView({ block: 'start' }); epHashDone = location.pathname + '#' + id; }
}
window.addEventListener('hashchange', function () { epHashDone = ''; epOpenHash(); });
new MutationObserver(epOpenHash).observe(document.documentElement, { childList: true, subtree: true });

// Install on the phone: keep the browser's install offer so the guide can show a real button.
window.addEventListener('beforeinstallprompt', function (e) { e.preventDefault(); window._epInstall = e; });
epUi.device = function () {
    var ua = navigator.userAgent;
    return {
        installed: window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true,
        ios: /iphone|ipad|ipod/i.test(ua),
        pc: !/android|iphone|ipad|ipod|mobile/i.test(ua),
        canPrompt: !!window._epInstall
    };
};
epUi.install = async function () {
    if (!window._epInstall) return false;
    window._epInstall.prompt();
    var r = await window._epInstall.userChoice;
    window._epInstall = null;
    return !!r && r.outcome === 'accepted';
};
