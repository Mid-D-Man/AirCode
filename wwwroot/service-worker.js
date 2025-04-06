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

// Add this improved fetch handler to your service-worker.js

self.addEventListener('fetch', (event) => {
    // Skip non-GET requests
    if (event.request.method !== 'GET') return;

    // Parse the URL
    const url = new URL(event.request.url);

    // Handle the fetch with SPA awareness
    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    // Return cached response
                    return cachedResponse;
                }

                // Handle different request types
                if (event.request.mode === 'navigate' ||
                    url.pathname.endsWith('.html') ||
                    // This pattern catches client-side routes
                    (url.pathname.includes('/Client/') ||
                        url.pathname.includes('/Admin/') ||
                        url.pathname.match(/\/[^/.]+$/))) {

                    // For navigation requests or client routes, serve index.html
                    console.log('[ServiceWorker] Handling SPA route:', url.pathname);
                    return caches.match(new URL('index.html', self.registration.scope).href)
                        .then(indexResponse => {
                            if (indexResponse) {
                                return indexResponse;
                            }
                            // Try fetching from network if not cached
                            return fetch(event.request)
                                .catch(() => {
                                    // As last resort, create a simple response
                                    return new Response(
                                        '<html><body><h1>AirCode</h1><p>Unable to load content. Please check your connection.</p></body></html>',
                                        { headers: { 'Content-Type': 'text/html' } }
                                    );
                                });
                        });
                }

                // For favicon requests that might fail
                if (url.pathname.includes('favicon') || url.pathname.endsWith('.ico')) {
                    return fetch(event.request)
                        .catch(() => {
                            // Try alternate favicon location
                            return fetch(new URL('favicon.png', self.registration.scope).href)
                                .catch(() => {
                                    // Return empty image as last resort
                                    return new Response(
                                        'R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7',
                                        { headers: { 'Content-Type': 'image/gif' } }
                                    );
                                });
                        });
                }

                // Standard fetch for all other assets
                return fetch(event.request)
                    .then(response => {
                        // Clone the response to cache it
                        if (response.ok) {
                            const clonedResponse = response.clone();
                            caches.open(CACHE_NAME).then(cache => {
                                cache.put(event.request, clonedResponse);
                            });
                        }
                        return response;
                    })
                    .catch(error => {
                        console.error('[ServiceWorker] Fetch failed:', error);
                        // For API endpoints
                        if (url.pathname.includes('/api/') || url.pathname.endsWith('.json')) {
                            return new Response(
                                JSON.stringify({ error: 'Network error', offline: true }),
                                {
                                    status: 503,
                                    headers: { 'Content-Type': 'application/json' }
                                }
                            );
                        }

                        // For other assets, return a placeholder or cached fallback
                        if (url.pathname.endsWith('.css')) {
                            return new Response('/* Offline fallback stylesheet */',
                                { headers: { 'Content-Type': 'text/css' } });
                        }

                        if (url.pathname.endsWith('.js')) {
                            return new Response('console.log("Offline fallback script");',
                                { headers: { 'Content-Type': 'application/javascript' } });
                        }

                        // For images, try to find cached images or return empty gif
                        if (url.pathname.match(/\.(png|jpg|jpeg|gif|svg)$/)) {
                            return new Response(
                                'R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7',
                                { headers: { 'Content-Type': 'image/gif' } }
                            );
                        }

                        // Default response for anything else
                        return new Response('Offline content unavailable',
                            { headers: { 'Content-Type': 'text/plain' } });
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