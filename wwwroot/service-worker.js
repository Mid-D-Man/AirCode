// Base path handling for GitHub Pages
const baseUrl = '/AirCode';

const CACHE_NAME = 'aircode-cache-v1.1'; // Incremented version to force cache refresh
const urlsToCache = [
    `${baseUrl}/`,
    `${baseUrl}/index.html`,
    `${baseUrl}/404.html`,
    `${baseUrl}/favicon.ico`,
    `${baseUrl}/favicon.png`,
    `${baseUrl}/css/bootstrap/bootstrap.min.css`,
    `${baseUrl}/css/app.css`,
    `${baseUrl}/css/colors.css`,
    `${baseUrl}/css/responsive.css`,
    `${baseUrl}/icons/favicon.png`,
    `${baseUrl}/icons/icon-512.png`,
    `${baseUrl}/icons/icon-192.png`,
    `${baseUrl}/js/themeSwitcher.js`,
    `${baseUrl}/js/connectivityServices.js`,
    `${baseUrl}/_framework/blazor.webassembly.js`,
    // Add any other essential assets, like fonts or additional scripts
];

self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install');
    self.skipWaiting(); // Force the waiting service worker to become the active service worker
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
                    // Remove old caches that don't match the current CACHE_NAME
                    if (cacheName !== CACHE_NAME) {
                        console.log('[ServiceWorker] Removing old cache:', cacheName);
                        return caches.delete(cacheName);
                    }
                })
            );
        }).then(() => {
            // Tell the active service worker to take control of the page immediately
            return self.clients.claim();
        })
    );
});

self.addEventListener('fetch', (event) => {
    // Special handling for favicon.ico requests
    if (event.request.url.endsWith('favicon.ico')) {
        event.respondWith(
            caches.match(`${baseUrl}/favicon.ico`).then(response => {
                return response || fetch(event.request);
            })
        );
        return;
    }

    // Check if the request URL starts with the base URL
    const url = new URL(event.request.url);

    event.respondWith(
        caches.match(event.request).then((response) => {
            // Return asset from cache if available
            if (response) {
                return response;
            }

            // For navigation requests, return the index.html if no match is found
            if (event.request.mode === 'navigate') {
                return caches.match(`${baseUrl}/index.html`);
            }

            // Otherwise, fetch from the network
            return fetch(event.request).then((networkResponse) => {
                // If the network response is valid, clone it
                if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
                    return networkResponse;
                }

                const responseToCache = networkResponse.clone();

                // Only cache resources from our app (not external resources)
                if (url.origin === self.location.origin) {
                    event.waitUntil(
                        caches.open(CACHE_NAME).then((cache) => {
                            return cache.put(event.request, responseToCache);
                        }).catch(err => {
                            console.error('Error caching response:', err);
                        })
                    );
                }

                return networkResponse;
            }).catch(error => {
                console.error('[ServiceWorker] Fetch failed:', error);
                // For navigation failures, return the cached index.html
                if (event.request.mode === 'navigate') {
                    return caches.match(`${baseUrl}/index.html`);
                }
            });
        })
    );
});