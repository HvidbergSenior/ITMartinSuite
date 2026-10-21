// Browser side of notifications. Blazor calls these via JS interop.
window.elpush = (function () {
    function b64ToBytes(b64) {
        var pad = '='.repeat((4 - b64.length % 4) % 4);
        var raw = atob((b64 + pad).replace(/-/g, '+').replace(/_/g, '/'));
        var out = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }

    async function reg() {
        if (!('serviceWorker' in navigator)) return null;
        return await navigator.serviceWorker.register('/sw.js');
    }

    return {
        // "unsupported" | "ios-needs-homescreen" | "denied" | "off" | "on"
        state: async function () {
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

        // This phone's own record on the server, or null if it forgot us.
        status: async function () {
            var r = await reg();
            var sub = r ? await r.pushManager.getSubscription() : null;
            if (!sub) return null;
            var res = await fetch('/api/push/status?endpoint=' + encodeURIComponent(sub.endpoint));
            return res.ok ? await res.json() : null;
        },

        endpoint: async function () {
            var r = await reg();
            var sub = r ? await r.pushManager.getSubscription() : null;
            return sub ? sub.endpoint : null;
        },

        subscribe: async function (name, cheapest, expensive, code) {
            var r = await reg();
            var perm = await Notification.requestPermission();
            if (perm !== 'granted') return 'denied';
            var key = await (await fetch('/api/push/public-key')).text();
            var sub = await r.pushManager.getSubscription()
                   || await r.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64ToBytes(key) });
            var j = sub.toJSON();
            var res = await fetch('/api/push/subscribe', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth, name: name, notifyCheapest: cheapest, notifyExpensive: expensive, code: code || null })
            });
            return res.ok ? 'on' : res.status === 400 ? 'code' : 'error';
        },

        unsubscribe: async function () {
            var r = await reg();
            var sub = r ? await r.pushManager.getSubscription() : null;
            if (sub) {
                await fetch('/api/push/unsubscribe', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ endpoint: sub.endpoint }) });
                await sub.unsubscribe();
            }
            return 'off';
        },

        test: async function () {
            var r = await reg();
            var sub = r ? await r.pushManager.getSubscription() : null;
            if (!sub) return false;
            var res = await fetch('/api/push/test', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ endpoint: sub.endpoint }) });
            return res.ok;
        }
    };
})();

// Your region and tariffs, kept on this phone only (one-year cookie).
window.elprefs = {
    save: function (name, value) {
        document.cookie = name + "=" + value + ";path=/;max-age=31536000;samesite=lax";
    }
};
