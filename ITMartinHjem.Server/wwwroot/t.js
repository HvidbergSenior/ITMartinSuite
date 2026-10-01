// Visit counting for itmartin.dk itself (numbers on /admin/tal, Services/Tracker). Counts every page, also the ones
// Blazor opens without reloading, and sends how long the page was open when the visitor leaves it.
(function () {
    if (location.search.indexOf('claude_test=1') !== -1) return;
    var vid;
    try { vid = localStorage.getItem('_mid'); if (!vid) { vid = Math.random().toString(36).slice(2) + Math.random().toString(36).slice(2); localStorage.setItem('_mid', vid); } }
    catch (e) { vid = ''; }
    var cur = null, start = 0, shown = 0, lastUrl = document.referrer;

    function leave() {
        if (!cur) return;
        var s = Math.round((shown + (document.visibilityState === 'visible' ? Date.now() - start : 0)) / 1000);
        if (s > 0) navigator.sendBeacon('/api/hit/' + cur + '/tid', String(s));
    }
    function track() {
        leave();
        cur = null; start = Date.now(); shown = 0;
        var here = location.pathname + location.search;
        // Svar (/admin) is Martin's own: the call still marks his IP as "not counted".
        fetch('/api/hit', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, keepalive: true,
            body: JSON.stringify({ path: here, title: document.title, referrer: lastUrl, visitorId: vid, host: location.hostname })
        }).then(function (r) { return r.status === 200 ? r.json() : null; })
          .then(function (j) { if (j) cur = j.id; }).catch(function () { });
        lastUrl = location.href;
    }
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'hidden') { shown += Date.now() - start; leave(); }
        else start = Date.now();
    });
    // Blazor changes pages with pushState: count each one (after the new title is set).
    var push = history.pushState;
    history.pushState = function () {
        var before = location.pathname;
        push.apply(this, arguments);
        if (location.pathname !== before) setTimeout(track, 300);
    };
    window.addEventListener('popstate', function () { setTimeout(track, 300); });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { setTimeout(track, 300); });
    else setTimeout(track, 300);
})();
