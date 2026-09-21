// Kontrol service worker: shows alarm pushes, opens Kontrol when tapped. No page caching.
self.addEventListener('install', function () { self.skipWaiting(); });
self.addEventListener('activate', function (e) { e.waitUntil(self.clients.claim()); });
self.addEventListener('push', function (e) {
    var data = { title: 'Kontrol', body: '', url: '/' };
    try { data = Object.assign(data, e.data.json()); } catch (err) { }
    e.waitUntil(self.registration.showNotification(data.title, { body: data.body, data: { url: data.url }, tag: 'kontrol-' + Date.now(), requireInteraction: data.title.indexOf('rødt') >= 0 }));
});
self.addEventListener('notificationclick', function (e) {
    e.notification.close();
    var url = (e.notification.data && e.notification.data.url) || '/';
    e.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
        for (var i = 0; i < list.length; i++) { if ('focus' in list[i]) { list[i].navigate(url); return list[i].focus(); } }
        return self.clients.openWindow(url);
    }));
});
