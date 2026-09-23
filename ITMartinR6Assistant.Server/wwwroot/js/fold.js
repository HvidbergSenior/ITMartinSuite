// Remembers which cards (<details data-fold="key">) each viewer has opened or closed, per device.
// Blazor adds cards as the page changes, so new ones are picked up with a MutationObserver.
(function () {
    const prefix = 'r6fold:';
    const read = k => { try { return localStorage.getItem(prefix + k); } catch { return null; } };
    const write = (k, v) => { try { localStorage.setItem(prefix + k, v); } catch { } };

    function init(el) {
        if (el.dataset.foldInit) return;
        el.dataset.foldInit = '1';
        const saved = read(el.dataset.fold);
        if (saved !== null) el.open = saved === '1';
    }
    function scan(root) {
        if (root.matches && root.matches('details[data-fold]')) init(root);
        if (root.querySelectorAll) root.querySelectorAll('details[data-fold]').forEach(init);
    }

    document.addEventListener('toggle', e => {
        const el = e.target;
        if (el.dataset && el.dataset.fold && el.dataset.foldInit) write(el.dataset.fold, el.open ? '1' : '0');
    }, true);
    new MutationObserver(ms => ms.forEach(m => m.addedNodes.forEach(n => { if (n.nodeType === 1) scan(n); })))
        .observe(document.documentElement, { childList: true, subtree: true });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => scan(document));
    else scan(document);
})();
