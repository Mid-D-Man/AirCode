// Blazor WASM PWA Service Worker - Production Offline-Capable Version
// Based on Microsoft's recommended patterns and Blazor asset manifest integration

const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v11';
const OFFLINE_CACHE = 'aircode-offline-v11';

// CRITICAL: Import Blazor-generated asset manifest
self.importScripts(BASE_PATH + 'service-worker-assets.js');

// Get all assets from Blazor manifest - this is the key to proper offline support
const assetsManifest = self.assetsManifest;
const offlineAssetsInclude = assetsManifest?.offlineAssetsInclude || [];
const offlineAssetsExclude = assetsManifest?.offlineAssetsExclude || [];

// Core navigation assets - must be cached first
const NAVIGATION_ASSETS = [
    BASE_PATH,
    BASE_PATH + 'index.html'
];

// Filter assets for offline capability
const offlineAssets = [
    ...NAVIGATION_ASSETS,
    ...assetsManifest.assets
        ?.filter(asset => offlineAssetsInclude.length === 0 || offlineAssetsInclude.some(pattern => asset.url.includes(pattern)))
        ?.filter(asset => !offlineAssetsExclude.some(pattern => asset.url.includes(pattern)))
        ?.map(asset => BASE_PATH + asset.url) || []
];

console.log(`SW: v11 - Caching ${offlineAssets.length} offline assets`);

self.addEventListener('install', event => {
    console.log('SW: Installing v11 - Blazor Asset Manifest Integration');

    event.waitUntil(
        (async () => {
            // Clear old caches first
            const cacheNames = await caches.keys();
            await Promise.all(
                cacheNames
                    .filter(name => name.startsWith('aircode-cache-') && name !== CACHE_NAME)
                    .map(name => caches.delete(name))
            );

            // Open fresh cache
            const cache = await caches.open(CACHE_NAME);

            // Phase 1: Cache navigation assets immediately
            console.log('SW: Phase 1 - Navigation assets');
            for (const url of NAVIGATION_ASSETS) {
                try {
                    const response = await fetch(url, {
                        cache: 'reload',
                        credentials: 'same-origin'
                    });
                    if (response.ok) {
                        await cache.put(url, response);
                        console.log(`SW: Cached navigation: ${url}`);
                    }
                } catch (error) {
                    console.warn(`SW: Navigation cache failed: ${url}`, error);
                }
            }

            // Phase 2: Batch cache all Blazor assets with integrity verification
            console.log('SW: Phase 2 - Blazor assets with integrity');
            const batchSize = 10; // Process in smaller batches to avoid overwhelming

            for (let i = 0; i < offlineAssets.length; i += batchSize) {
                const batch = offlineAssets.slice(i, i + batchSize);

                await Promise.allSettled(
                    batch.map(async (assetUrl) => {
                        if (NAVIGATION_ASSETS.includes(assetUrl)) return; // Skip already cached

                        try {
                            // Find asset info for integrity check
                            const assetInfo = assetsManifest.assets?.find(a =>
                                assetUrl.endsWith(a.url)
                            );

                            const response = await fetch(assetUrl, {
                                cache: 'reload',
                                credentials: 'same-origin'
                            });

                            if (response.ok) {
                                // Verify integrity if available
                                if (assetInfo?.hash) {
                                    const responseClone = response.clone();
                                    const buffer = await responseClone.arrayBuffer();
                                    const hashArray = new Uint8Array(
                                        await crypto.subtle.digest('SHA-256', buffer)
                                    );
                                    const hashHex = Array.from(hashArray)
                                        .map(b => b.toString(16).padStart(2, '0'))
                                        .join('');

                                    if (!assetInfo.hash.includes(hashHex)) {
                                        console.warn(`SW: Integrity mismatch for ${assetUrl}`);
                                        return; // Skip caching if integrity fails
                                    }
                                }

                                await cache.put(assetUrl, response);
                                console.log(`SW: Cached asset: ${assetUrl}`);
                            }
                        } catch (error) {
                            console.warn(`SW: Asset cache failed: ${assetUrl}`, error);
                        }
                    })
                );
            }

            // Phase 3: Create offline cache for runtime assets
            const offlineCache = await caches.open(OFFLINE_CACHE);
            const offlineResponse = new Response(
                JSON.stringify({
                    offline: true,
                    timestamp: new Date().toISOString(),
                    cached: true
                }),
                {
                    status: 200,
                    headers: { 'Content-Type': 'application/json' }
                }
            );
            await offlineCache.put('/offline-indicator', offlineResponse);

            console.log('SW: Installation complete - Ready for offline use');
            await self.skipWaiting();
        })()
    );
});

