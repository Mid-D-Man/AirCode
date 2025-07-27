// Enhanced service worker for GitHub Pages Blazor WASM PWA - AirCode
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'aircode-offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
    /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/,
    /\.json$/, /\.css$/, /\.woff$/, /\.woff2$/,
    /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/,
    /\.blat$/, /\.dat$/, /\.svg$/
];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Cache for dynamic pages and routes - FIXED: Use consistent naming
const pagesCacheName = `aircode-pages-cache-${self.assetsManifest.version}`;
const blazorRoutes = ['/', '/Admin/OfflineAttendanceEven', '/Client/OfflineScan'];

self.addEventListener('install', event => {
    console.log('AirCode Service worker: Install');
    event.waitUntil(onInstall(event));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    console.log('AirCode Service worker: Activate');
    event.waitUntil(onActivate(event));
    self.clients.claim();
});

self.addEventListener('fetch', event => {
    event.respondWith(onFetch(event));
});

self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

async function onInstall(event) {
    console.info('AirCode Service worker: Installing and caching assets');

    try {
        // Cache static assets from manifest
        const assetsRequests = self.assetsManifest.assets
            .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
            .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
            .map(asset => new Request(asset.url, {
                integrity: asset.hash,
                cache: 'no-cache'
            }));

        const cache = await caches.open(cacheName);

        // FIXED: Add assets in smaller batches to prevent failures
        const batchSize = 20;
        for (let i = 0; i < assetsRequests.length; i += batchSize) {
            const batch = assetsRequests.slice(i, i + batchSize);
            try {
                await cache.addAll(batch);
                console.log(`AirCode: Cached batch ${Math.floor(i/batchSize) + 1}/${Math.ceil(assetsRequests.length/batchSize)}`);
            } catch (error) {
                console.warn('AirCode: Failed to cache batch, trying individually:', error);
                // Try adding individually if batch fails
                for (const request of batch) {
                    try {
                        const response = await fetch(request);
                        if (response.ok) {
                            await cache.put(request, response);
                        }
                    } catch (individualError) {
                        console.warn('AirCode: Failed to cache individual asset:', request.url);
                    }
                }
            }
        }

        // FIXED: Ensure pages cache is properly set up
        const pagesCache = await caches.open(pagesCacheName);

        // Cache index.html with proper error handling
        try {
            const indexResponse = await fetch('./index.html', { cache: 'no-cache' });
            if (indexResponse.ok) {
                await pagesCache.put('./index.html', indexResponse.clone());
                await pagesCache.put('/', indexResponse.clone()); // FIXED: Also cache root path
                console.log('AirCode: Successfully cached index.html and root path');
            }
        } catch (error) {
            console.error('AirCode: Failed to cache index.html:', error);
        }

        // Cache Blazor routes
        for (const route of blazorRoutes) {
            if (route !== '/') {
                try {
                    const routeResponse = await fetch('./index.html', { cache: 'no-cache' });
                    if (routeResponse.ok) {
                        await pagesCache.put(route, routeResponse.clone());
                        console.log(`AirCode: Cached route: ${route}`);
                    }
                } catch (error) {
                    console.warn(`AirCode: Failed to cache route ${route}:`, error);
                }
            }
        }

        console.info('AirCode Service worker: Assets cached successfully');
    } catch (error) {
        console.error('AirCode Service worker: Installation failed:', error);
    }
}

async function onActivate(event) {
    console.info('AirCode Service worker: Activate - cleaning old caches');

    try {
        const cacheKeys = await caches.keys();
        await Promise.all(cacheKeys
            .filter(key =>
                (key.startsWith(cacheNamePrefix) && key !== cacheName) ||
                (key.startsWith('aircode-pages-cache-') && key !== pagesCacheName)
            )
            .map(key => {
                console.log('AirCode: Deleting old cache:', key);
                return caches.delete(key);
            })
        );

        // Notify clients about new version
        const clients = await self.clients.matchAll();
        clients.forEach(client => {
            client.postMessage({ type: 'NEW_VERSION_AVAILABLE' });
        });

        console.info('AirCode Service worker: Activation complete');
    } catch (error) {
        console.error('AirCode Service worker: Activation failed:', error);
    }
}

async function onFetch(event) {
    const request = event.request;
    const url = new URL(request.url);

    // Skip non-GET requests
    if (request.method !== 'GET') {
        return fetch(request);
    }

    // Skip requests to other origins
    if (url.origin !== location.origin) {
        return fetch(request);
    }

    // Handle navigation requests (page loads)
    if (request.mode === 'navigate') {
        return handleNavigationRequest(request);
    }

    // Handle static asset requests
    return handleAssetRequest(request);
}

async function handleNavigationRequest(request) {
    const url = new URL(request.url);
    const pathname = url.pathname;

    try {
        // Try network first for navigation
        const networkResponse = await fetch(request, {
            cache: 'no-cache'
        });

        // Cache successful responses
        if (networkResponse.ok) {
            const pagesCache = await caches.open(pagesCacheName);
            pagesCache.put(request, networkResponse.clone());
        }

        return networkResponse;
    } catch (error) {
        console.log('AirCode: Network failed for navigation, serving from cache:', pathname);

        // FIXED: Better fallback logic for pages cache
        const pagesCache = await caches.open(pagesCacheName);

        // Try exact match first
        let cachedResponse = await pagesCache.match(request);

        // Try pathname match
        if (!cachedResponse && pathname !== '/') {
            cachedResponse = await pagesCache.match(pathname);
        }

        // Try root path
        if (!cachedResponse) {
            cachedResponse = await pagesCache.match('/');
        }

        // Finally try index.html
        if (!cachedResponse) {
            cachedResponse = await pagesCache.match('./index.html');
        }

        if (cachedResponse) {
            console.log('AirCode: Serving cached page for:', pathname);
            return cachedResponse;
        }

        console.error('AirCode: No cached page available for:', pathname);
        return new Response('Offline - Page not available', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }
}

async function handleAssetRequest(request) {
    // Try cache first for static assets
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        return cachedResponse;
    }

    try {
        // Try network for uncached assets
        const networkResponse = await fetch(request);

        // Cache successful responses if they match our patterns
        if (networkResponse.ok && shouldCacheAsset(request.url)) {
            cache.put(request, networkResponse.clone());
        }

        return networkResponse;
    } catch (error) {
        console.log('AirCode: Asset request failed and not in cache:', request.url);
        return new Response('Asset not available offline', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }
}

function shouldCacheAsset(url) {
    return offlineAssetsInclude.some(pattern => pattern.test(url)) &&
        !offlineAssetsExclude.some(pattern => pattern.test(url));
}