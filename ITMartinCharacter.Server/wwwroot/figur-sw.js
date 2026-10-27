// Character service worker: only makes the app installable on the home screen (no offline copy - the figure lives on the server).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => {});
