self.addEventListener('push', function (event) {
    if (!event.data) return;
    var d = {};
    try { d = event.data.json(); } catch (e) { d = { title: 'ITKolibri', body: event.data.text() }; }
    event.waitUntil(
        self.registration.showNotification(d.title || 'ITKolibri · Upload', {
            body: d.body || '',
            icon: '/kolibri-icon.svg',
            badge: '/kolibri-icon.svg',
            data: { url: d.url || '/' },
            vibrate: [200, 100, 200]
        })
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    var target = (event.notification.data && event.notification.data.url) || '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
            for (var c of list) { if ('focus' in c) return c.focus(); }
            if (clients.openWindow) return clients.openWindow(target);
        })
    );
});
