// Service worker for AirCode PWA with improved GitHub Pages support
// Version: v10

// Cache name - increment this when updating 
const CACHE_NAME = 'aircode-cache-v10';

// Get the base URL from service worker's location
const baseUrl = self.registration.scope;

self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install started with scope:', baseUrl);
    self.skipWaiting();

    // Assets to cache - using scope-relative paths
    const urlsToCache = [
        '', // Root URL - important!
        'index.html',
        'favicon.png',
        'icon-192.png',
        'icon-512.png',
        'manifest.json',
        'css/app.css',
        'css/bootstrap/bootstrap.min.css',
        'css/colors.css',
        'css/responsive.css',
        '_framework/blazor.webassembly.js',
        'js/debug.js',
        'js/connectivityServices.js',
        'js/themeSwitcher.js'
    ];

    // Generate full URLs based on scope
    const fullUrlsToCache = urlsToCache.map(path => new URL(path, baseUrl).href);

    console.log('[ServiceWorker] Caching URLs:', fullUrlsToCache);

    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                // Use a more resilient caching approach with individual fetches
                const cachePromises = fullUrlsToCache.map(url =>
                    fetch(url)
                        .then(response => {
                            if (!response.ok) {
                                throw new Error(`Failed to cache: ${url}`);
                            }
                            return cache.put(url, response);
                        })
                        .catch(error => {
                            console.warn(`[ServiceWorker] Failed to cache ${url}:`, error);
                            // Continue even if one file fails
                            return Promise.resolve();
                        })
                );

                return Promise.all(cachePromises);
            })
            .then(() => console.log('[ServiceWorker] Initial caching complete'))
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
                    return Promise.resolve();
                })
            );
        }).then(() => {
            console.log('[ServiceWorker] Claiming clients');
            return self.clients.claim();
        })
    );
});

// Improved fetch handler for SPA and GitHub Pages
self.addEventListener('fetch', (event) => {
    // Skip non-GET requests
    if (event.request.method !== 'GET') return;

    const url = new URL(event.request.url);

    // Special handling for navigation requests (HTML pages)
    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request)
                .catch(() => {
                    // If fetch fails, serve the cached index.html
                    console.log('[ServiceWorker] Serving cached index.html for navigation');
                    return caches.match(new URL('index.html', baseUrl).href);
                })
        );
        return;
    }

    // Handle other requests
    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    // Return from cache if available
                    return cachedResponse;
                }

                // Special handling for framework files - critical for Blazor
                if (url.pathname.includes('/_framework/')) {
                    return fetch(event.request);
                }

                // For favicon and other icon requests, try different paths if needed
                if (url.pathname.includes('favicon') || url.pathname.includes('icon')) {
                    return fetch(event.request)
                        .catch(() => {
                            // Try alternate paths
                            const iconPaths = [
                                new URL('favicon.png', baseUrl).href,
                                new URL('icon-192.png', baseUrl).href
                            ];

                            // Try each path until one works
                            return Promise.any(
                                iconPaths.map(path => fetch(path))
                            ).catch(() => {
                                console.log('[ServiceWorker] All icon fallbacks failed');
                                return new Response('');
                            });
                        });
                }

                // Regular network request with cache update
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

                        // For API requests, return a network error indicator
                        if (url.pathname.endsWith('.json')) {
                            return new Response(JSON.stringify({
                                error: 'Network error',
                                offline: true
                            }), {
                                status: 503,
                                headers: {'Content-Type': 'application/json'}
                            });
                        }

                        // Return the index.html for navigation-like requests
                        if (url.pathname.endsWith('/') || !url.pathname.includes('.')) {
                            return caches.match(new URL('index.html', baseUrl).href);
                        }

                        // Otherwise return an empty response
                        return new Response('');
                    });
            })
    );
});

// Handle messages from the main thread
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }

    if (event.data && event.data.type === 'LOG') {
        console.log('[ServiceWorker] From page:', event.data.message);
    }
});

console.log('[ServiceWorker] Loaded with scope:', self.registration.scope);