self.addEventListener('push', function (event) {
    if (!event.data) return;
    var d = event.data.json();
    event.waitUntil(
        self.registration.showNotification(d.title || 'Club', {
            body: d.body || '',
            icon: '/icon.svg',
            badge: '/icon.svg',
            vibrate: [200, 100, 200],
            data: { url: d.url || '/' }
        })
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    var url = (event.notification.data && event.notification.data.url) || '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
            // A message push opens that message; other pushes just bring Club to the front as before.
            if (url !== '/' && clients.openWindow) return clients.openWindow(url);
            for (var c of list) { if ('focus' in c) return c.focus(); }
            if (clients.openWindow) return clients.openWindow('/');
        })
    );
});
