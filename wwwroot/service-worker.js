// Service worker for AirCode PWA
// Version specifically designed for GitHub Pages deployment

// Cache name with version - bump this when deploying changes
const CACHE_NAME = 'aircode-cache-v7';

// Get the base URL from service worker's location
const baseUrl = self.registration.scope;

self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install started');
    self.skipWaiting();

    // Dynamically build the URLs to cache based on the base URL
    const urlsToCache = [
        baseUrl,
        new URL('index.html', baseUrl).href,
        new URL('404.html', baseUrl).href,
        new URL('favicon.png', baseUrl).href,
        new URL('css/app.css', baseUrl).href,
        new URL('css/bootstrap/bootstrap.min.css', baseUrl).href,
        new URL('css/colors.css', baseUrl).href,
        new URL('css/responsive.css', baseUrl).href,
        new URL('_framework/blazor.webassembly.js', baseUrl).href,
        new URL('js/debug.js', baseUrl).href,
        new URL('js/connectivityServices.js', baseUrl).href,
        new URL('js/themeSwitcher.js', baseUrl).href,
        new URL('manifest.json', baseUrl).href
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

    // Log the request URL for debugging
    console.log('[ServiceWorker] Fetching:', event.request.url);

    // Handle the fetch
    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    // Return cached response
                    console.log('[ServiceWorker] Returning cached:', event.request.url);
                    return cachedResponse;
                }

                // Not in cache, fetch from network
                return fetch(event.request)
                    .then(response => {
                        // Only cache same-origin responses
                        if (response && response.status === 200 &&
                            (event.request.url.startsWith(self.location.origin) ||
                                event.request.url.includes('github.io'))) {
                            const responseToCache = response.clone();
                            caches.open(CACHE_NAME)
                                .then(cache => {
                                    console.log('[ServiceWorker] Caching new resource:', event.request.url);
                                    cache.put(event.request, responseToCache);
                                });
                        }
                        return response;
                    })
                    .catch(error => {
                        console.log('[ServiceWorker] Fetch failed:', error, event.request.url);

                        // For navigation requests, try to return index.html
                        if (event.request.mode === 'navigate') {
                            return caches.match(new URL('index.html', baseUrl).href);
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