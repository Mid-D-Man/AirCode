// Base path handling for GitHub Pages
const baseUrl = '/AirCode';

// Smaller cache list to avoid potential issues
const CACHE_NAME = 'aircode-cache-v3';
const urlsToCache = [
    `${baseUrl}/`,
    `${baseUrl}/index.html`,
    `${baseUrl}/404.html`,
    `${baseUrl}/favicon.png`,
    `${baseUrl}/css/app.css`
];

self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install');
    self.skipWaiting();
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                console.log('[ServiceWorker] Caching core assets');
                return cache.addAll(urlsToCache);
            })
    );
});

self.addEventListener('activate', (event) => {
    console.log('[ServiceWorker] Activate');
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cacheName => {
                    if (cacheName !== CACHE_NAME) {
                        return caches.delete(cacheName);
                    }
                })
            );
        }).then(() => self.clients.claim())
    );
});

// Simplified fetch handler that falls back to network for most requests
self.addEventListener('fetch', (event) => {
    // Only cache same-origin requests
    if (event.request.url.startsWith(self.location.origin)) {
        event.respondWith(
            caches.match(event.request).then(cachedResponse => {
                if (cachedResponse) {
                    return cachedResponse;
                }

                return fetch(event.request).catch(() => {
                    // For navigation, return the cached index page as fallback
                    if (event.request.mode === 'navigate') {
                        return caches.match(`${baseUrl}/index.html`);
                    }
                });
            })
        );
    }
});