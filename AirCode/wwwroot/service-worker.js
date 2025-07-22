// Fixed Service Worker - Proper Path Resolution
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v4'; // Increment version

// Essential assets only - avoid 404s
const CRITICAL_ASSETS = [
    BASE_PATH,
    BASE_PATH + 'index.html',
    BASE_PATH + 'manifest.json',
    BASE_PATH + '_framework/blazor.webassembly.js',
    BASE_PATH + 'css/app.css',
    BASE_PATH + 'js/pwaManager.js'
].filter(Boolean);

self.addEventListener('install', event => {
    console.log('SW: Installing with cache:', CACHE_NAME);
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                return Promise.allSettled(
                    CRITICAL_ASSETS.map(url =>
                        cache.add(url).catch(err =>
                            console.warn(`Cache failed for ${url}:`, err.message)
                        )
                    )
                );
            })
            .then(() => {
                console.log('SW: Install complete, skipping waiting');
                return self.skipWaiting();
            })
    );
});

self.addEventListener('activate', event => {
    console.log('SW: Activating');
    event.waitUntil(
        Promise.all([
            // Clean old caches
            caches.keys().then(names =>
                Promise.all(
                    names.map(name =>
                        name !== CACHE_NAME ? caches.delete(name) : null
                    ).filter(Boolean)
                )
            ),
            // Take control immediately
            self.clients.claim()
        ]).then(() => {
            console.log('SW: Activation complete');
            // Notify all clients about update
            return self.clients.matchAll();
        }).then(clients => {
            clients.forEach(client => {
                client.postMessage({ type: 'UPDATE_AVAILABLE' });
            });
        })
    );
});

self.addEventListener('fetch', event => {
    // Only handle GET requests
    if (event.request.method !== 'GET') return;

    // Skip chrome-extension and non-http requests
    if (!event.request.url.startsWith('http')) return;

    event.respondWith(
        caches.match(event.request)
            .then(response => {
                if (response) {
                    console.log('SW: Cache hit for', event.request.url);
                    return response;
                }

                return fetch(event.request)
                    .then(fetchResponse => {
                        // Don't cache non-successful responses
                        if (!fetchResponse || fetchResponse.status !== 200 || fetchResponse.type !== 'basic') {
                            return fetchResponse;
                        }

                        const responseToCache = fetchResponse.clone();
                        caches.open(CACHE_NAME)
                            .then(cache => cache.put(event.request, responseToCache))
                            .catch(err => console.warn('Cache put failed:', err));

                        return fetchResponse;
                    })
                    .catch(error => {
                        console.warn('Fetch failed for', event.request.url, error);

                        // Fallback for navigation requests
                        if (event.request.mode === 'navigate') {
                            return caches.match(BASE_PATH + 'index.html');
                        }

                        // Return offline response
                        return new Response('Offline', {
                            status: 503,
                            statusText: 'Service Unavailable'
                        });
                    });
            })
    );
});

self.addEventListener('message', event => {
    console.log('SW: Message received', event.data);

    if (event.data?.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

console.log('SW: Script loaded, base path:', BASE_PATH);