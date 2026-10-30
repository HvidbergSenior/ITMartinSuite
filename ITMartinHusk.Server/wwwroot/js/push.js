// Browser side of Husk's beskeder (the same flow as ElPriser's push.js). Blazor calls these via JS interop.
window.huskpush = (function () {
    function b64ToBytes(b64) {
        var pad = '='.repeat((4 - b64.length % 4) % 4);
        var raw = atob((b64 + pad).replace(/-/g, '+').replace(/_/g, '/'));
        var out = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }
    async function reg() { return 'serviceWorker' in navigator ? await navigator.serviceWorker.register('/sw.js') : null; }
    async function current() { var r = await reg(); return r ? await r.pushManager.getSubscription() : null; }
    function post(url, body) { return fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); }

    return {
        // "unsupported" | "ios-needs-homescreen" | "denied" | "off" | "on"
        state: async function () {
            if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
                var isIOS = /iPhone|iPad|iPod/.test(navigator.userAgent);
                var standalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
                return isIOS && !standalone ? 'ios-needs-homescreen' : 'unsupported';
            }
            if (Notification.permission === 'denied') return 'denied';
            var sub = await current();
            if (!sub) return 'off';
            var res = await fetch('/api/push/status?endpoint=' + encodeURIComponent(sub.endpoint));
            return res.ok ? 'on' : 'off';
        },
        subscribe: async function () {
            var r = await reg();
            if (await Notification.requestPermission() !== 'granted') return 'denied';
            var key = await (await fetch('/api/push/public-key')).text();
            var sub = await r.pushManager.getSubscription()
                   || await r.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64ToBytes(key) });
            var j = sub.toJSON();
            var res = await post('/api/push/subscribe', { endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth });
            return res.ok ? 'on' : 'off';
        },
        unsubscribe: async function () {
            var sub = await current();
            if (sub) { await post('/api/push/unsubscribe', { endpoint: sub.endpoint }); await sub.unsubscribe(); }
            return 'off';
        },
        test: async function () {
            var sub = await current();
            return sub ? (await post('/api/push/test', { endpoint: sub.endpoint })).ok : false;
        }
    };
})();
