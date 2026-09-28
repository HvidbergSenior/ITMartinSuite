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

        pushTest: async function () {
            await fetch('/api/push/test', { method: 'POST' });
        }
    };
})();
