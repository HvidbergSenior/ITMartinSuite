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
        // bogshoppen.dk/produkter: the categories the visitor chose, remembered for the next visit (a cookie, so the
        // server can open the page with them at once - no jump). Empty = forget.
        // Rewrites the address bar only (no page change, keeps the history state the picture zoom uses).
        // Brings an element (the product list) to the top of the screen after a new choice.
        scrollToId: function (id) {
            var el = document.getElementById(id);
            if (el && el.getBoundingClientRect().top < 0) el.scrollIntoView({ block: 'start' });
        },
        setAddress: function (url) {
            try { history.replaceState(history.state, '', url); } catch (e) { }
        },
        rememberCategories: function (v) {
            document.cookie = 'bs-vis=' + encodeURIComponent(v || '') + ';path=/;samesite=lax;max-age=' + (v ? 31536000 : 0);
        },
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

        // "Skriv til mig" (2026-11-05): name + first message -> Hjem asks Snak for a room -> the visitor goes into it.
        // From the /chat bubble (an iframe on itmartin.dk) the whole page goes, not just the little frame.
        snakStart: function (form) {
            var btn = form.querySelector('button[type=submit]'), err = form.querySelector('.hj-snak-fejl');
            var navn = form.navn.value.trim(), besked = form.besked.value.trim();
            err.hidden = true; btn.disabled = true; btn.textContent = 'Laver dit rum …';
            fetch('/api/snak/start', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ navn: navn, besked: besked })
            }).then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
              .then(function (x) {
                  if (!x.ok || !x.j.link) throw new Error(x.j.fejl || '');
                  var to = x.j.link + '?navn=' + encodeURIComponent(navn);
                  try { window.top.location.href = to; } catch (e) { location.href = to; }
              })
              .catch(function (e) {
                  err.textContent = e.message || 'Det virkede ikke lige nu. Prøv igen om lidt – eller ring eller skriv en mail.';
                  err.hidden = false; btn.disabled = false; btn.textContent = 'Start snakken';
              });
            return false;
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
        box.innerHTML = '<button type="button" class="k-btn hj-zoom-close">Luk ✕</button><img alt="">'
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
