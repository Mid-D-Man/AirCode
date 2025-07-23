// Blazor WASM Offline-First Service Worker - TypeScript Fixes Applied
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v10';

// CRITICAL: Boot sequence assets must be cached FIRST
const BOOT_SEQUENCE_ASSETS = [
    BASE_PATH + '_framework/blazor.boot.json',
    BASE_PATH + '_framework/blazor.webassembly.js',
    BASE_PATH + '_framework/dotnet.js',
    BASE_PATH + '_framework/dotnet.wasm'
];

const CRITICAL_ASSETS = [
    BASE_PATH,
    BASE_PATH + 'index.html',
    ...BOOT_SEQUENCE_ASSETS,
    BASE_PATH + '_framework/AirCode.dll',
    BASE_PATH + '_framework/Microsoft.AspNetCore.Components.dll',
    BASE_PATH + '_framework/Microsoft.AspNetCore.Components.WebAssembly.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Configuration.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Configuration.Json.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.DependencyInjection.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Logging.dll',
    BASE_PATH + '_framework/System.Net.Http.dll',
    BASE_PATH + '_framework/System.Text.Json.dll',
    BASE_PATH + 'manifest.json',
    BASE_PATH + 'icon-192.png',
    BASE_PATH + 'icon-512.png',
    BASE_PATH + 'favicon.ico',
    BASE_PATH + 'css/bootstrap/bootstrap.min.css',
    BASE_PATH + 'css/app.css',
    BASE_PATH + 'AirCode.styles.css',
    BASE_PATH + '_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js',
    BASE_PATH + 'js/pwaManager.js'
];

// Framework patterns for dynamic discovery
const FRAMEWORK_PATTERNS = [
    /_framework\/.*\.dll$/,
    /_framework\/.*\.pdb$/,
    /_framework\/.*\.dat$/,
    /_framework\/.*\.wasm$/,
    /_framework\/.*\.js$/,
    /_framework\/.*\.json$/
];

self.addEventListener('install', event => {
    console.log('SW: Installing v10 - Offline-First Fix');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(async cache => {
                // PHASE 1: Cache boot sequence with integrity bypass
                console.log('SW: Phase 1 - Boot sequence caching');
                for (const url of BOOT_SEQUENCE_ASSETS) {
                    try {
                        const response = await fetch(url, {
                            cache: 'reload',
                            credentials: 'same-origin',
                            mode: 'cors'
                        });

                        if (response.ok) {
                            await cache.put(url, response.clone());
                            console.log(`SW: Boot asset cached: ${url}`);
                        }
                    } catch (error) {
                        console.error(`SW: CRITICAL - Boot asset failed: ${url}`, error);
                    }
                }

                // PHASE 2: Dynamic framework discovery from blazor.boot.json
                try {
                    const bootResponse = await cache.match(BASE_PATH + '_framework/blazor.boot.json');
                    if (bootResponse) {
                        const bootData = await bootResponse.json();
                        const dynamicAssets = [];

                        // Extract all DLLs and resources from boot.json
                        if (bootData.resources?.assembly) {
                            Object.keys(bootData.resources.assembly).forEach(dll => {
                                dynamicAssets.push(BASE_PATH + '_framework/' + dll);
                            });
                        }

                        if (bootData.resources?.runtime) {
                            Object.keys(bootData.resources.runtime).forEach(runtime => {
                                dynamicAssets.push(BASE_PATH + '_framework/' + runtime);
                            });
                        }

                        // Cache discovered assets
                        console.log(`SW: Phase 2 - Caching ${dynamicAssets.length} discovered assets`);
                        for (const asset of dynamicAssets) {
                            try {
                                const response = await fetch(asset, { cache: 'reload' });
                                if (response.ok) {
                                    await cache.put(asset, response);
                                }
                            } catch (error) {
                                console.warn(`SW: Dynamic asset cache miss: ${asset}`);
                            }
                        }
                    }
                } catch (error) {
                    console.warn('SW: Dynamic discovery failed, using static list');
                }

                // PHASE 3: Cache remaining critical assets
                console.log('SW: Phase 3 - Critical assets');
                const remainingAssets = CRITICAL_ASSETS.filter(url =>
                    !BOOT_SEQUENCE_ASSETS.includes(url)
                );

                await Promise.allSettled(
                    remainingAssets.map(async url => {
                        try {
                            const response = await fetch(url, { cache: 'reload' });
                            if (response.ok) {
                                await cache.put(url, response);
                            }
                        } catch (error) {
                            console.warn(`SW: Critical asset miss: ${url}`);
                        }
                    })
                );

                console.log('SW: All phases complete - Skip waiting');
                return self.skipWaiting();
            })
            .catch(error => {
                console.error('SW: Install failed:', error);
            })
    );
});

