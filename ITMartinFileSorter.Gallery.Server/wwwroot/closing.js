// Shows a warning banner at the top of a gallery that closes soon (Galleries__N__ClosesAt, see Closing.cs).
(function () {
    var seg = location.pathname.replace(/^\/+|\/+$/g, '').split('/')[0];
    var slug = (decodeURIComponent(seg) || new URLSearchParams(location.search).get('g') || '').toLowerCase();
    if (!slug) return;
    fetch('/api/closing?gallery=' + encodeURIComponent(slug)).then(function (r) { return r.ok ? r.json() : null; }).then(function (c) {
        if (!c || !c.warning) return;
        var b = document.createElement('div');
        b.setAttribute('role', 'status');
        b.textContent = c.warning;
        b.style.cssText = 'position:sticky;top:0;z-index:1000;padding:12px 16px;text-align:center;font:700 16px/1.4 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;' +
            'background:#fff3cd;color:#5c4400;border-bottom:2px solid #f0b400';
        document.body.insertBefore(b, document.body.firstChild);
    }).catch(function () { });
})();
