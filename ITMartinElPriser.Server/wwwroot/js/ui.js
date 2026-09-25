// Small per-phone UI memory: the chosen chip on Planlæg, and which fold-out cards are open.
window.epUi = {
    get: function (key) {
        try { return localStorage.getItem('ep_ui_' + key); } catch (e) { return null; }
    },
    set: function (key, value) {
        try { localStorage.setItem('ep_ui_' + key, value); } catch (e) { }
    },
    // Opens remembered cards once each; Blazor adds them on every page change, so this
    // also runs from a MutationObserver below.
    restoreDetails: function () {
        document.querySelectorAll('details[data-remember]:not([data-restored])').forEach(function (d) {
            d.dataset.restored = '1';
            if (epUi.get('open_' + d.dataset.remember) === '1') d.open = true;
        });
    }
};
document.addEventListener('toggle', function (e) {
    var d = e.target;
    if (d.tagName === 'DETAILS' && d.dataset.remember && d.dataset.restored) epUi.set('open_' + d.dataset.remember, d.open ? '1' : '0');
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