self.addEventListener('activate', event => {
    console.log('SW: Activating v10');
    event.waitUntil(
        Promise.all([
            // FIX 1: Explicit type alignment for Promise.all
            caches.keys().then(names =>
                Promise.all(
                    names
                        .filter(name => name !== CACHE_NAME)
                        .map(name => caches.delete(name))
                )
            ),
            self.clients.claim()
        ]).then(() => {
            console.log('SW: Activated - Offline-first ready');
            return self.clients.matchAll();
        }).then(clients => {
            clients.forEach(client => {
                client.postMessage({
                    type: 'SW_ACTIVATED',
                    version: 10,
                    offlineReady: true
                });
            });
        })
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET' ||
        !event.request.url.startsWith(self.location.origin)) {
        return;
    }

    const url = new URL(event.request.url);
    const isFrameworkAsset = FRAMEWORK_PATTERNS.some(pattern => pattern.test(url.pathname));
    const isCriticalAsset = CRITICAL_ASSETS.some(asset => url.pathname.endsWith(asset.replace(BASE_PATH, '')));

    // OFFLINE-FIRST: Always try cache first for all GET requests
    event.respondWith(handleOfflineFirstRequest(event.request, isFrameworkAsset, isCriticalAsset));
});

async function handleOfflineFirstRequest(request, isFrameworkAsset, isCriticalAsset) {
    const url = new URL(request.url);

    try {
        // STEP 1: Always check cache first (offline-first strategy)
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
            console.log('SW: Cache hit (offline-first):', url.pathname);

            // For non-critical assets, update cache in background
            if (!isCriticalAsset && !isFrameworkAsset) {
                await updateCacheInBackground(request);
            }

            return cachedResponse;
        }

        // STEP 2: Cache miss - try network
        console.log('SW: Cache miss, trying network:', url.pathname);
        const networkResponse = await fetch(request, {
            credentials: 'same-origin',
            cache: 'no-cache'
        });

        if (networkResponse.ok) {
            // Cache successful network responses
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone()).catch(err =>
                console.warn('SW: Cache write failed:', err)
            );
            console.log('SW: Network response cached:', url.pathname);
            return networkResponse;
        } else {
            throw new Error(`HTTP ${networkResponse.status}`);
        }

    } catch (networkError) {
        console.warn('SW: Network failed for:', url.pathname, networkError.message);

        // STEP 3: Network failed - provide offline fallbacks
        return handleOfflineRequest(request, url);
    }
}

// Background cache update for non-critical assets
async function updateCacheInBackground(request) {
    try {
        const response = await fetch(request, { cache: 'no-cache' });
        if (response.ok) {
            const cache = await caches.open(CACHE_NAME);
            await cache.put(request, response);
            console.log('SW: Background cache update:', request.url);
        }
    } catch (error) {
        // Silent fail for background updates
    }
}

