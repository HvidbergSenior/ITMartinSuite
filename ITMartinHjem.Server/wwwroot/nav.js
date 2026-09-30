// Draws the site menu at the top of hand-made pages (wwwroot/bliv-pilot) so they feel like
// the rest of the site. The menu itself comes from /api/menu (pages marked "Vis i menuen").
(function () {
    var nav = document.createElement('nav');
    nav.style.cssText = 'display:flex;flex-wrap:wrap;gap:4px 16px;justify-content:center;padding:10px 16px;' +
        'border-bottom:1px solid rgba(0,0,0,.1);background:#fff;font:600 16px/1.4 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif';
    document.body.insertBefore(nav, document.body.firstChild);
    fetch('/api/menu').then(function (r) { return r.json(); }).then(function (items) {
        items.forEach(function (i) {
            var a = document.createElement('a');
            a.href = i.href;
            a.textContent = i.title;
            var here = location.pathname.replace(/\/$/, '') === i.href.replace(/\/$/, '');
            a.style.cssText = 'text-decoration:none;padding:4px 0;color:' + (here ? '#92400e' : '#1f2933') +
                (here ? ';border-bottom:2px solid #92400e' : '');
            nav.appendChild(a);
        });
    }).catch(function () { nav.remove(); });
    // Birthday day (Components/Fest): the same strip as on the Blazor pages, under the menu.
    fetch('/api/fest').then(function (r) { return r.json(); }).then(function (f) {
        if (!f.on) return;
        var a = document.createElement('a');
        a.href = '/#chat';
        a.style.cssText = 'display:block;text-align:center;padding:10px 16px;text-decoration:none;color:#fff1dc;' +
            'background:linear-gradient(90deg,#8c2d08,#d9480f,#f28c13,#d9480f,#8c2d08);font:700 15px/1.4 system-ui,sans-serif';
        a.innerHTML = '🎂 <b>ITMartin har fødselsdag i dag – 49 år!</b> Sig tillykke i chatten på forsiden →';
        nav.parentNode.insertBefore(a, nav.nextSibling);
    }).catch(function () { });
})();