self.addEventListener('activate', event => {
    console.log('SW: Activating v11');

    event.waitUntil(
        (async () => {
            // Clean up old caches
            const cacheNames = await caches.keys();
            await Promise.all(
                cacheNames
                    .filter(name =>
                        (name.startsWith('aircode-cache-') && name !== CACHE_NAME) ||
                        (name.startsWith('aircode-offline-') && name !== OFFLINE_CACHE)
                    )
                    .map(name => caches.delete(name))
            );

            // Take control immediately
            await self.clients.claim();

            // Notify all clients
            const clients = await self.clients.matchAll();
            clients.forEach(client => {
                client.postMessage({
                    type: 'SW_ACTIVATED',
                    version: 11,
                    offlineReady: true,
                    assetsCount: offlineAssets.length
                });
            });

            console.log('SW: Activated - Full offline capability enabled');
        })()
    );
});

self.addEventListener('fetch', event => {
    const { request } = event;

    // Only handle GET requests from same origin
    if (request.method !== 'GET' || !request.url.startsWith(self.location.origin)) {
        return;
    }

    const url = new URL(request.url);

    // Navigation requests - always serve cached index.html for SPA routing
    if (request.mode === 'navigate') {
        event.respondWith(handleNavigation(request));
        return;
    }

    // All other requests - cache-first with network fallback
    event.respondWith(handleResource(request, url));
});

async function handleNavigation(request) {
    try {
        // Always try cache first for navigation
        const cachedResponse = await caches.match(BASE_PATH + 'index.html');
        if (cachedResponse) {
            console.log('SW: Serving cached index.html for navigation');
            return cachedResponse;
        }

        // Network fallback
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            // Cache the response for future use
            const cache = await caches.open(CACHE_NAME);
            cache.put(BASE_PATH + 'index.html', networkResponse.clone());
            return networkResponse;
        }

        throw new Error(`Navigation failed: ${networkResponse.status}`);

    } catch (error) {
        console.error('SW: Navigation completely failed - app unavailable', error);
        return new Response(
            `<!DOCTYPE html>
            <html>
            <head><title>App Unavailable</title></head>
            <body>
                <h1>Application Unavailable</h1>
                <p>The application is not available offline and cannot connect to the server.</p>
                <button onclick="location.reload()">Retry</button>
            </body>
            </html>`,
            {
                status: 503,
                headers: { 'Content-Type': 'text/html' }
            }
        );
    }
}

async function handleResource(request, url) {
    const cacheKey = request.url;

    try {
        // CACHE FIRST - Check all cache instances
        let cachedResponse = await caches.match(cacheKey);

        if (cachedResponse) {
            console.log(`SW: Cache hit: ${url.pathname}`);

            // Background update for non-critical resources
            if (!url.pathname.includes('_framework/')) {
                fetch(request).then(response => {
                    if (response.ok) {
                        caches.open(CACHE_NAME).then(cache => {
                            cache.put(cacheKey, response.clone());
                        });
                    }
                }).catch(() => {}); // Silent background update
            }

            return cachedResponse;
        }

        // NETWORK FALLBACK
        console.log(`SW: Cache miss, trying network: ${url.pathname}`);
        const networkResponse = await fetch(request, {
            credentials: 'same-origin'
        });

        if (networkResponse.ok) {
            // Cache successful network responses
            const cache = await caches.open(CACHE_NAME);
            cache.put(cacheKey, networkResponse.clone());
            console.log(`SW: Network success, cached: ${url.pathname}`);
            return networkResponse;
        }

        throw new Error(`Network failed: ${networkResponse.status}`);

    } catch (networkError) {
        console.warn(`SW: Network unavailable for: ${url.pathname}`, networkError);
        return handleOfflineRequest(request, url);
    }
}

