// Service worker for AirCode PWA
// This version uses relative paths instead of absolute paths with baseUrl

// Cache name with version
const CACHE_NAME = 'aircode-cache-v4';

// URLs to cache - using relative paths to work with any base URL
const urlsToCache = [
    './',
    './index.html',
    './404.html',
    './favicon.png',
    './css/app.css',
    './_framework/blazor.webassembly.js'
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
            .catch(error => {
                console.error('[ServiceWorker] Cache failed:', error);
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
                        console.log('[ServiceWorker] Clearing old cache:', cacheName);
                        return caches.delete(cacheName);
                    }
                })
            );
        }).then(() => {
            console.log('[ServiceWorker] Claiming clients');
            return self.clients.claim();
        })
    );
});

// Improved fetch handler with better error handling
self.addEventListener('fetch', (event) => {
    // Skip non-GET requests
    if (event.request.method !== 'GET') return;

    // Handle the fetch
    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    // Return cached response
                    return cachedResponse;
                }

                // Not in cache, fetch from network
                return fetch(event.request)
                    .then(response => {
                        // Cache valid responses for future
                        if (response && response.status === 200) {
                            const responseToCache = response.clone();
                            caches.open(CACHE_NAME)
                                .then(cache => {
                                    cache.put(event.request, responseToCache);
                                });
                        }
                        return response;
                    })
                    .catch(error => {
                        console.log('[ServiceWorker] Fetch failed:', error);

                        // For navigation requests, return index.html as fallback
                        if (event.request.mode === 'navigate') {
                            return caches.match('./index.html');
                        }

                        // Return a proper error for other requests
                        return new Response('Network error', {
                            status: 408,
                            headers: new Headers({ 'Content-Type': 'text/plain' })
                        });
                    });
            })
    );
});