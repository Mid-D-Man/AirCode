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
const blazorRoutes = ['/', '/Admin/OfflineAttendanceEvent', '/Client/OfflineScan'];

// IMPORTANT: URLs that should bypass service worker completely
const bypassPatterns = [
    /_framework\/blazor\.webassembly\.js/,
    /_framework\/dotnet\./,
    /_framework\/.*\.wasm$/,
    /\/_framework\/.*\.dll$/,
    /\/_framework\/.*\.pdb$/,
    /\/api\//,  // API calls
    /\/_blazor\//,  // SignalR hub connections
];

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
    // CRITICAL: Let certain requests bypass service worker entirely
    if (shouldBypassServiceWorker(event.request)) {
        return; // Don't call event.respondWith, let the request go through normally
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
    
    // Bypass non-GET requests
    if (request.method !== 'GET') {
        return true;
    }
    
    // Bypass requests to other origins
    if (url.origin !== location.origin) {
        return true;
    }
    
    // Bypass critical Blazor framework requests
    return bypassPatterns.some(pattern => pattern.test(url.pathname));
}

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

        // Add assets in smaller batches with better error handling
        const batchSize = 10; // Reduced batch size
        for (let i = 0; i < assetsRequests.length; i += batchSize) {
            const batch = assetsRequests.slice(i, i + batchSize);
            try {
                await Promise.allSettled(batch.map(async (request) => {
                    try {
                        const response = await fetch(request.clone(), {
                            cache: 'no-cache',
                            signal: AbortSignal.timeout(10000) // 10 second timeout
                        });
                        if (response.ok) {
                            await cache.put(request, response);
                        }
                    } catch (error) {
                        console.warn('AirCode: Failed to cache asset:', request.url, error.message);
                    }
                }));
                
                console.log(`AirCode: Processed batch ${Math.floor(i/batchSize) + 1}/${Math.ceil(assetsRequests.length/batchSize)}`);
            } catch (error) {
                console.warn('AirCode: Batch processing error:', error);
            }
        }

        // Cache pages with timeout
        const pagesCache = await caches.open(pagesCacheName);

        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 5000);
            
            const indexResponse = await fetch('./index.html', { 
                cache: 'no-cache',
                signal: controller.signal
            });
            
            clearTimeout(timeoutId);
            
            if (indexResponse.ok) {
                await pagesCache.put('./index.html', indexResponse.clone());
                await pagesCache.put('/', indexResponse.clone());
                console.log('AirCode: Successfully cached index.html and root path');
            }
        } catch (error) {
            if (error.name === 'AbortError') {
                console.warn('AirCode: Index.html caching timed out');
            } else {
                console.error('AirCode: Failed to cache index.html:', error);
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

        console.info('AirCode Service worker: Activation complete');
    } catch (error) {
        console.error('AirCode Service worker: Activation failed:', error);
    }
}

async function onFetch(event) {
    const request = event.request;
    const url = new URL(request.url);

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
        // Try network first with timeout
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 8000); // 8 second timeout
        
        const networkResponse = await fetch(request, {
            cache: 'no-cache',
            signal: controller.signal
        });
        
        clearTimeout(timeoutId);

        // Cache successful responses
        if (networkResponse.ok) {
            const pagesCache = await caches.open(pagesCacheName);
            pagesCache.put(request, networkResponse.clone()).catch(error => {
                console.warn('AirCode: Failed to cache navigation response:', error);
            });
        }

        return networkResponse;
    } catch (error) {
        if (error.name === 'AbortError') {
            console.log('AirCode: Navigation request timed out, serving from cache:', pathname);
        } else {
            console.log('AirCode: Network failed for navigation, serving from cache:', pathname, error.message);
        }

        // Fallback to cache
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
        // Try network for uncached assets with timeout
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 5000);
        
        const networkResponse = await fetch(request, {
            signal: controller.signal
        });
        
        clearTimeout(timeoutId);

        // Cache successful responses if they match our patterns
        if (networkResponse.ok && shouldCacheAsset(request.url)) {
            cache.put(request, networkResponse.clone()).catch(error => {
                console.warn('AirCode: Failed to cache asset:', error);
            });
        }

        return networkResponse;
    } catch (error) {
        if (error.name === 'AbortError') {
            console.log('AirCode: Asset request timed out and not in cache:', request.url);
        } else {
            console.log('AirCode: Asset request failed and not in cache:', request.url, error.message);
        }
        
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
