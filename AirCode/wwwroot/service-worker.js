// Enhanced Service Worker - Aggressive Offline Caching
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v7';

//#region Critical Assets Configuration
const CRITICAL_ASSETS = [
    // Core app routes
    BASE_PATH,
    BASE_PATH + 'index.html',
    BASE_PATH + 'Client/OfflineScan',
    BASE_PATH + 'Admin/OfflineAttendanceEvent',

    // PWA manifest & icons - FIXED: Conditional icon caching
    BASE_PATH + 'manifest.json',

    // Core CSS
    BASE_PATH + 'css/bootstrap/bootstrap.min.css',
    BASE_PATH + 'css/app.css',
    BASE_PATH + 'css/colors.css',
    BASE_PATH + 'css/responsive.css',

    // Blazor framework essentials - FIXED: Correct paths
    BASE_PATH + '_framework/blazor.webassembly.js',
    BASE_PATH + '_framework/blazor.boot.json',
    BASE_PATH + '_framework/dotnet.7.0.17.5xcw3lqzx7.js',

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

// FIXED: Icon assets moved to conditional loading
const ICON_ASSETS = [
    BASE_PATH + 'icon-192.png',
    BASE_PATH + 'icon-512.png',
    BASE_PATH + 'favicon.ico'
];

const SECONDARY_ASSETS = [
    // Additional JS utilities
    BASE_PATH + 'js/themeSwitcher.js',
    BASE_PATH + 'js/pageNavigator.js',
    BASE_PATH + 'js/debug.js',
    BASE_PATH + 'js/cameraUtil.js',
    BASE_PATH + 'js/firestoreModule.js',
    BASE_PATH + 'js/gpuPerformance.js',

    // Font icons
    BASE_PATH + 'css/open-iconic/font/css/open-iconic-bootstrap.min.css',

    // Configuration
    BASE_PATH + 'appsettings.json',

    // Blazor styles (may not exist during initial load)
    BASE_PATH + 'AirCode.styles.css'
];
//#endregion

//#region Service Worker Lifecycle
self.addEventListener('install', event => {
    console.log('SW: Installing v7 - Offline Bootstrap Fix');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(async cache => {
                // FIXED: Graceful critical asset caching
                const criticalResults = await Promise.allSettled(
                    CRITICAL_ASSETS.map(url =>
                        cache.add(url).catch(err => {
                            console.warn('Critical cache miss (non-fatal):', url);
                            return null;
                        })
                    )
                );

                // FIXED: Conditional icon caching (icons often missing during dev)
                const iconResults = await Promise.allSettled(
                    ICON_ASSETS.map(url =>
                        fetch(url).then(response => {
                            if (response.ok) {
                                return cache.put(url, response);
                            }
                            console.warn('Icon not available:', url);
                        }).catch(() => console.warn('Icon fetch failed:', url))
                    )
                );

                // Cache secondary assets (fail silently)
                const secondaryResults = await Promise.allSettled(
                    SECONDARY_ASSETS.map(url =>
                        cache.add(url).catch(err => console.warn('Secondary cache miss:', url))
                    )
                );

                const criticalSuccesses = criticalResults.filter(r => r.status === 'fulfilled').length;
                console.log(`SW: Cached ${criticalSuccesses}/${CRITICAL_ASSETS.length} critical assets`);

                // FIXED: Don't fail installation if some assets are missing
                return self.skipWaiting();
            })
            .catch(err => {
                console.error('SW: Cache initialization failed, continuing anyway:', err);
                return self.skipWaiting(); // FIXED: Always proceed with installation
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
                // FIXED: Notify clients that SW is ready for offline
                return self.clients.matchAll().then(clients => {
                    clients.forEach(client => {
                        client.postMessage({
                            type: 'SW_READY',
                            cacheName: CACHE_NAME
                        });
                    });
                });
            })
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

                // FIXED: Enhanced network fetch with better error handling
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

                        // FIXED: Better offline fallbacks
                        if (event.request.mode === 'navigate') {
                            // Navigation requests get index.html
                            return caches.match(BASE_PATH + 'index.html')
                                .then(fallback => {
                                    if (fallback) {
                                        console.log('SW: Serving offline fallback for navigation');
                                        return fallback;
                                    }
                                    return createOfflinePage();
                                });
                        }

                        // FIXED: Blazor framework files - return empty response instead of error
                        if (event.request.url.includes('_framework/') ||
                            event.request.url.includes('.dll') ||
                            event.request.url.includes('.json')) {
                            console.log('SW: Framework file unavailable offline:', event.request.url);
                            return new Response('{}', {
                                status: 200,
                                headers: { 'Content-Type': 'application/json' }
                            });
                        }

                        if (event.request.destination === 'image') {
                            return createPlaceholderImage();
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

// FIXED: Better offline page fallback
function createOfflinePage() {
    const offlineHtml = `
<!DOCTYPE html>
<html>
<head>
    <title>AirCode - Offline</title>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        body { font-family: system-ui; text-align: center; padding: 2rem; }
        .offline-message { margin-top: 2rem; }
    </style>
</head>
<body>
    <h1>AirCode</h1>
    <div class="offline-message">
        <h2>You're offline</h2>
        <p>Please check your connection and try again.</p>
        <button onclick="window.location.reload()">Retry</button>
    </div>
</body>
</html>`;

    return new Response(offlineHtml, {
        status: 200,
        headers: { 'Content-Type': 'text/html' }
    });
}

// FIXED: Placeholder image for missing icons
function createPlaceholderImage() {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="192" height="192" viewBox="0 0 192 192">
        <rect width="192" height="192" fill="#f0f0f0"/>
        <text x="96" y="96" text-anchor="middle" dy=".3em" font-family="system-ui" font-size="24" fill="#666">AirCode</text>
    </svg>`;

    return new Response(svg, {
        status: 200,
        headers: { 'Content-Type': 'image/svg+xml' }
    });
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
                    offlineReady: keys.length > 0
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