// Web Push helpers for FileSorter - adapted from ITMartinFamily.Server's
// wwwroot/js/family.js (registerServiceWorker/requestPushPermission/
// subscribeForPush pattern), trimmed to FileSorter's single-subscription
// (no family/member scoping) needs.
window.fileSorterPush = {
    registerServiceWorker: async function () {
        if (!('serviceWorker' in navigator)) return;
        try { await navigator.serviceWorker.register('/sw.js'); } catch (e) { }
    },

    requestPushPermission: async function () {
        if (!('Notification' in window)) return 'not-supported';
        if (Notification.permission !== 'default') return Notification.permission;
        return await Notification.requestPermission();
    },

    // Returns the current subscription's endpoint (or null), so the UI can
    // reflect whether this browser is already subscribed without a server
    // round trip on every page load.
    getSubscriptionEndpoint: async function () {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;
        try {
            var reg = await navigator.serviceWorker.ready;
            var sub = await reg.pushManager.getSubscription();
            return sub ? sub.endpoint : null;
        } catch (e) { return null; }
    },

    subscribeForPush: async function (publicKey) {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;
        try {
            var reg = await navigator.serviceWorker.ready;
            var existing = await reg.pushManager.getSubscription();
            var sub = existing || await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: fileSorterPush._urlBase64ToUint8Array(publicKey)
            });
            var json = sub.toJSON();
            await fetch('/api/push/subscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    endpoint: json.endpoint,
                    p256dh: json.keys.p256dh,
                    auth: json.keys.auth
                })
            });
            return json.endpoint;
        } catch (e) { return null; }
    },

    unsubscribeFromPush: async function () {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;
        try {
            var reg = await navigator.serviceWorker.ready;
            var sub = await reg.pushManager.getSubscription();
            if (!sub) return;
            var endpoint = sub.endpoint;
            await sub.unsubscribe();
            await fetch('/api/push/unsubscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ endpoint: endpoint })
            });
        } catch (e) { }
    },

    _urlBase64ToUint8Array: function (base64String) {
        var padding = '='.repeat((4 - base64String.length % 4) % 4);
        var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        var raw = atob(base64);
        var arr = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) arr[i] = raw.charCodeAt(i);
        return arr;
    }
};
