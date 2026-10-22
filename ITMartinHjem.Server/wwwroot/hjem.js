// Small browser helpers for Martin Hvidberg's page, called from Blazor.
window.hjem = (function () {
    function b64ToBytes(b64) {
        var pad = '='.repeat((4 - b64.length % 4) % 4);
        var raw = atob((b64 + pad).replace(/-/g, '+').replace(/_/g, '/'));
        var out = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }
    function newKey() {
        var a = new Uint8Array(16);
        crypto.getRandomValues(a);
        return Array.from(a, function (b) { return b.toString(16).padStart(2, '0'); }).join('');
    }
    var memKey = null;
    async function reg() {
        if (!('serviceWorker' in navigator)) return null;
        return await navigator.serviceWorker.register('/sw.js');
    }

    return {
        // The visitor's chat identity: kept in this browser only. Private windows get a
        // fresh one each time, which just means a new conversation.
        visitorKey: function () {
            try {
                var k = localStorage.getItem('hjem-chat');
                if (!k) { k = newKey(); localStorage.setItem('hjem-chat', k); }
                return k;
            } catch (e) { return memKey || (memKey = newKey()); }
        },

        scrollEnd: function (id) {
            var el = document.getElementById(id);
            if (el) el.scrollTop = el.scrollHeight;
        },

        // "unsupported" | "ios-needs-homescreen" | "denied" | "off" | "on"
        pushState: async function () {
            if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
                var isIOS = /iPhone|iPad|iPod/.test(navigator.userAgent);
                var standalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
                return isIOS && !standalone ? 'ios-needs-homescreen' : 'unsupported';
            }
            if (Notification.permission === 'denied') return 'denied';
            var r = await reg();
            var sub = r ? await r.pushManager.getSubscription() : null;
            return sub ? 'on' : 'off';
        },

        pushSubscribe: async function () {
            try {
                var r = await reg();
                var perm = await Notification.requestPermission();
                if (perm !== 'granted') return 'denied';
                var key = await (await fetch('/api/push/public-key')).text();
                var sub = await r.pushManager.getSubscription()
                       || await r.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64ToBytes(key) });
                var j = sub.toJSON();
                var res = await fetch('/api/push/subscribe', {
                    method: 'POST', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth })
                });
                return res.ok ? 'on' : 'error';
            } catch (e) { return 'error'; }
        },

        // Visitor: "Giv mig besked, når Martin svarer". Called straight from the button's onclick, so iPhone/Android
        // count it as the visitor's own tap - permission is asked before anything else.
        visitorPush: async function (btn) {
            var done = function (t) { if (btn) { btn.textContent = t; btn.disabled = true; } };
            try {
                if (!('Notification' in window) || !('serviceWorker' in navigator) || !('PushManager' in window)) {
                    return done('Din browser kan ikke give besked – skriv din e-mail i stedet');
                }
                var perm = await Notification.requestPermission();
                if (perm !== 'granted') return done('Ikke slået til – skriv din e-mail i stedet');
                var r = await reg();
                var key = await (await fetch('/api/push/public-key')).text();
                var sub = await r.pushManager.getSubscription()
                       || await r.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64ToBytes(key) });
                var j = sub.toJSON();
                var res = await fetch('/api/push/visitor', {
                    method: 'POST', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ visitorKey: localStorage.getItem('hjem-chat') || '', endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth })
                });
                done(res.ok ? '✅ Du får besked her, når Martin svarer' : 'Det lykkedes ikke – skriv din e-mail i stedet');
            } catch (e) { done('Det lykkedes ikke – skriv din e-mail i stedet'); }
        },

        pushTest: async function () {
            await fetch('/api/push/test', { method: 'POST' });
        }
    };
})();

// Example pictures (a.hj-zoom) open big on top of the page instead of a new tab - on a phone people
// had to close that tab to get back (user 2026-10-22). Close: Luk button, tap anywhere, Esc or back.
(function () {
    var box = null;
    function close(fromBack) {
        if (!box) return;
        box.remove(); box = null;
        document.body.style.overflow = '';
        if (!fromBack && history.state && history.state.hjZoom) history.back();
    }
    document.addEventListener('click', function (e) {
        var a = e.target.closest && e.target.closest('a.hj-zoom');
        if (!a || e.ctrlKey || e.metaKey || e.shiftKey) return;
        e.preventDefault();
        var img = a.querySelector('img');
        box = document.createElement('div');
        box.className = 'hj-zoom-box';
        box.setAttribute('role', 'dialog');
        box.setAttribute('aria-label', 'Billedet i stor størrelse');
        box.innerHTML = '<button type="button" class="k-btn k-btn-primary hj-zoom-close">Luk ✕</button><img alt="">'
            + '<div class="hj-zoom-help">Tryk hvor som helst for at lukke</div>';
        var big = box.querySelector('img');
        big.src = a.getAttribute('href');
        big.alt = img ? img.alt : '';
        box.addEventListener('click', function () { close(false); });
        document.body.appendChild(box);
        document.body.style.overflow = 'hidden';
        history.pushState({ hjZoom: true }, '');
        box.querySelector('button').focus();
    }, true);   // capture: before Blazor's link handling
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') close(false); });
    window.addEventListener('popstate', function () { close(true); });
})();
