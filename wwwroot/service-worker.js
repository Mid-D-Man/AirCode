// Base path handling for GitHub Pages
const baseUrl = '/AirCode';

const CACHE_NAME = 'aircode-cache-v1.2'; // Incremented version to force cache refresh
const urlsToCache = [
    `${baseUrl}/`,
    `${baseUrl}/index.html`,
    `${baseUrl}/404.html`,
    `${baseUrl}/favicon.ico`,
    `${baseUrl}/css/bootstrap/bootstrap.min.css`,
    `${baseUrl}/css/app.css`,
    `${baseUrl}/css/colors.css`,
    `${baseUrl}/css/responsive.css`,
    `${baseUrl}/icons/icon-512.png`,
    `${baseUrl}/icons/icon-192.png`,
    `${baseUrl}/js/themeSwitcher.js`,
    `${baseUrl}/js/connectivityServices.js`,
    `${baseUrl}/_framework/blazor.webassembly.js`,
];

self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install');
    self.skipWaiting(); // Force activation
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                console.log('[ServiceWorker] Caching app shell');
                return cache.addAll(urlsToCache);
            })
            .catch(err => console.error('[ServiceWorker] Failed to cache during install:', err))
    );
});

self.addEventListener('activate', (event) => {
    console.log('[ServiceWorker] Activate');
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cacheName => {
                    if (cacheName !== CACHE_NAME) {
                        console.log('[ServiceWorker] Removing old cache:', cacheName);
                        return caches.delete(cacheName);
                    }
                })
            );
        }).then(() => {
            return self.clients.claim();
        })
    );
});

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // Special handling for favicon requests at root
    if (event.request.url.includes('favicon.ico')) {
        event.respondWith(
            caches.match(`${baseUrl}/favicon.ico`)
                .then(response => {
                    return response || fetch(event.request);
                })
                .catch(() => {
                    // If everything fails, return a simple transparent image
                    return new Response(
                        new Blob([
                            // 1x1 transparent PNG
                            atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=')
                        ], {
                            type: 'image/png'
                        })
                    );
                })
        );
        return;
    }

    event.respondWith(
        caches.match(event.request)
            .then((response) => {
                if (response) {
                    return response;
                }

                // For navigation requests, try index.html
                if (event.request.mode === 'navigate') {
                    return caches.match(`${baseUrl}/index.html`);
                }

                return fetch(event.request)
                    .then((networkResponse) => {
                        if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
                            return networkResponse;
                        }

                        const responseToCache = networkResponse.clone();
                        if (url.origin === self.location.origin) {
                            caches.open(CACHE_NAME)
                                .then((cache) => {
                                    cache.put(event.request, responseToCache);
                                });
                        }

                        return networkResponse;
                    })
                    .catch(error => {
                        console.error('[ServiceWorker] Fetch failed:', error);
                        if (event.request.mode === 'navigate') {
                            return caches.match(`${baseUrl}/index.html`);
                        }
                    });
            })
    );
});