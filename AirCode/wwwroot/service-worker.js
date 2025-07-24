// Blazor WebAssembly PWA Service Worker - Fully Offline Capable
// Optimized for .NET 7 Blazor WASM with GitHub Pages deployment

const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v11';
const RUNTIME_CACHE = 'aircode-runtime-v11';

// Core Blazor assets that MUST be cached for offline functionality
const ESSENTIAL_BLAZOR_ASSETS = [
    BASE_PATH,
    BASE_PATH + 'index.html',
    BASE_PATH + '_framework/blazor.webassembly.js',
    BASE_PATH + '_framework/dotnet.js',
    BASE_PATH + '_framework/dotnet.wasm',
    BASE_PATH + 'manifest.json'
];

// Static assets that enhance the experience
const STATIC_ASSETS = [
    BASE_PATH + 'icon-192.png',
    BASE_PATH + 'icon-512.png',
    BASE_PATH + 'favicon.ico',
    BASE_PATH + 'css/bootstrap/bootstrap.min.css',
    BASE_PATH + 'css/app.css',
    BASE_PATH + 'AirCode.styles.css',
    BASE_PATH + '_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js',
    BASE_PATH + 'js/pwaManager.js'
];

// Framework file patterns for dynamic caching
const FRAMEWORK_PATTERNS = [
    /_framework\/.*\.dll$/,
    /_framework\/.*\.pdb$/,
    /_framework\/.*\.wasm$/,
    /_framework\/.*\.dat$/,
    /_framework\/.*\.json$/
];

// Special handling URLs
const BOOT_JSON_URL = BASE_PATH + '_framework/blazor.boot.json';

self.addEventListener('install', event => {
    console.log('SW: Installing Blazor PWA Service Worker v11');

    event.waitUntil(
        Promise.all([
            // Cache essential assets immediately
            caches.open(CACHE_NAME).then(cache => {
                console.log('SW: Caching essential Blazor assets');
                return cache.addAll(ESSENTIAL_BLAZOR_ASSETS.concat(STATIC_ASSETS))
                    .catch(error => {
                        console.warn('SW: Some static assets failed to cache:', error);
                        // Continue installation even if some static assets fail
                        return cache.addAll(ESSENTIAL_BLAZOR_ASSETS);
                    });
            }),

            // Initialize runtime cache
            caches.open(RUNTIME_CACHE)
        ]).then(() => {
            console.log('SW: Installation complete - skipping waiting');
            return self.skipWaiting();
        }).catch(error => {
            console.error('SW: Installation failed:', error);
            throw error;
        })
    );
});

self.addEventListener('activate', event => {
    console.log('SW: Activating Blazor PWA Service Worker v11');

    event.waitUntil(
        Promise.all([
            // Clean up old caches
            caches.keys().then(cacheNames => {
                return Promise.all(
                    cacheNames
                        .filter(cacheName =>
                            cacheName !== CACHE_NAME &&
                            cacheName !== RUNTIME_CACHE
                        )
                        .map(cacheName => {
                            console.log('SW: Deleting old cache:', cacheName);
                            return caches.delete(cacheName);
                        })
                );
            }),

            // Take control of all clients
            self.clients.claim()
        ]).then(() => {
            console.log('SW: Activation complete - PWA ready for offline use');

            // Notify clients that SW is ready
            return self.clients.matchAll();
        }).then(clients => {
            clients.forEach(client => {
                client.postMessage({
                    type: 'SW_ACTIVATED',
                    version: 11,
                    offlineReady: true,
                    timestamp: new Date().toISOString()
                });
            });
        })
    );
});

