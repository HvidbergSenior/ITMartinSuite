// Small per-phone UI memory: the chosen chip on Planlæg, and which fold-out cards are open.
window.epUi = {
    get: function (key) {
        try { return localStorage.getItem('ep_ui_' + key); } catch (e) { return null; }
    },
    set: function (key, value) {
        try { localStorage.setItem('ep_ui_' + key, value); } catch (e) { }
    },
    restoreDetails: function () {
        document.querySelectorAll('details[data-remember]').forEach(function (d) {
            if (epUi.get('open_' + d.dataset.remember) === '1') d.open = true;
        });
    }
};
document.addEventListener('toggle', function (e) {
    var d = e.target;
    if (d.tagName === 'DETAILS' && d.dataset.remember) epUi.set('open_' + d.dataset.remember, d.open ? '1' : '0');
}, true);
