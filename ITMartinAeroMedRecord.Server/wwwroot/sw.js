self.addEventListener('push', function (event) {
    if (!event.data) return;
    var d = event.data.json();
    event.waitUntil(
        self.registration.showNotification(d.title || 'Formidling', {
            body: d.body || '',
            icon: '/icon.svg',
            badge: '/icon.svg',
            vibrate: [200, 100, 200]
        })
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
            for (var c of list) { if ('focus' in c) return c.focus(); }
            if (clients.openWindow) return clients.openWindow('/');
        })
    );
});
