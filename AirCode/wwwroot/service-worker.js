// Fixed Service Worker - Blazor Framework Loading Issues Resolved
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v9';

// Critical Assets - Framework order matters
const CRITICAL_ASSETS = [
    // Core routes first
    BASE_PATH,
    BASE_PATH + 'index.html',

    // Blazor framework core - CRITICAL ORDER
    BASE_PATH + '_framework/blazor.webassembly.js',
    BASE_PATH + '_framework/blazor.boot.json',
    BASE_PATH + '_framework/dotnet.js',
    BASE_PATH + '_framework/dotnet.wasm',

    // App assemblies
    BASE_PATH + '_framework/AirCode.dll',
    BASE_PATH + '_framework/Microsoft.AspNetCore.Components.dll',
    BASE_PATH + '_framework/Microsoft.AspNetCore.Components.WebAssembly.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Configuration.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Configuration.Json.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.DependencyInjection.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Logging.dll',
    BASE_PATH + '_framework/System.Net.Http.dll',
    BASE_PATH + '_framework/System.Text.Json.dll',

    // PWA essentials
    BASE_PATH + 'manifest.json',
    BASE_PATH + 'icon-192.png',
    BASE_PATH + 'icon-512.png',
    BASE_PATH + 'favicon.ico',

    // Critical CSS
    BASE_PATH + 'css/bootstrap/bootstrap.min.css',
    BASE_PATH + 'css/app.css',
    BASE_PATH + 'AirCode.styles.css',

    // Auth service
    BASE_PATH + '_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js',

    // PWA Manager
    BASE_PATH + 'js/pwaManager.js'
];

const SECONDARY_ASSETS = [
    BASE_PATH + 'css/colors.css',
    BASE_PATH + 'css/responsive.css',
    BASE_PATH + 'js/connectivityServices.js',
    BASE_PATH + 'js/debug.js',
    BASE_PATH + 'js/cryptographyHandler.js',
    BASE_PATH + 'js/qrCodeModule.js',
    BASE_PATH + 'js/cameraUtil.js',
    BASE_PATH + 'js/validateKeyAndIV.js'
];

// Framework patterns for dynamic caching
const FRAMEWORK_PATTERNS = [
    /_framework\/.*\.dll$/,
    /_framework\/.*\.pdb$/,
    /_framework\/.*\.dat$/,
    /_framework\/.*\.wasm$/,
    /_framework\/.*\.js$/
];

self.addEventListener('install', event => {
    console.log('SW: Installing v9 - Framework Fix');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(async cache => {
                // Cache critical assets with aggressive retry
                const criticalPromises = CRITICAL_ASSETS.map(async url => {
                    let attempts = 0;
                    const maxAttempts = 3;

                    while (attempts < maxAttempts) {
                        try {
                            const response = await fetch(url, {
                                cache: 'reload',
                                credentials: 'same-origin'
                            });

                            if (response.ok) {
                                await cache.put(url, response.clone());
                                console.log(`SW: Cached critical (attempt ${attempts + 1}):`, url);
                                return;
                            } else if (response.status === 404) {
                                console.warn(`SW: Critical asset not found:`, url);
                                return; // Don't retry 404s
                            } else {
                                throw new Error(`HTTP ${response.status}`);
                            }
                        } catch (error) {
                            attempts++;
                            console.warn(`SW: Cache attempt ${attempts} failed for ${url}:`, error.message);

                            if (attempts >= maxAttempts) {
                                console.error(`SW: Failed to cache critical asset after ${maxAttempts} attempts:`, url);
                            } else {
                                // Wait before retry
                                await new Promise(resolve => setTimeout(resolve, 1000 * attempts));
                            }
                        }
                    }
                });

                await Promise.allSettled(criticalPromises);

                // Cache secondary assets (non-blocking)
                for (const url of SECONDARY_ASSETS) {
                    try {
                        const response = await fetch(url);
                        if (response.ok) {
                            await cache.put(url, response);
                        }
                    } catch (error) {
                        console.warn('SW: Secondary cache miss:', url);
                    }
                }

                console.log('SW: Critical assets cached, skipping waiting');
                return self.skipWaiting();
            })
    );
});

