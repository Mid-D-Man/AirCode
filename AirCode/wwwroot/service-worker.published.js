
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'aircode-offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
    /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/,
    /\.json$/, /\.css$/, /\.woff$/, /\.woff2$/,
    /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/,
    /\.blat$/, /\.dat$/, /\.svg$/, /\.map$/  
];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

const pagesCacheName = `aircode-pages-cache-${self.assetsManifest.version}`;
const blazorRoutes = ['/', '/Admin/OfflineAttendanceEvent', '/Client/OfflineScan'];

// Add cache refresh configuration
const CACHE_DURATION = 7 * 24 * 60 * 60 * 1000; // 7 days in milliseconds
const METADATA_CACHE = 'aircode-cache-metadata';

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
    
    // Add periodic cache validation
    if (event.data && event.data.type === 'VALIDATE_CACHE') {
        event.waitUntil(validateCaches());
    }
});

// Add periodic sync for cache validation
self.addEventListener('periodicsync', event => {
    if (event.tag === 'validate-cache') {
        event.waitUntil(validateCaches());
    }
});

// Store cache timestamp metadata
async function setCacheTimestamp(cacheName) {
    const metaCache = await caches.open(METADATA_CACHE);
    const timestamp = new Date().toISOString();
    const response = new Response(JSON.stringify({ timestamp, cacheName }));
    await metaCache.put(cacheName, response);
}

async function getCacheTimestamp(cacheName) {
    try {
        const metaCache = await caches.open(METADATA_CACHE);
        const response = await metaCache.match(cacheName);
        if (response) {
            const data = await response.json();
            return new Date(data.timestamp);
        }
    } catch (error) {
        console.warn('AirCode SW: Error reading cache timestamp:', error);
    }
    return null;
}

async function validateCaches() {
    try {
        console.log('AirCode SW: Validating caches');
        
        // Check if caches are still valid
        const timestamp = await getCacheTimestamp(cacheName);
        if (timestamp) {
            const age = Date.now() - timestamp.getTime();
            if (age > CACHE_DURATION) {
                console.log('AirCode SW: Cache expired, refreshing...');
                // Trigger a cache refresh on next fetch
                await setCacheTimestamp(cacheName);
            }
        }
    } catch (error) {
        console.error('AirCode SW: Cache validation failed:', error);
    }
}

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

        // Batch caching with error resilience
        const batchSize = 10;
        let cachedCount = 0;
        
        for (let i = 0; i < assetsRequests.length; i += batchSize) {
            const batch = assetsRequests.slice(i, i + batchSize);
            const results = await Promise.allSettled(batch.map(async (request) => {
                try {
                    const response = await fetch(request.clone(), {
                        cache: 'no-cache',
                        signal: AbortSignal.timeout(10000)
                    });
                    if (response.ok) {
                        await cache.put(request, response);
                        cachedCount++;
                        return true;
                    }
                } catch (error) {
                    console.warn('AirCode SW: Failed to cache asset:', request.url);
                    return false;
                }
            }));
        }
        
        console.log(`AirCode SW: Cached ${cachedCount}/${assetsRequests.length} assets`);
        
        // Set cache timestamp
        await setCacheTimestamp(cacheName);

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

        // Request persistent storage on activation
        if ('storage' in navigator && 'persist' in navigator.storage) {
            const isPersisted = await navigator.storage.persist();
            console.log(`AirCode SW: Storage persistence: ${isPersisted}`);
        }

        // Register for periodic background sync if available
        if ('periodicSync' in self.registration) {
            try {
                await self.registration.periodicSync.register('validate-cache', {
                    minInterval: 24 * 60 * 60 * 1000 // 24 hours
                });
                console.log('AirCode SW: Periodic sync registered');
            } catch (error) {
                console.warn('AirCode SW: Periodic sync registration failed:', error);
            }
        }

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
    // Try cache first for navigation requests when offline
    if (!navigator.onLine) {
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
    }
    
    // Try network with fallback to cache
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
    // Cache-first strategy for assets
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        // Return cached response immediately
        return cachedResponse;
        
        // Optionally update cache in background (uncomment if needed)
        // event.waitUntil(updateCache(request, cache));
    }

    // Not in cache, fetch from network
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

// Background cache update function
async function updateCache(request, cache) {
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            await cache.put(request, networkResponse);
        }
    } catch (error) {
        // Silent fail - we already returned cached version
    }
}
