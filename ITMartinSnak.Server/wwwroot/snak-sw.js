// Snak service worker: makes the app installable and shows the push messages.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => {});

self.addEventListener('push', e => {
    let d = {};
    try { d = e.data ? e.data.json() : {}; } catch { d = { title: 'Snak', body: e.data ? e.data.text() : '' }; }
    e.waitUntil((async () => {
        // No banner while the app is open in front of you - you can already see the message.
        const wins = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        if (wins.some(w => w.visibilityState === 'visible' && w.focused)) return;
        await self.registration.showNotification(d.title || 'Snak', {
            body: d.body || '',
            icon: '/_content/ITMartin.Shared.UI/kolibri-icon.svg',
            badge: '/_content/ITMartin.Shared.UI/kolibri-icon.svg',
            tag: d.tag || 'snak',
            renotify: true,
            data: { url: d.url || '/' },
        });
    })());
});

self.addEventListener('notificationclick', e => {
    e.notification.close();
    const url = (e.notification.data && e.notification.data.url) || '/';
    e.waitUntil((async () => {
        const wins = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const w of wins) { if ('focus' in w) return w.focus(); }
        return self.clients.openWindow(url);
    })());
});