self.addEventListener('activate', event => {
    console.log('SW: Activating v9');
    event.waitUntil(
        Promise.all([
            // Clear old caches
            caches.keys().then(names =>
                Promise.all(names.map(name =>
                    name !== CACHE_NAME ? caches.delete(name) : null
                ))
            ),
            // Claim clients immediately
            self.clients.claim()
        ])
            .then(() => {
                console.log('SW: Activated and claimed all clients');
                // Notify all clients
                return self.clients.matchAll();
            })
            .then(clients => {
                clients.forEach(client => {
                    client.postMessage({ type: 'SW_ACTIVATED', version: 9 });
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
    const isFrameworkAsset = FRAMEWORK_PATTERNS.some(pattern => pattern.test(url.pathname));
    const isCriticalAsset = CRITICAL_ASSETS.some(asset => url.pathname.endsWith(asset.replace(BASE_PATH, '')));

    event.respondWith(handleRequest(event.request, isFrameworkAsset, isCriticalAsset));
});

async function handleRequest(request, isFrameworkAsset, isCriticalAsset) {
    const url = new URL(request.url);

    // Critical assets: Cache-first with network fallback
    if (isCriticalAsset || isFrameworkAsset) {
        try {
            const cachedResponse = await caches.match(request);
            if (cachedResponse) {
                console.log('SW: Serving from cache (critical):', url.pathname);
                return cachedResponse;
            }
        } catch (error) {
            console.warn('SW: Cache lookup failed:', error);
        }

        // Network fallback for critical assets
        try {
            const networkResponse = await fetch(request, {
                credentials: 'same-origin',
                cache: 'no-cache'
            });

            if (networkResponse.ok) {
                // Cache successful responses
                const cache = await caches.open(CACHE_NAME);
                cache.put(request, networkResponse.clone()).catch(err =>
                    console.warn('SW: Failed to cache network response:', err)
                );
                console.log('SW: Serving from network (critical):', url.pathname);
                return networkResponse;
            } else {
                throw new Error(`HTTP ${networkResponse.status}`);
            }
        } catch (error) {
            console.error('SW: Critical asset network failed:', url.pathname, error.message);
            return handleOfflineRequest(request);
        }
    }

    // Non-critical assets: Network-first with cache fallback
    try {
        const networkResponse = await fetch(request, {
            credentials: 'same-origin'
        });

        if (networkResponse.ok) {
            // Cache successful responses for future use
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone()).catch(err =>
                console.warn('SW: Cache write failed:', err)
            );
            return networkResponse;
        } else {
            throw new Error(`HTTP ${networkResponse.status}`);
        }
    } catch (error) {
        console.warn('SW: Network failed, trying cache:', url.pathname);

        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
            return cachedResponse;
        }

        return handleOfflineRequest(request);
    }
}

function handleOfflineRequest(request) {
    const url = new URL(request.url);

    // Navigation requests - serve cached index.html
    if (request.mode === 'navigate') {
        return caches.match(BASE_PATH + 'index.html')
            .then(response => {
                if (response) {
                    console.log('SW: Serving cached index for navigation');
                    return response;
                }
                return createOfflineResponse('App not available offline', 503);
            });
    }

    // Blazor framework files - critical for app startup
    if (url.pathname.includes('_framework/')) {
        return caches.match(request)
            .then(response => {
                if (response) {
                    console.log('SW: Serving cached framework asset:', url.pathname);
                    return response;
                }
                console.error('SW: Critical framework asset missing offline:', url.pathname);
                return createOfflineResponse('Framework component unavailable', 503);
            });
    }

    // JavaScript files
    if (url.pathname.endsWith('.js')) {
        return caches.match(request)
            .then(response => {
                if (response) return response;
                return createOfflineResponse('// Script unavailable offline', 200, 'application/javascript');
            });
    }

    // CSS files - graceful degradation
    if (url.pathname.endsWith('.css')) {
        return caches.match(request)
            .then(response => {
                if (response) return response;
                return new Response('/* Offline - styles unavailable */', {
                    status: 200,
                    headers: { 'Content-Type': 'text/css' }
                });
            });
    }

    // JSON files (like blazor.boot.json)
    if (url.pathname.endsWith('.json')) {
        return caches.match(request)
            .then(response => {
                if (response) return response;
                return createOfflineResponse('Configuration unavailable offline', 503);
            });
    }

    // WASM files
    if (url.pathname.endsWith('.wasm')) {
        return caches.match(request)
            .then(response => {
                if (response) return response;
                return createOfflineResponse('WebAssembly module unavailable', 503);
            });
    }

    // Image requests - placeholder
    if (request.destination === 'image') {
        return new Response(`<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
            <rect width="200" height="200" fill="#f0f0f0"/>
            <text x="100" y="100" text-anchor="middle" fill="#666" font-family="sans-serif">Offline</text>
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

// Message handling for PWA updates and debugging
self.addEventListener('message', event => {
    console.log('SW: Received message:', event.data);

    if (event.data?.type === 'SKIP_WAITING') {
        console.log('SW: Skip waiting requested');
        self.skipWaiting();
        return;
    }

    if (event.data?.type === 'GET_CACHE_STATUS') {
        caches.open(CACHE_NAME)
            .then(cache => cache.keys())
            .then(keys => {
                const cachedUrls = keys.map(req => req.url);
                const criticalCached = CRITICAL_ASSETS.filter(asset =>
                    cachedUrls.some(url => url.includes(asset.replace(BASE_PATH, '')))
                );

                event.ports[0]?.postMessage({
                    type: 'CACHE_STATUS',
                    totalCached: keys.length,
                    criticalCached: criticalCached.length,
                    criticalTotal: CRITICAL_ASSETS.length,
                    cacheName: CACHE_NAME,
                    criticalAssets: criticalCached
                });
            })
            .catch(error => {
                event.ports[0]?.postMessage({
                    type: 'CACHE_ERROR',
                    error: error.message
                });
            });
        return;
    }

    if (event.data?.type === 'FORCE_UPDATE') {
        caches.delete(CACHE_NAME).then(() => {
            console.log('SW: Cache cleared for force update');
            self.clients.matchAll().then(clients => {
                clients.forEach(client => {
                    client.postMessage({ type: 'CACHE_CLEARED' });
                });
            });
        });
        return;
    }
});

// Global error handling
self.addEventListener('error', event => {
    console.error('SW: Global error:', event.error);
});

self.addEventListener('unhandledrejection', event => {
    console.error('SW: Unhandled promise rejection:', event.reason);
});

console.log('SW: Script loaded - Version 9');