self.addEventListener('fetch', event => {
    // Only handle GET requests from same origin
    if (event.request.method !== 'GET' ||
        !event.request.url.startsWith(self.location.origin)) {
        return;
    }

    const url = new URL(event.request.url);
    const isNavigationRequest = event.request.mode === 'navigate';
    const isBootJson = url.pathname.endsWith('blazor.boot.json');
    const isFrameworkAsset = FRAMEWORK_PATTERNS.some(pattern => pattern.test(url.pathname));

    // Handle different types of requests with appropriate strategies
    if (isBootJson) {
        // blazor.boot.json: Network first with fallback
        event.respondWith(handleBootJsonRequest(event.request));
    } else if (isNavigationRequest) {
        // Navigation: Cache first for SPA routing
        event.respondWith(handleNavigationRequest(event.request));
    } else if (isFrameworkAsset) {
        // Framework assets: Cache first with network fallback
        event.respondWith(handleFrameworkAssetRequest(event.request));
    } else {
        // Other assets: Cache first with network update
        event.respondWith(handleStaticAssetRequest(event.request));
    }
});

// Handle blazor.boot.json with network-first strategy for updates
async function handleBootJsonRequest(request) {
    const url = new URL(request.url);
    console.log('SW: Handling blazor.boot.json request');

    try {
        // Always try network first for boot.json to get latest version
        console.log('SW: Fetching fresh blazor.boot.json from network');
        const networkResponse = await fetch(request, {
            cache: 'no-cache',
            credentials: 'same-origin'
        });

        if (networkResponse.ok) {
            // Parse boot.json and cache framework assets
            const bootData = await networkResponse.clone().json();
            await cacheFrameworkAssetsFromBootJson(bootData);

            // Cache the boot.json
            const cache = await caches.open(RUNTIME_CACHE);
            await cache.put(request, networkResponse.clone());

            console.log('SW: Fresh blazor.boot.json cached with framework assets');
            return networkResponse;
        }
    } catch (networkError) {
        console.warn('SW: Network failed for blazor.boot.json:', networkError.message);
    }

    // Network failed - try cache
    const cachedResponse = await caches.match(request);
    if (cachedResponse) {
        console.log('SW: Serving cached blazor.boot.json (offline)');
        return cachedResponse;
    }

    // No cache available - this is critical failure
    console.error('SW: CRITICAL - No blazor.boot.json available offline');
    return new Response(JSON.stringify({
        error: 'Boot configuration unavailable offline',
        message: 'Application cannot start without blazor.boot.json',
        offline: true
    }), {
        status: 503,
        headers: { 'Content-Type': 'application/json' }
    });
}

// Cache framework assets discovered from blazor.boot.json
async function cacheFrameworkAssetsFromBootJson(bootData) {
    if (!bootData || !bootData.resources) {
        console.warn('SW: Invalid boot.json structure');
        return;
    }

    const cache = await caches.open(CACHE_NAME);
    const assetsToCache = [];

    // Extract all framework assets
    ['assembly', 'runtime', 'pdb', 'satelliteResources'].forEach(resourceType => {
        if (bootData.resources[resourceType]) {
            Object.keys(bootData.resources[resourceType]).forEach(filename => {
                assetsToCache.push(BASE_PATH + '_framework/' + filename);
            });
        }
    });

    console.log(`SW: Discovered ${assetsToCache.length} framework assets from boot.json`);

    // Cache assets in batches to avoid overwhelming the network
    const batchSize = 10;
    for (let i = 0; i < assetsToCache.length; i += batchSize) {
        const batch = assetsToCache.slice(i, i + batchSize);

        await Promise.allSettled(
            batch.map(async assetUrl => {
                try {
                    // Check if already cached
                    const cached = await cache.match(assetUrl);
                    if (cached) return;

                    const response = await fetch(assetUrl, {
                        credentials: 'same-origin'
                    });

                    if (response.ok) {
                        await cache.put(assetUrl, response);
                        console.log(`SW: Cached framework asset: ${assetUrl}`);
                    }
                } catch (error) {
                    console.warn(`SW: Failed to cache framework asset: ${assetUrl}`, error);
                }
            })
        );
    }
}

