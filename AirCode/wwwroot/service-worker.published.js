// Simplified service worker for GitHub Pages Blazor WASM PWA - AirCode
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

const pagesCacheName = `aircode-pages-cache-${self.assetsManifest.version}`;
const blazorRoutes = ['/', '/Admin/OfflineAttendanceEvent', '/Client/OfflineScan'];

const bypassPatterns = [
    /\/api\//,
    /\/_blazor\//,
];

self.addEventListener('install', event => {
    console.log('AirCode SW: Installing');
    event.waitUntil(onInstall(event));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    console.log('AirCode SW: Activating');
    event.waitUntil(onActivate(event));
    self.clients.claim();
});

self.addEventListener('fetch', event => {
    if (shouldBypassServiceWorker(event.request)) {
        return;
    }
    
    event.respondWith(onFetch(event));
});

self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

function shouldBypassServiceWorker(request) {
    const url = new URL(request.url);
    
    if (request.method !== 'GET') {
        return true;
    }
    
    if (url.origin !== location.origin) {
        return true;
    }
    
    return bypassPatterns.some(pattern => pattern.test(url.pathname));
}

async function onInstall(event) {
    console.log('AirCode SW: Starting installation');

    try {
        const assetsRequests = self.assetsManifest.assets
            .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
            .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
            .map(asset => new Request(asset.url, {
                integrity: asset.hash,
                cache: 'no-cache'
            }));

        const cache = await caches.open(cacheName);

        const batchSize = 10;
        for (let i = 0; i < assetsRequests.length; i += batchSize) {
            const batch = assetsRequests.slice(i, i + batchSize);
            await Promise.allSettled(batch.map(async (request) => {
                try {
                    const response = await fetch(request.clone(), {
                        cache: 'no-cache',
                        signal: AbortSignal.timeout(10000)
                    });
                    if (response.ok) {
                        await cache.put(request, response);
                    }
                } catch (error) {
                    console.warn('AirCode SW: Failed to cache asset:', request.url);
                }
            }));
        }

        // Cache pages
        const pagesCache = await caches.open(pagesCacheName);
        
        try {
            const indexResponse = await fetch('./index.html', { 
                cache: 'no-cache',
                signal: AbortSignal.timeout(5000)
            });
            
            if (indexResponse.ok) {
                await pagesCache.put('./index.html', indexResponse.clone());
                await pagesCache.put('/', indexResponse.clone());
                
                for (const route of blazorRoutes) {
                    await pagesCache.put(route, indexResponse.clone());
                }
            }
        } catch (error) {
            console.warn('AirCode SW: Failed to cache pages');
        }

        console.log('AirCode SW: Installation complete');
    } catch (error) {
        console.error('AirCode SW: Installation failed:', error);
    }
}

async function onActivate(event) {
    console.log('AirCode SW: Cleaning old caches');

    try {
        const cacheKeys = await caches.keys();
        await Promise.all(cacheKeys
            .filter(key =>
                (key.startsWith(cacheNamePrefix) && key !== cacheName) ||
                (key.startsWith('aircode-pages-cache-') && key !== pagesCacheName)
            )
            .map(key => caches.delete(key))
        );

        console.log('AirCode SW: Activation complete');
    } catch (error) {
        console.error('AirCode SW: Activation failed:', error);
    }
}

async function onFetch(event) {
    const request = event.request;

    if (request.mode === 'navigate') {
        return handleNavigationRequest(request);
    }

    return handleAssetRequest(request);
}

async function handleNavigationRequest(request) {
    try {
        const networkResponse = await fetch(request, {
            cache: 'no-cache',
            signal: AbortSignal.timeout(8000)
        });

        if (networkResponse.ok) {
            const pagesCache = await caches.open(pagesCacheName);
            pagesCache.put(request, networkResponse.clone()).catch(() => {});
        }

        return networkResponse;
    } catch (error) {
        const pagesCache = await caches.open(pagesCacheName);

        let cachedResponse = await pagesCache.match(request);
        if (!cachedResponse) {
            cachedResponse = await pagesCache.match('/');
        }
        if (!cachedResponse) {
            cachedResponse = await pagesCache.match('./index.html');
        }

        if (cachedResponse) {
            return cachedResponse;
        }

        return new Response('Offline - Page not available', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }
}

async function handleAssetRequest(request) {
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        return cachedResponse;
    }

    try {
        const networkResponse = await fetch(request);

        if (networkResponse.ok && shouldCacheAsset(request.url)) {
            cache.put(request, networkResponse.clone()).catch(() => {});
        }

        return networkResponse;
    } catch (error) {
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
