// Define a unique cache name for this version of my app.
// Changing the version (e.g., 'aircode-cache-v1') forces an update of the cache.
const CACHE_NAME = 'aircode-cache-v1';

// List of assets to cache during installation.
// Ensure these URLs match our deployed paths. For local development, we may need to adjust them.
const urlsToCache = [
    '/', // Fallback to index.html as the root of the application
    '/index.html',
    '/css/bootstrap/bootstrap.min.css',
    '/css/app.css',
    '/css/colors.css',
    '/css/responsive.css',
    '/icon-512.png',
    '/icon-192.png',
    '/favicon.png',
    '/js/themeSwitcher.js',
    '/js/connectivityServices.js'
];

/*
 * Install Event:
 * ----------------
 * Triggered when the service worker is installed.
 * Here we open the cache and add all required assets from the urlsToCache list.
 * We use event.waitUntil() to ensure the service worker does not complete installation until caching is done.
 */
self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Install event initiated.');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                console.log('[ServiceWorker] Caching app shell assets individually:', urlsToCache);
                return Promise.all(
                    urlsToCache.map(url =>
                        cache.add(url).catch(err => {
                            console.error(`[ServiceWorker] Failed to cache ${url}:`, err);
                            // Optionally resolve the promise so the installation continues.
                            return Promise.resolve();
                        })
                    )
                );
            })
    );
});


/*
 * Activate Event:
 * ----------------
 * Triggered once the service worker is activated.
 * This is the perfect time to remove outdated caches.
 * We iterate through all cache keys and delete those that don't match the current CACHE_NAME.
 */
self.addEventListener('activate', (event) => {
    console.log('[ServiceWorker] Activate event initiated.');
    event.waitUntil(
        caches.keys().then(cacheNames => {
            // Filter out the caches that match the current version.
            const cachesToDelete = cacheNames.filter(cName => cName !== CACHE_NAME);
            return Promise.all(cachesToDelete.map(cName => {
                console.log('[ServiceWorker] Deleting outdated cache:', cName);
                return caches.delete(cName); // Returns Promise<boolean>
            }));
        }).then(() => {
            console.log('[ServiceWorker] All outdated caches are cleared.');
        })
    );
});


/*
 * Fetch Event:
 * ----------------
 * This event intercepts network requests.
 * 
 * Workflow:
 *  1. Check if the requested asset exists in the cache.
 *  2. If found, return the cached version.
 *  3. Otherwise, fetch the asset from the network.
 *  4. If the network fetch returns a valid response (status 200 and basic type),
 *     clone it and store one copy in the cache for future visits.
 *  5. If fetching fails (especially for navigation requests like page loads),
 *     you can optionally fallback to a default cached page (e.g., index.html).
 */
self.addEventListener('fetch', event => {
    console.log('[ServiceWorker] Fetch request for:', event.request.url);
    event.respondWith(
        caches.match(event.request)
            .then(response => {
                // If available, serve the asset from the cache.
                if (response) {
                    console.log('[ServiceWorker] Serving from cache:', event.request.url);
                    return response;
                }
                // Otherwise, fetch it from the network.
                return fetch(event.request)
                    .then(networkResponse => {
                        // Validate the response before caching it.
                        if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
                            console.log('[ServiceWorker] Network response not cacheable for:', event.request.url);
                            return networkResponse;
                        }
                        // Clone the response so it can be stored in the cache and returned.
                        const responseClone = networkResponse.clone();
                        // Cache the new resource in the background.
                        event.waitUntil(
                            caches.open(CACHE_NAME).then(cache => {
                                console.log('[ServiceWorker] Caching new resource:', event.request.url);
                                return cache.put(event.request, responseClone);
                            })
                                .catch(err => {
                                    console.error('[ServiceWorker] Error caching new resource:', err);
                                })
                        );
                        return networkResponse;
                    })
                    .catch(error => {
                        console.error('[ServiceWorker] Fetch request failed for:', event.request.url, error);
                        // Fallback: if the request is for navigation (e.g., page load) and fails, return the cached index.html.
                        if (event.request.mode === 'navigate') {
                            return caches.match('/index.html');
                        }
                        throw error;
                    });
            })
    );
});