// Handle navigation requests (SPA routing)
async function handleNavigationRequest(request) {
    console.log('SW: Handling navigation request');

    // For SPA, always serve the cached index.html
    const cachedIndex = await caches.match(BASE_PATH + 'index.html');
    if (cachedIndex) {
        console.log('SW: Serving cached index.html for navigation');
        return cachedIndex;
    }

    // No cached index - try network
    try {
        const networkResponse = await fetch(BASE_PATH + 'index.html');
        if (networkResponse.ok) {
            // Cache for future use
            const cache = await caches.open(CACHE_NAME);
            await cache.put(BASE_PATH + 'index.html', networkResponse.clone());
            return networkResponse;
        }
    } catch (error) {
        console.warn('SW: Network failed for index.html:', error);
    }

    // Complete failure
    return new Response(`
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <title>AirCode - Offline</title>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
        </head>
        <body>
            <div style="text-align: center; padding: 50px; font-family: Arial, sans-serif;">
                <h1>AirCode</h1>
                <p>Application is currently unavailable offline...</p>
                <p>Please check your internet connection and try again.</p>
                <button onclick="location.reload()">Retry</button>
            </div>
        </body>
        </html>
    `, {
        status: 503,
        headers: { 'Content-Type': 'text/html' }
    });
}

// Handle framework assets (_framework/*)
async function handleFrameworkAssetRequest(request) {
    const url = new URL(request.url);

    // Check cache first for framework assets
    const cachedResponse = await caches.match(request);
    if (cachedResponse) {
        console.log('SW: Cache hit for framework asset:', url.pathname);
        return cachedResponse;
    }

    // Not in cache - try network
    try {
        console.log('SW: Fetching framework asset from network:', url.pathname);
        const networkResponse = await fetch(request, {
            credentials: 'same-origin'
        });

        if (networkResponse.ok) {
            // Cache successful response
            const cache = await caches.open(CACHE_NAME);
            await cache.put(request, networkResponse.clone());
            console.log('SW: Framework asset cached:', url.pathname);
            return networkResponse;
        }
    } catch (error) {
        console.warn('SW: Network failed for framework asset:', url.pathname, error);
    }

    // Framework asset not available - this might be critical
    console.error('SW: Framework asset unavailable:', url.pathname);

    // Return appropriate error based on file type
    if (url.pathname.endsWith('.dll')) {
        return new Response('Assembly not available offline', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    } else if (url.pathname.endsWith('.wasm')) {
        return new Response('WebAssembly module not available offline', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }

    return new Response('Framework resource not available offline', {
        status: 503,
        statusText: 'Service Unavailable'
    });
}

// Handle static assets (CSS, JS, images, etc.)
async function handleStaticAssetRequest(request) {
    const url = new URL(request.url);

    // Check cache first
    const cachedResponse = await caches.match(request);
    if (cachedResponse) {
        console.log('SW: Cache hit for static asset:', url.pathname);

        // Update cache in background for non-critical assets
        updateCacheInBackground(request);

        return cachedResponse;
    }

    // Not in cache - try network
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            // Cache successful response
            const cache = await caches.open(CACHE_NAME);
            await cache.put(request, networkResponse.clone());
            return networkResponse;
        }
    } catch (error) {
        console.warn('SW: Network failed for static asset:', url.pathname);
    }

    // Provide fallbacks for different asset types
    return createAssetFallback(url);
}

// Background cache update for static assets
async function updateCacheInBackground(request) {
    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(CACHE_NAME);
            await cache.put(request, response);
        }
    } catch (error) {
        // Silent failure for background updates
    }
}

