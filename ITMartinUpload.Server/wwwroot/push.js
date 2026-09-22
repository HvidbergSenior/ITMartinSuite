// Web push sign-up. Called from the page with the customer slug (or "martin")
// and the VAPID public key; returns a short Danish status the page can show.
export async function subscribe(who, token, publicKey) {
    return subscribeCore(who, token ? `?token=${encodeURIComponent(token)}` : '', publicKey);
}

async function subscribeCore(who, query, publicKey) {
    if (!('serviceWorker' in navigator) || !('PushManager' in window))
        return 'Din browser kan ikke sende beskeder. Prøv Chrome eller Safari på telefonen.';

    try {
        const reg = await navigator.serviceWorker.register('/sw.js');
        const permission = await Notification.requestPermission();
        if (permission !== 'granted')
            return 'Du sagde nej til beskeder. Du kan slå dem til igen i browserens indstillinger.';

        const existing = await reg.pushManager.getSubscription();
        const sub = existing ?? await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(publicKey)
        });

        const json = sub.toJSON();
        const res = await fetch(`/api/push/${encodeURIComponent(who)}${query}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                endpoint: sub.endpoint,
                p256dh: json.keys.p256dh,
                auth: json.keys.auth,
                who: who
            })
        });

        return res.ok
            ? 'Beskeder er slået til på denne enhed.'
            : 'Kunne ikke slå beskeder til. Prøv igen.';
    } catch (e) {
        return 'Kunne ikke slå beskeder til: ' + (e && e.message ? e.message : e);
    }
}

// Martin signs up with his admin pin instead of a customer token.
export async function subscribeMartin(pin, publicKey) {
    return subscribeCore('martin', `?pin=${encodeURIComponent(pin)}`, publicKey);
}

function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = window.atob(base64);
    return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
}
