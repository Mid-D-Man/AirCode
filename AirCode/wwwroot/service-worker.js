// Enhanced Service Worker - Aggressive Offline Caching
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v8';

//#region Critical Assets Configuration
const CRITICAL_ASSETS = [
    // Core app routes
    BASE_PATH,
    BASE_PATH + 'index.html',
    BASE_PATH + 'Client/OfflineScan',
    BASE_PATH + 'Admin/OfflineAttendanceEvent',

    // PWA manifest & icons
    BASE_PATH + 'manifest.json',
    BASE_PATH + 'icon-192.png',
    BASE_PATH + 'icon-512.png',
    BASE_PATH + 'favicon.ico',

    // Core CSS
    BASE_PATH + 'css/bootstrap/bootstrap.min.css',
    BASE_PATH + 'css/app.css',
    BASE_PATH + 'css/colors.css',
    BASE_PATH + 'css/responsive.css',
    BASE_PATH + 'AirCode.styles.css',

    // Blazor framework essentials - CRITICAL FIX
    BASE_PATH + '_framework/blazor.webassembly.js',
    BASE_PATH + '_framework/blazor.boot.json',
    BASE_PATH + '_framework/dotnet.7.0.17.5xcw3lqzx7.js',
    BASE_PATH + '_framework/dotnet.wasm',
    BASE_PATH + '_framework/AirCode.dll',
    BASE_PATH + '_framework/Microsoft.AspNetCore.Components.dll',
    BASE_PATH + '_framework/Microsoft.AspNetCore.Components.WebAssembly.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Configuration.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Configuration.Json.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.DependencyInjection.dll',
    BASE_PATH + '_framework/Microsoft.Extensions.Logging.dll',
    BASE_PATH + '_framework/System.Net.Http.dll',
    BASE_PATH + '_framework/System.Text.Json.dll',

    // Critical offline services
    BASE_PATH + 'js/pwaManager.js',
    BASE_PATH + 'js/connectivityServices.js',
    BASE_PATH + 'js/offlineCredentialsHandler.js',
    BASE_PATH + 'js/cryptographyHandler.js',
    BASE_PATH + 'js/qrCodeModule.js',
    BASE_PATH + 'js/debug.js',
    BASE_PATH + 'js/cameraUtil.js',
    BASE_PATH + 'js/validateKeyAndIV.js',

    // QR Scanner dependencies
    BASE_PATH + '_content/ReactorBlazorQRCodeScanner/ReactorBlazorQRCodeScanner.lib.module.js',
    BASE_PATH + '_content/ReactorBlazorQRCodeScanner/jsQR.js',
    BASE_PATH + '_content/ReactorBlazorQRCodeScanner/qrCodeScannerJsInterop.js',

    // WASM QR Generator
    BASE_PATH + 'wasm/qr_code_generator.js',
    BASE_PATH + 'wasm/qr_code_generator_bg.wasm',

    // Auth service dependencies
    BASE_PATH + '_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js'
];

const SECONDARY_ASSETS = [
    // Additional JS utilities
    BASE_PATH + 'js/themeSwitcher.js',
    BASE_PATH + 'js/pageNavigator.js',
    BASE_PATH + 'js/floatingQrDrag.js',
    BASE_PATH + 'js/firestoreModule.js',
    BASE_PATH + 'js/gpuPerformance.js',

    // Font icons
    BASE_PATH + 'css/open-iconic/font/css/open-iconic-bootstrap.min.css',

    // Configuration
    BASE_PATH + 'appsettings.json'
];

// Framework assemblies pattern - dynamic caching
const FRAMEWORK_PATTERNS = [
    /_framework\/.*\.dll$/,
    /_framework\/.*\.pdb$/,
    /_framework\/.*\.dat$/,
    /_framework\/.*\.wasm$/
];
//#endregion

//#region Service Worker Lifecycle
self.addEventListener('install', event => {
    console.log('SW: Installing v7 - Offline Fix');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(async cache => {
                // Pre-cache critical assets with retry logic
                const criticalResults = await Promise.allSettled(
                    CRITICAL_ASSETS.map(async url => {
                        try {
                            const response = await fetch(url, { cache: 'no-cache' });
                            if (response.ok) {
                                await cache.put(url, response);
                                console.log('SW: Cached critical:', url);
                            } else {
                                console.warn('SW: Failed to fetch critical:', url, response.status);
                            }
                        } catch (err) {
                            console.warn('SW: Critical cache error:', url, err);
                        }
                    })
                );

                // Cache secondary assets (non-blocking)
                Promise.allSettled(
                    SECONDARY_ASSETS.map(async url => {
                        try {
                            const response = await fetch(url);
                            if (response.ok) {
                                await cache.put(url, response);
                            }
                        } catch (err) {
                            console.warn('SW: Secondary cache miss:', url);
                        }
                    })
                );

                const criticalFailures = criticalResults.filter(r => r.status === 'rejected').length;
                console.log(`SW: Cached ${CRITICAL_ASSETS.length - criticalFailures}/${CRITICAL_ASSETS.length} critical assets`);

                return self.skipWaiting();
            })
            .catch(err => {
                console.error('SW: Install failed:', err);
                throw err;
            })
    );
});