// Create fallback responses for different asset types
function createAssetFallback(url) {
    // CSS fallback
    if (url.pathname.endsWith('.css')) {
        return new Response(`
            /* Asset unavailable offline: ${url.pathname} */
            body::before {
                content: "Some styles may be missing due to offline mode";
                display: block;
                background: #fffbe6;
                color: #8b6914;
                padding: 4px 8px;
                font-size: 12px;
                text-align: center;
                border-bottom: 1px solid #d9d1a8;
            }
        `, {
            status: 200,
            headers: { 'Content-Type': 'text/css' }
        });
    }

    // JavaScript fallback
    if (url.pathname.endsWith('.js')) {
        return new Response(`
            console.warn('Script unavailable offline: ${url.pathname}');
            // Offline fallback - script functionality may be limited
        `, {
            status: 200,
            headers: { 'Content-Type': 'application/javascript' }
        });
    }

    // Image fallback
    if (/\.(png|jpg|jpeg|gif|svg|ico)$/i.test(url.pathname)) {
        return new Response(`
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
                <rect width="200" height="200" fill="#f8f9fa" stroke="#dee2e6" stroke-width="2"/>
                <text x="100" y="100" text-anchor="middle" fill="#6c757d" font-family="Arial, sans-serif" font-size="14">
                    Image unavailable
                </text>
                <text x="100" y="120" text-anchor="middle" fill="#6c757d" font-family="Arial, sans-serif" font-size="12">
                    offline
                </text>
            </svg>
        `, {
            status: 200,
            headers: { 'Content-Type': 'image/svg+xml' }
        });
    }

    // Default fallback
    return new Response(JSON.stringify({
        error: 'Resource unavailable offline',
        url: url.pathname,
        timestamp: new Date().toISOString()
    }), {
        status: 503,
        headers: { 'Content-Type': 'application/json' }
    });
}

// Message handling for PWA functionality
self.addEventListener('message', event => {
    const { data } = event;

    if (data?.type === 'SKIP_WAITING') {
        event.waitUntil(self.skipWaiting());
        return;
    }

    if (data?.type === 'GET_CACHE_STATUS') {
        event.waitUntil(
            Promise.all([
                caches.open(CACHE_NAME).then(cache => cache.keys()),
                caches.open(RUNTIME_CACHE).then(cache => cache.keys())
            ]).then(([staticKeys, runtimeKeys]) => {
                const staticUrls = staticKeys.map(req => req.url);
                const runtimeUrls = runtimeKeys.map(req => req.url);

                // Check essential assets
                const essentialCached = ESSENTIAL_BLAZOR_ASSETS.every(asset =>
                    staticUrls.some(url => url.endsWith(asset.replace(BASE_PATH, '')))
                );

                // Check for boot.json
                const bootJsonCached = runtimeUrls.some(url => url.includes('blazor.boot.json'));

                const status = {
                    type: 'CACHE_STATUS',
                    offlineReady: essentialCached && bootJsonCached,
                    essentialAssetsCached: essentialCached,
                    bootJsonCached: bootJsonCached,
                    totalStaticAssets: staticKeys.length,
                    totalRuntimeAssets: runtimeKeys.length,
                    timestamp: new Date().toISOString()
                };

                event.ports[0]?.postMessage(status);
            }).catch(error => {
                event.ports[0]?.postMessage({
                    type: 'CACHE_STATUS',
                    error: error.message,
                    offlineReady: false
                });
            })
        );
        return;
    }

    if (data?.type === 'FORCE_UPDATE_CACHE') {
        event.waitUntil(
            caches.delete(CACHE_NAME)
                .then(() => caches.delete(RUNTIME_CACHE))
                .then(() => {
                    // Reinstall with fresh cache
                    return self.skipWaiting();
                })
        );
        return;
    }
});

// Error handling for uncaught service worker errors
self.addEventListener('error', event => {
    console.error('SW: Uncaught error:', event.error);
});

self.addEventListener('unhandledrejection', event => {
    console.error('SW: Unhandled promise rejection:', event.reason);
});

console.log('SW: Blazor PWA Service Worker v11 loaded - Fully offline capable!');