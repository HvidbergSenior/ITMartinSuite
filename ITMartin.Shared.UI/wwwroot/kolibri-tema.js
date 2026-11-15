// Lys/Mørk for the Bogshoppen apps (2026-11-15: "exactly the same" in all four). Loaded in <head> WITHOUT defer, so the
// colours are right before the first paint: what was chosen with the button, else what the phone is set to.
// The button (#themeToggleBtn) is in the Kolibri header, which Blazor renders again - so the click is caught on the document.
(function () {
    var KEY = 'kolibri_theme', OLD = ['salg_theme', 'vurdering_theme'];
    function saved() {
        try {
            var t = localStorage.getItem(KEY);
            for (var i = 0; !t && i < OLD.length; i++) t = localStorage.getItem(OLD[i]);
            return t;
        } catch (_) { return null; }
    }
    var root = document.documentElement;
    root.setAttribute('data-theme', saved() || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));
    function sync() {
        var light = (root.getAttribute('data-theme') || 'light') === 'light';
        var b = document.getElementById('themeToggleBtn'), label = light ? 'Mørk' : 'Lys';
        if (b && b.textContent !== label) b.textContent = label;
        var meta = document.querySelector('meta[name=theme-color]');
        if (meta) meta.setAttribute('content', light ? '#f5f5f7' : '#0b1220');
    }
    document.addEventListener('click', function (e) {
        if (!e.target.closest || !e.target.closest('#themeToggleBtn')) return;
        var next = (root.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        root.setAttribute('data-theme', next);
        try { localStorage.setItem(KEY, next); } catch (_) { }
        sync();
    });
    document.addEventListener('DOMContentLoaded', function () {
        new MutationObserver(sync).observe(document.body, { childList: true, subtree: true });
        sync();
    });
})();