function handleOfflineRequest(request, url) {
    const pathname = url.pathname;

    // Framework assets - critical, must be available
    if (pathname.includes('_framework/')) {
        console.error(`SW: CRITICAL - Framework asset missing offline: ${pathname}`);
        return new Response('Framework asset unavailable offline', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }

    // JavaScript fallback
    if (pathname.endsWith('.js')) {
        return new Response(`
            console.warn('Script unavailable offline: ${pathname}');
            // Offline stub - prevents errors
        `, {
            status: 200,
            headers: { 'Content-Type': 'application/javascript' }
        });
    }

    // CSS fallback
    if (pathname.endsWith('.css')) {
        return new Response(`
            /* Offline - stylesheet unavailable: ${pathname} */
            body::before {
                content: "Some styles unavailable offline";
                display: block;
                background: #fff3cd;
                color: #856404;
                padding: 8px;
                text-align: center;
                font-size: 12px;
            }
        `, {
            status: 200,
            headers: { 'Content-Type': 'text/css' }
        });
    }

    // JSON/API fallback
    if (pathname.endsWith('.json') || pathname.includes('/api/')) {
        return new Response(JSON.stringify({
            error: 'Offline',
            message: 'This resource is not available offline',
            offline: true,
            path: pathname
        }), {
            status: 503,
            headers: { 'Content-Type': 'application/json' }
        });
    }

    // Image fallback
    if (request.destination === 'image') {
        return new Response(`
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
                <rect width="200" height="100" fill="#f8f9fa" stroke="#dee2e6"/>
                <text x="100" y="50" text-anchor="middle" dy=".3em" fill="#6c757d" font-family="system-ui">
                    Offline
                </text>
            </svg>
        `, {
            status: 200,
            headers: { 'Content-Type': 'image/svg+xml' }
        });
    }

    // Generic fallback
    return new Response('Resource not available offline', {
        status: 503,
        statusText: 'Service Unavailable'
    });
}

// Enhanced messaging for PWA management
self.addEventListener('message', event => {
    const { data } = event;

    if (data?.type === 'SKIP_WAITING') {
        event.waitUntil(self.skipWaiting());
        return;
    }

    if (data?.type === 'GET_CACHE_STATUS') {
        event.waitUntil(
            (async () => {
                const cache = await caches.open(CACHE_NAME);
                const keys = await cache.keys();
                const cachedUrls = keys.map(req => req.url);

                const offlineCoverage = offlineAssets.filter(asset =>
                    cachedUrls.some(url => url.includes(asset.replace(BASE_PATH, '')))
                ).length;

                const isOfflineReady = offlineCoverage >= offlineAssets.length * 0.9; // 90% threshold

                event.ports[0]?.postMessage({
                    type: 'CACHE_STATUS',
                    offlineReady: isOfflineReady,
                    totalAssets: offlineAssets.length,
                    cachedAssets: offlineCoverage,
                    coverage: Math.round((offlineCoverage / offlineAssets.length) * 100),
                    version: 11
                });
            })()
        );
        return;
    }

    if (data?.type === 'FORCE_UPDATE') {
        event.waitUntil(
            (async () => {
                await caches.delete(CACHE_NAME);
                await caches.delete(OFFLINE_CACHE);
                await self.skipWaiting();
            })()
        );
        return;
    }
});

console.log('SW: v11 Loaded - Blazor Asset Manifest Integration Active');