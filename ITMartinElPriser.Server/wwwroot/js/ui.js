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
