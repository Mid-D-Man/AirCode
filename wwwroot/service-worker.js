// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
//self.addEventListener('fetch', () => { });


// Base path handling for GitHub Pages
const baseUrl = self.location.pathname.replace(/\/service-worker\.js$/, '');


const CACHE_NAME = 'aircode-cache-v1';
const urlsToCache = [
    '/', // Your index.html
    '/index.html',
    '/css/bootstrap/bootstrap.min.css',
    '/css/app.css',
    '/css/colors.css',
    '/css/responsive.css',
    '/icons/favicon.png',
    '/icons/icon-512.png',
    '/icons/icon-192.png',
    '/js/themeSwitcher.js',
    '/js/connectivityServices.js',
    // Add any other essential assets, like fonts or additional scripts
];

self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install');
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
                    // Remove old caches that don’t match the current CACHE_NAME
                    if (cacheName !== CACHE_NAME) {
                        console.log('[ServiceWorker] Removing old cache:', cacheName);
                        return caches.delete(cacheName);
                    }
                })
            );
        })
    );
});

self.addEventListener('fetch', (event) => {
    event.respondWith(
        caches.match(event.request).then((response) => {
            // Return asset from cache if available
            if (response) {
                return response;
            }
            // Otherwise, fetch from the network
            return fetch(event.request).then((networkResponse) => {
                // If the network response is valid, clone it
                if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
                    return networkResponse;
                }
                const responseToCache = networkResponse.clone();
                // Use event.waitUntil to ensure caching is attempted, but don't block the response
                event.waitUntil(
                    caches.open(CACHE_NAME).then((cache) => {
                        return cache.put(event.request, responseToCache);
                    }).catch(err => {
                        console.error('Error caching response:', err);
                    })
                );
                return networkResponse;
            });
        })
    );
});
