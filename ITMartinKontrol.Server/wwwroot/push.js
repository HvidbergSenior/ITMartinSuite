// Subscribe this phone to Kontrol alarms (needs the PIN, same as start/stop).
window.kpush = (function () {
    function b64ToBytes(b64) { var pad = '='.repeat((4 - b64.length % 4) % 4); var raw = atob((b64 + pad).replace(/-/g, '+').replace(/_/g, '/')); var out = new Uint8Array(raw.length); for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i); return out; }
    async function reg() { if (!('serviceWorker' in navigator)) return null; return await navigator.serviceWorker.register('/sw.js'); }
    return {
        state: async function () {
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) return 'unsupported';
            if (Notification.permission === 'denied') return 'denied';
            var r = await reg(); var sub = r ? await r.pushManager.getSubscription() : null; return sub ? 'on' : 'off';
        },
        subscribe: async function (pin) {
            var r = await reg(); if (!r) return 'unsupported';
            if ((await Notification.requestPermission()) !== 'granted') return 'denied';
            var key = await (await fetch('/api/push/public-key')).text();
            var sub = await r.pushManager.getSubscription() || await r.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64ToBytes(key) });
            var j = sub.toJSON();
            var res = await fetch('/api/push/subscribe', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Kontrol-Pin': pin }, body: JSON.stringify({ endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth, name: navigator.userAgent.slice(0, 40) }) });
            return res.ok ? 'on' : (res.status === 401 ? 'pin' : 'error');
        },
        unsubscribe: async function () {
            var r = await reg(); var sub = r ? await r.pushManager.getSubscription() : null;
            if (sub) { await fetch('/api/push/unsubscribe', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ endpoint: sub.endpoint }) }); await sub.unsubscribe(); }
            return 'off';
        },
        test: async function (pin) { var res = await fetch('/api/push/test', { method: 'POST', headers: { 'X-Kontrol-Pin': pin } }); return res.ok; }
    };
})();
