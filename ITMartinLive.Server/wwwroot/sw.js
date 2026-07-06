self.addEventListener('push', e => {
    const data = e.data ? e.data.json() : { title: 'Live', body: 'Ny opdatering' };
    e.waitUntil(self.registration.showNotification(data.title, {
        body: data.body,
        icon: '/favicon.png',
        badge: '/favicon.png',
        vibrate: [100, 50, 100],
        data: { url: self.location.origin }
    }));
});

self.addEventListener('notificationclick', e => {
    e.notification.close();
    e.waitUntil(clients.openWindow(e.notification.data?.url ?? '/'));
});
