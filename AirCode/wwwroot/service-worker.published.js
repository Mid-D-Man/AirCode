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

// Cache for dynamic pages and routes
const pagesCacheName = `aircode-pages-cache-${self.assetsManifest.version}`;
const blazorRoutes = ['/', '/Admin/OfflineAttendanceEven', '/Client/OfflineScan']; //  @page routes go here for offline stuff

self.addEventListener('install', event => {
    console.log('AirCode Service worker: Install');
    event.waitUntil(onInstall(event));
    self.skipWaiting(); // Force activation of new service worker
});

self.addEventListener('activate', event => {
    console.log('AirCode Service worker: Activate');
    event.waitUntil(onActivate(event));
    self.clients.claim(); // Take control of all clients immediately
});

self.addEventListener('fetch', event => {
    event.respondWith(onFetch(event));
});

// Listen for skip waiting message from client
self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

async function onInstall(event) {
    console.info('AirCode Service worker: Installing and caching assets');

    // Cache static assets from manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, {
            integrity: asset.hash,
            cache: 'no-cache'
        }));

    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);

    // Cache Blazor routes for offline access
    const pagesCache = await caches.open(pagesCacheName);
    const indexRequest = new Request('./index.html', { cache: 'no-cache' });
    await pagesCache.put('./index.html', await fetch(indexRequest));

    // Cache routes as index.html for SPA routing
    for (const route of blazorRoutes) {
        if (route !== '/') {
            await pagesCache.put(route, await fetch('./index.html'));
        }
    }

    console.info('AirCode Service worker: Assets cached successfully');
}

async function onActivate(event) {
    console.info('AirCode Service worker: Activate - cleaning old caches');

    // Delete old caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key =>
            (key.startsWith(cacheNamePrefix) && key !== cacheName) ||
            (key.startsWith('aircode-pages-cache-') && key !== pagesCacheName)
        )
        .map(key => {
            console.log('Deleting old AirCode cache:', key);
            return caches.delete(key);
        })
    );

    // Notify clients about new version
    const clients = await self.clients.matchAll();
    clients.forEach(client => {
        client.postMessage({ type: 'NEW_VERSION_AVAILABLE' });
    });

    console.info('AirCode Service worker: Activation complete');
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
        const networkResponse = await fetch(request);

        // Cache successful responses
        if (networkResponse.ok) {
            const pagesCache = await caches.open(pagesCacheName);
            pagesCache.put(request, networkResponse.clone());
        }

        return networkResponse;
    } catch (error) {
        console.log('Network failed for navigation, serving from cache:', pathname);

        // Fallback to cached page
        const pagesCache = await caches.open(pagesCacheName);
        let cachedResponse = await pagesCache.match(request);

        // If specific route not cached, serve index.html for SPA routing
        if (!cachedResponse) {
            cachedResponse = await pagesCache.match('./index.html');
        }

        return cachedResponse || new Response('Offline - Page not available', {
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
        console.log('Asset request failed and not in cache:', request.url);
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