// Service worker: makes the page installable and shows chat pushes to the owner.
// No page caching - the timeline and the chat must always be fresh.
self.addEventListener('install', function () { self.skipWaiting(); });
self.addEventListener('activate', function (e) { e.waitUntil(self.clients.claim()); });
self.addEventListener('fetch', function () { });

self.addEventListener('push', function (e) {
    var data = { title: 'Martin Hvidberg', body: '', url: '/admin' };
    try { data = Object.assign(data, e.data.json()); } catch (err) { }
    e.waitUntil(self.registration.showNotification(data.title, {
        body: data.body,
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        data: { url: data.url },
        tag: 'hjem-chat',
        renotify: true
    }));
});

self.addEventListener('notificationclick', function (e) {
    e.notification.close();
    var url = (e.notification.data && e.notification.data.url) || '/admin';
    e.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
        // iPhone: navigate() on an already-open app can fail - then open the page fresh.
        for (var i = 0; i < list.length; i++) {
            var c = list[i];
            if ('focus' in c && 'navigate' in c) {
                return c.focus().then(function (w) { return w.navigate(url); }).catch(function () { return self.clients.openWindow(url); });
            }
        }
        return self.clients.openWindow(url);
    }));
});
