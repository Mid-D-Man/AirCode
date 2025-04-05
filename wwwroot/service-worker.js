// Base path handling for GitHub Pages
const baseUrl = '/AirCode';

const CACHE_NAME = 'aircode-cache-v2'; // Updated version to force refresh
const urlsToCache = [
    `${baseUrl}/`,
    `${baseUrl}/index.html`,
    `${baseUrl}/404.html`,
    `${baseUrl}/favicon.png`,
    `${baseUrl}/icon-192.png`,
    `${baseUrl}/icon-512.png`,
    `${baseUrl}/css/bootstrap/bootstrap.min.css`,
    `${baseUrl}/css/app.css`,
    `${baseUrl}/css/colors.css`,
    `${baseUrl}/css/responsive.css`,
    `${baseUrl}/js/themeSwitcher.js`,
    `${baseUrl}/js/connectivityServices.js`,
    `${baseUrl}/_framework/blazor.webassembly.js`,
    // Add other essential assets
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
                    // Remove old caches
                    if (cacheName !== CACHE_NAME) {
                        console.log('[ServiceWorker] Removing old cache:', cacheName);
                        return caches.delete(cacheName);
                    }
                })
            );
        }).then(() => {
            return self.clients.claim(); // Take control immediately
        })
    );
});

self.addEventListener('fetch', (event) => {
    // Special handling for favicon requests
    if (event.request.url.includes('favicon')) {
        event.respondWith(
            caches.match(`${baseUrl}/favicon.png`).then(response => {
                return response || fetch(event.request);
            })
        );
        return;
    }

    const url = new URL(event.request.url);

    event.respondWith(
        caches.match(event.request).then((response) => {
            // Return cached asset if available
            if (response) {
                return response;
            }

            // For navigation requests, return index.html if no match found
            if (event.request.mode === 'navigate') {
                return caches.match(`${baseUrl}/index.html`);
            }

            // Otherwise, fetch from network
            return fetch(event.request).then((networkResponse) => {
                if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
                    return networkResponse;
                }

                const responseToCache = networkResponse.clone();

                // Cache resources from our domain
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
                // Return cached index.html for navigation failures
                if (event.request.mode === 'navigate') {
                    return caches.match(`${baseUrl}/index.html`);
                }
            });
        })
    );
});