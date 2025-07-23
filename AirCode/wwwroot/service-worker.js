// Enhanced Service Worker - Aggressive Offline Caching
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v6';

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

    // Blazor framework essentials
    BASE_PATH + '_framework/blazor.webassembly.js',
    BASE_PATH + '_framework/blazor.boot.json',
    BASE_PATH + '_framework/dotnet.7.0.17.5xcw3lqzx7.js',
    BASE_PATH + '_framework/blazor.boot.json',
    BASE_PATH + '_framework/dotnet.wasm',
    BASE_PATH + '_framework/AirCode.dll',
    BASE_PATH + '_framework/System.*.dll',

    // Blazor compiled assemblies 
    BASE_PATH + '_framework/Microsoft.AspNetCore.Components.WebAssembly.dll',

    // Critical offline services
    BASE_PATH + 'js/pwaManager.js',
    BASE_PATH + 'js/connectivityServices.js',
    BASE_PATH + 'js/offlineCredentialsHandler.js',
    BASE_PATH + 'js/cryptographyHandler.js',
    BASE_PATH + 'js/qrCodeModule.js',
    
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
    BASE_PATH + 'js/debug.js',

    // Font icons
    BASE_PATH + 'css/open-iconic/font/css/open-iconic-bootstrap.min.css',

    // Configuration
    BASE_PATH + 'appsettings.json'
];
//#endregion

//#region Service Worker Lifecycle
self.addEventListener('install', event => {
    console.log('SW: Installing v5');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                // Cache critical assets first (must succeed)
                const criticalPromises = CRITICAL_ASSETS.map(url =>
                    cache.add(url).catch(err => {
                        console.warn('Critical cache miss:', url, err);
                        throw err; // Fail fast for critical assets
                    })
                );

                // Cache secondary assets (can fail silently)
                const secondaryPromises = SECONDARY_ASSETS.map(url =>
                    cache.add(url).catch(err => console.warn('Secondary cache miss:', url))
                );

                return Promise.all([
                    Promise.all(criticalPromises),
                    Promise.allSettled(secondaryPromises)
                ]);
            })
            .then(() => {
                console.log('SW: Critical assets cached successfully');
                return self.skipWaiting();
            })
            .catch(err => {
                console.error('SW: Failed to cache critical assets:', err);
                throw err;
            })
    );
});

self.addEventListener('activate', event => {
    console.log('SW: Activating v5');
    event.waitUntil(
        caches.keys()
            .then(names => Promise.all(
                names.map(name => name !== CACHE_NAME ? caches.delete(name) : null)
            ))
            .then(() => self.clients.claim())
            .then(() => console.log('SW: Activated and claimed clients'))
    );
});
//#endregion

//#region Fetch Strategy - Cache First with Network Fallback
self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    // Skip external requests
    if (!event.request.url.startsWith(self.location.origin)) return;

    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    console.log('SW: Serving from cache:', event.request.url);
                    return cachedResponse;
                }

                // Network fetch with caching
                return fetch(event.request)
                    .then(fetchResponse => {
                        if (!fetchResponse.ok) {
                            throw new Error(`Network error: ${fetchResponse.status}`);
                        }

                        // Clone and cache successful responses
                        const responseClone = fetchResponse.clone();
                        caches.open(CACHE_NAME)
                            .then(cache => cache.put(event.request, responseClone))
                            .catch(err => console.warn('Cache write failed:', err));

                        console.log('SW: Serving from network:', event.request.url);
                        return fetchResponse;
                    })
                    .catch(error => {
                        console.warn('SW: Network failed for:', event.request.url, error);

                        // Offline fallbacks for specific request types
                        if (event.request.mode === 'navigate') {
                            // Navigation requests get index.html
                            return caches.match(BASE_PATH + 'index.html')
                                .then(fallback => fallback || createOfflineResponse());
                        }

                        if (event.request.destination === 'image') {
                            // Image requests get placeholder or fail gracefully
                            return new Response('', {
                                status: 200,
                                statusText: 'OK',
                                headers: { 'Content-Type': 'image/svg+xml' }
                            });
                        }

                        // Generic offline response
                        return createOfflineResponse();
                    });
            })
    );
});
//#endregion

//#region Utility Functions
function createOfflineResponse() {
    return new Response(
        JSON.stringify({
            error: 'Offline',
            message: 'This request is not available offline',
            timestamp: new Date().toISOString()
        }),
        {
            status: 503,
            statusText: 'Service Unavailable',
            headers: { 'Content-Type': 'application/json' }
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
                    cacheName: CACHE_NAME
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