self.addEventListener('activate', event => {
    console.log('SW: Activating v7');
    event.waitUntil(
        caches.keys()
            .then(names => Promise.all(
                names.map(name => name !== CACHE_NAME ? caches.delete(name) : null)
            ))
            .then(() => self.clients.claim())
            .then(() => {
                console.log('SW: Activated and claimed clients');
                // Notify clients of successful activation
                self.clients.matchAll().then(clients => {
                    clients.forEach(client => client.postMessage({ type: 'SW_ACTIVATED' }));
                });
            })
    );
});
//#endregion

//#region Fetch Strategy - Enhanced Offline Handling
self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    // Skip external requests
    if (!event.request.url.startsWith(self.location.origin)) return;

    const url = new URL(event.request.url);
    const isFrameworkAsset = FRAMEWORK_PATTERNS.some(pattern => pattern.test(url.pathname));

    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    console.log('SW: Cache hit:', event.request.url);
                    return cachedResponse;
                }

                // Network-first for framework assets, cache-first for others
                return fetch(event.request, {
                    cache: isFrameworkAsset ? 'no-cache' : 'default',
                    credentials: 'same-origin'
                })
                    .then(fetchResponse => {
                        if (!fetchResponse.ok) {
                            throw new Error(`HTTP ${fetchResponse.status}`);
                        }

                        // Cache successful responses
                        const responseClone = fetchResponse.clone();
                        caches.open(CACHE_NAME)
                            .then(cache => {
                                cache.put(event.request, responseClone);
                                console.log('SW: Cached from network:', event.request.url);
                            })
                            .catch(err => console.warn('SW: Cache write failed:', err));

                        return fetchResponse;
                    })
                    .catch(error => {
                        console.warn('SW: Network failed:', event.request.url, error.message);
                        return handleOfflineRequest(event.request);
                    });
            })
    );
});

function handleOfflineRequest(request) {
    const url = new URL(request.url);

    // Navigation requests - serve cached index.html
    if (request.mode === 'navigate') {
        return caches.match(BASE_PATH + 'index.html')
            .then(response => response || createOfflineResponse('Page not available offline'));
    }

    // Blazor framework files - critical for app startup
    if (url.pathname.includes('_framework/')) {
        return caches.match(request)
            .then(response => {
                if (response) return response;
                console.error('SW: Critical framework asset missing:', url.pathname);
                return createOfflineResponse('Framework asset unavailable', 503);
            });
    }

    // JavaScript files - check cache first
    if (url.pathname.endsWith('.js')) {
        return caches.match(request)
            .then(response => response || createOfflineResponse('Script unavailable offline'));
    }

    // CSS files - graceful degradation
    if (url.pathname.endsWith('.css')) {
        return caches.match(request)
            .then(response => response || new Response('/* Offline fallback */', {
                headers: { 'Content-Type': 'text/css' }
            }));
    }

    // Image requests - placeholder
    if (request.destination === 'image') {
        return new Response(`<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
            <rect width="200" height="200" fill="#f0f0f0"/>
            <text x="100" y="100" text-anchor="middle" fill="#666">Offline</text>
        </svg>`, {
            headers: { 'Content-Type': 'image/svg+xml' }
        });
    }

    return createOfflineResponse('Resource not available offline');
}
//#endregion

//#region Utility Functions
function createOfflineResponse(message = 'Offline', status = 200) {
    return new Response(
        JSON.stringify({
            error: 'Offline',
            message: message,
            timestamp: new Date().toISOString(),
            cached: true
        }),
        {
            status: status,
            statusText: status === 200 ? 'OK' : 'Service Unavailable',
            headers: {
                'Content-Type': 'application/json',
                'Cache-Control': 'no-cache'
            }
        }
    );
}
//#endregion

//#region Message Handling
self.addEventListener('message', event => {
    console.log('SW: Received message:', event.data);

    if (event.data?.type === 'SKIP_WAITING') {
        console.log('SW: Skip waiting requested');
        self.skipWaiting();
    }

    if (event.data?.type === 'GET_CACHE_STATUS') {
        caches.open(CACHE_NAME)
            .then(cache => cache.keys())
            .then(keys => {
                event.ports[0].postMessage({
                    type: 'CACHE_STATUS',
                    cachedUrls: keys.length,
                    cacheName: CACHE_NAME,
                    criticalAssets: CRITICAL_ASSETS.length
                });
            });
    }

    if (event.data?.type === 'FORCE_UPDATE') {
        caches.delete(CACHE_NAME).then(() => {
            console.log('SW: Cache cleared, reloading...');
            self.clients.matchAll().then(clients => {
                clients.forEach(client => client.navigate(client.url));
            });
        });
    }
});
//#endregion

//#region Background Sync (Future Enhancement)
// self.addEventListener('sync', event => {
//   if (event.tag === 'offline-attendance-sync') {
//     event.waitUntil(syncOfflineAttendance());
//   }
// });
//#endregion