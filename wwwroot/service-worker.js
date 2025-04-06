// Service worker for AirCode PWA
// Version specifically designed for GitHub Pages deployment

// Cache name with version - bump this when deploying changes
const CACHE_NAME = 'aircode-cache-v8';

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

// Improved fetch handler with better SPA support
self.addEventListener('fetch', (event) => {
    // Skip non-GET requests
    if (event.request.method !== 'GET') return;

    // Parse the URL
    const url = new URL(event.request.url);

    // Skip external requests that aren't to our site
    if (!url.origin.includes('github.io') &&
        url.origin !== self.location.origin) {
        return;
    }

    // Handle the fetch
    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    // Return cached response
                    return cachedResponse;
                }

                // Special handling for navigation requests
                if (event.request.mode === 'navigate') {
                    return fetch(event.request)
                        .then(response => {
                            // If we got a 404 for a navigation request, serve index.html
                            if (response.status === 404) {
                                console.log('[ServiceWorker] Serving SPA index for route:', event.request.url);
                                return caches.match(new URL('index.html', baseUrl).href);
                            }
                            return response;
                        })
                        .catch(error => {
                            console.log('[ServiceWorker] Navigation fetch failed:', error);
                            return caches.match(new URL('index.html', baseUrl).href);
                        });
                }

                // For favicon and other icon requests, try different paths
                if (url.pathname.includes('favicon') || url.pathname.includes('icon')) {
                    return fetch(event.request)
                        .catch(() => {
                            // Try alternate locations
                            const paths = [
                                new URL('favicon.png', baseUrl).href,
                                new URL('icon-192.png', baseUrl).href
                            ];

                            return Promise.any(
                                paths.map(path => fetch(path))
                            ).catch(() => {
                                // If all fail, return a blank image
                                return new Response(
                                    'R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7',
                                    { headers: {'Content-Type': 'image/gif'} }
                                );
                            });
                        });
                }

                // Regular fetch for all other requests
                return fetch(event.request)
                    .then(response => {
                        // Only cache successful responses
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
                        // For API requests with .json, return an offline indicator
                        if (url.pathname.endsWith('.json')) {
                            return new Response(JSON.stringify({
                                error: 'Network error',
                                offline: true
                            }), {
                                status: 503,
                                headers: new Headers({
                                    'Content-Type': 'application/json'
                                })
                            });
                        }

                        // For other resources, try to return something cached
                        return caches.match(new URL('index.html', baseUrl).href);
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