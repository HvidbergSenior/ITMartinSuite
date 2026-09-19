// Service worker: receives pushes and opens the app when tapped. No caching
// of pages - prices change, and a stale "now" price is worse than a spinner.
self.addEventListener('install', function () { self.skipWaiting(); });
self.addEventListener('activate', function (e) { e.waitUntil(self.clients.claim()); });

self.addEventListener('push', function (e) {
    var data = { title: 'MinElpris', body: '', url: '/' };
    try { data = Object.assign(data, e.data.json()); } catch (err) { }
    e.waitUntil(self.registration.showNotification(data.title, {
        body: data.body,
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        data: { url: data.url },
        tag: 'mitel',
        renotify: true
    }));
});

self.addEventListener('notificationclick', function (e) {
    e.notification.close();
    var url = (e.notification.data && e.notification.data.url) || '/';
    e.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
        for (var i = 0; i < list.length; i++) {
            if ('focus' in list[i]) { list[i].navigate(url); return list[i].focus(); }
        }
        return self.clients.openWindow(url);
    }));
});