function handleOfflineRequest(request, url) {
    // Navigation requests - serve cached index.html
    if (request.mode === 'navigate') {
        return caches.match(BASE_PATH + 'index.html')
            .then(response => {
                if (response) {
                    console.log('SW: Serving cached index for navigation (offline)');
                    return response;
                }
                return createOfflineResponse('App unavailable - no cached version', 503);
            });
    }

    // Framework assets - critical for app functionality
    if (url.pathname.includes('_framework/')) {
        return caches.match(request)
            .then(response => {
                if (response) {
                    console.log('SW: Serving cached framework asset (offline):', url.pathname);
                    return response;
                }
                console.error('SW: CRITICAL - Framework asset missing offline:', url.pathname);
                return createOfflineResponse('Framework component missing', 503);
            });
    }

    // JavaScript files
    if (url.pathname.endsWith('.js')) {
        return caches.match(request).then(response => {
            if (response) return response;
            return new Response('console.warn("Script unavailable offline");', {
                status: 200,
                headers: { 'Content-Type': 'application/javascript' }
            });
        });
    }

    // CSS files
    if (url.pathname.endsWith('.css')) {
        return caches.match(request).then(response => {
            if (response) return response;
            return new Response('/* Offline - styles unavailable */', {
                status: 200,
                headers: { 'Content-Type': 'text/css' }
            });
        });
    }

    // JSON files (including blazor.boot.json)
    if (url.pathname.endsWith('.json')) {
        return caches.match(request).then(response => {
            if (response) return response;
            return createOfflineResponse('Configuration unavailable offline', 503);
        });
    }

    // WASM files
    if (url.pathname.endsWith('.wasm')) {
        return caches.match(request).then(response => {
            if (response) return response;
            return createOfflineResponse('WebAssembly module unavailable', 503);
        });
    }

    // Image fallback
    if (request.destination === 'image') {
        return new Response(`<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
            <circle cx="100" cy="100" r="80" fill="#f0f0f0" stroke="#ccc" stroke-width="2"/>
            <text x="100" y="105" text-anchor="middle" fill="#666" font-family="sans-serif" font-size="14">Offline</text>
        </svg>`, {
            status: 200,
            headers: { 'Content-Type': 'image/svg+xml' }
        });
    }

    return createOfflineResponse('Resource not available offline');
}

function createOfflineResponse(message = 'Offline', status = 503, contentType = 'application/json') {
    const body = contentType === 'application/javascript'
        ? `console.warn('${message}');`
        : JSON.stringify({
            error: 'Offline',
            message: message,
            timestamp: new Date().toISOString(),
            cached: false
        });

    return new Response(body, {
        status: status,
        statusText: status === 200 ? 'OK' : 'Service Unavailable',
        headers: {
            'Content-Type': contentType,
            'Cache-Control': 'no-cache'
        }
    });
}

// Enhanced message handling
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') {
        // FIX 2: Properly handle skipWaiting Promise
        event.waitUntil(self.skipWaiting());
        return;
    }

    if (event.data?.type === 'GET_OFFLINE_STATUS') {
        caches.open(CACHE_NAME)
            .then(cache => cache.keys())
            .then(keys => {
                const cachedUrls = keys.map(req => req.url);
                const bootCached = BOOT_SEQUENCE_ASSETS.every(asset =>
                    cachedUrls.some(url => url.includes(asset.replace(BASE_PATH, '')))
                );
                const criticalCached = CRITICAL_ASSETS.filter(asset =>
                    cachedUrls.some(url => url.includes(asset.replace(BASE_PATH, '')))
                );

                event.ports[0]?.postMessage({
                    type: 'OFFLINE_STATUS',
                    offlineReady: bootCached && criticalCached.length >= CRITICAL_ASSETS.length * 0.8,
                    bootSequenceReady: bootCached,
                    criticalCached: criticalCached.length,
                    criticalTotal: CRITICAL_ASSETS.length,
                    totalCached: keys.length
                });
            });
        return;
    }
});

console.log('SW: v10 Loaded - Offline-First Strategy Active');