// Service worker for AirCode PWA
// Version specifically designed for GitHub Pages deployment

// Cache name with version
const CACHE_NAME = 'aircode-cache-v6';

// Get the base URL from service worker's location
const baseUrl = self.registration.scope;

self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install started');
    self.skipWaiting();

    // Dynamically build the URLs to cache based on the base URL
    const urlsToCache = [
        baseUrl,
        new URL('index.html', baseUrl),
        new URL('404.html', baseUrl),
        new URL('favicon.png', baseUrl),
        new URL('css/app.css', baseUrl),
        new URL('css/bootstrap/bootstrap.min.css', baseUrl),
        new URL('css/colors.css', baseUrl),
        new URL('css/responsive.css', baseUrl),
        new URL('_framework/blazor.webassembly.js', baseUrl),
        new URL('js/debug.js', baseUrl),
        new URL('js/connectivityServices.js', baseUrl),
        new URL('js/themeSwitcher.js', baseUrl),
        new URL('manifest.json', baseUrl)
    ];

    console.log('[ServiceWorker] URLs to cache:', urlsToCache);

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

// Improved fetch handler with better error handling and path normalization
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

                        // For navigation requests, try to return index.html
                        if (event.request.mode === 'navigate') {
                            return caches.match(new URL('index.html', baseUrl));
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

// Add a message handler to receive and log messages from the main page
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'LOG') {
        console.log('[ServiceWorker] From page:', event.data.message);
    }
});

console.log('[ServiceWorker] Initialized with scope:', self.registration.scope);