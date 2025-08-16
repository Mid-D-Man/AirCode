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

// Cache expiry constants
const CACHE_EXPIRY_DAYS = 7;
const CACHE_EXPIRY_MS = CACHE_EXPIRY_DAYS * 24 * 60 * 60 * 1000;

// Critical Blazor framework patterns requiring network-first
const frameworkPatterns = [
    /_framework\/blazor\.webassembly\.js/,
    /_framework\/dotnet\./,
    /_framework\/.*\.wasm$/
];

const pagesCacheName = `aircode-pages-cache-${self.assetsManifest.version}`;

// IMPORTANT: URLs that should bypass service worker completely
const bypassPatterns = [
    /\/api\//,
    /\/_blazor\//,
];

self.addEventListener('install', event => {
    logSW('info', 'Service worker installing', { version: self.assetsManifest.version });
    event.waitUntil(onInstall(event));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    logSW('info', 'Service worker activating');
    event.waitUntil(onActivate(event));
    self.clients.claim();
});

self.addEventListener('fetch', event => {
    if (shouldBypassServiceWorker(event.request)) {
        logSW('debug', 'Bypassing SW for request', { url: event.request.url });
        return;
    }
    
    event.respondWith(onFetch(event));
});

self.addEventListener('message', event => {
    logSW('info', 'Received message', { data: event.data });
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

function shouldBypassServiceWorker(request) {
    const url = new URL(request.url);
    
    if (request.method !== 'GET') {
        logSW('debug', 'Bypassing non-GET request', { method: request.method, url: url.href });
        return true;
    }
    
    if (url.origin !== location.origin) {
        logSW('debug', 'Bypassing external origin', { origin: url.origin });
        return true;
    }
    
    const shouldBypass = bypassPatterns.some(pattern => pattern.test(url.pathname));
    if (shouldBypass) {
        logSW('debug', 'Bypassing pattern match', { pathname: url.pathname });
    }
    
    return shouldBypass;
}

function isCacheExpired(timestamp) {
    if (!timestamp) return true;
    return (Date.now() - parseInt(timestamp)) > CACHE_EXPIRY_MS;
}

function isFrameworkAsset(url) {
    return frameworkPatterns.some(pattern => pattern.test(url));
}

async function addToCache(cache, request, response) {
    try {
        const responseWithTimestamp = new Response(response.body, {
            status: response.status,
            statusText: response.statusText,
            headers: {
                ...Object.fromEntries(response.headers),
                'sw-cached-at': Date.now().toString()
            }
        });
        
        await cache.put(request, responseWithTimestamp);
        logSW('debug', 'Successfully cached', { url: request.url });
    } catch (error) {
        logSW('error', 'Failed to cache', { url: request.url, error: error.message });
        throw error;
    }
}

async function onInstall(event) {
    logSW('info', 'Starting installation', { assetCount: self.assetsManifest.assets.length });

    try {
        const assetsRequests = self.assetsManifest.assets
            .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
            .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
            .map(asset => new Request(asset.url, {
                integrity: asset.hash,
                cache: 'no-cache'
            }));

        logSW('info', 'Filtered assets for caching', { count: assetsRequests.length });

        const cache = await caches.open(cacheName);
        let successCount = 0;
        let failureCount = 0;

        const batchSize = 10;
        for (let i = 0; i < assetsRequests.length; i += batchSize) {
            const batch = assetsRequests.slice(i, i + batchSize);
            try {
                await Promise.allSettled(batch.map(async (request) => {
                    try {
                        const response = await fetch(request.clone(), {
                            cache: 'no-cache',
                            signal: AbortSignal.timeout(10000)
                        });
                        if (response.ok) {
                            await addToCache(cache, request, response);
                            successCount++;
                        } else {
                            logSW('warn', 'Failed to fetch asset', { url: request.url, status: response.status });
                            failureCount++;
                        }
                    } catch (error) {
                        logSW('error', 'Asset caching error', { url: request.url, error: error.message });
                        failureCount++;
                    }
                }));
                
                logSW('info', 'Batch processed', { 
                    batch: Math.floor(i/batchSize) + 1, 
                    total: Math.ceil(assetsRequests.length/batchSize),
                    success: successCount,
                    failures: failureCount
                });
            } catch (error) {
                logSW('error', 'Batch processing error', { error: error.message });
                failureCount += batch.length;
            }
        }

        // Cache Blazor routes for offline navigation
        const pagesCache = await caches.open(pagesCacheName);

        try {
            const indexResponse = await fetch('./index.html', { 
                cache: 'no-cache',
                signal: AbortSignal.timeout(5000)
            });
            
            if (indexResponse.ok) {
                // Cache index.html for all Blazor routes
                await addToCache(pagesCache, new Request('./index.html'), indexResponse.clone());
                await addToCache(pagesCache, new Request('/'), indexResponse.clone());
                
                for (const route of blazorRoutes) {
                    try {
                        await addToCache(pagesCache, new Request(route), indexResponse.clone());
                        logSW('info', 'Cached Blazor route', { route });
                    } catch (error) {
                        logSW('error', 'Failed to cache route', { route, error: error.message });
                    }
                }
                
                logSW('info', 'Successfully cached all pages and routes', { routeCount: blazorRoutes.length });
            }
        } catch (error) {
            logSW('error', 'Failed to cache index.html', { error: error.message });
        }

        logSW('info', 'Installation completed', { 
            totalAssets: assetsRequests.length,
            successful: successCount, 
            failed: failureCount 
        });
    } catch (error) {
        logSW('error', 'Installation failed', { error: error.message, stack: error.stack });
        throw error;
    }
}

async function onActivate(event) {
    logSW('info', 'Starting activation');

    try {
        const cacheKeys = await caches.keys();
        const keysToDelete = cacheKeys.filter(key =>
            (key.startsWith(cacheNamePrefix) && key !== cacheName) ||
            (key.startsWith('aircode-pages-cache-') && key !== pagesCacheName)
        );

        logSW('info', 'Cleaning old caches', { 
            totalCaches: cacheKeys.length,
            toDelete: keysToDelete.length,
            deletingKeys: keysToDelete
        });

        await Promise.all(keysToDelete.map(async (key) => {
            try {
                await caches.delete(key);
                logSW('info', 'Deleted cache', { key });
            } catch (error) {
                logSW('error', 'Failed to delete cache', { key, error: error.message });
            }
        }));

        logSW('info', 'Activation complete');
    } catch (error) {
        logSW('error', 'Activation failed', { error: error.message, stack: error.stack });
        throw error;
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
    const url = new URL(request.url);
    const pathname = url.pathname;

    logSW('info', 'Handling navigation request', { pathname });

    try {
        const networkResponse = await fetch(request, {
            cache: 'no-cache',
            signal: AbortSignal.timeout(8000)
        });

        if (networkResponse.ok) {
            logSW('info', 'Network navigation successful', { pathname, status: networkResponse.status });
            const pagesCache = await caches.open(pagesCacheName);
            addToCache(pagesCache, request, networkResponse.clone()).catch(error => {
                logSW('error', 'Failed to cache navigation response', { pathname, error: error.message });
            });
        }

        return networkResponse;
    } catch (error) {
        logSW('warn', 'Network navigation failed, checking cache', { 
            pathname, 
            error: error.message,
            isTimeout: error.name === 'AbortError'
        });

        const pagesCache = await caches.open(pagesCacheName);

        // Try multiple cache resolution strategies
        const cacheStrategies = [
            () => pagesCache.match(request),
            () => pathname !== '/' ? pagesCache.match(pathname) : null,
            () => pagesCache.match('/'),
            () => pagesCache.match('./index.html')
        ];

        for (let i = 0; i < cacheStrategies.length; i++) {
            const strategy = cacheStrategies[i];
            if (!strategy) continue;

            try {
                const cachedResponse = await strategy();
                if (cachedResponse) {
                    const cacheTimestamp = cachedResponse.headers.get('sw-cached-at');
                    const isExpired = isCacheExpired(cacheTimestamp);
                    
                    logSW('info', 'Found cached navigation response', { 
                        pathname, 
                        strategy: i,
                        expired: isExpired,
                        cacheAge: cacheTimestamp ? Math.floor((Date.now() - parseInt(cacheTimestamp)) / (1000 * 60 * 60 * 24)) : 'unknown'
                    });

                    if (isExpired) {
                        logSW('error', 'Cached page expired', { 
                            pathname, 
                            cacheAge: Math.floor((Date.now() - parseInt(cacheTimestamp)) / (1000 * 60 * 60 * 24))
                        });
                        return new Response(`
                            <html>
                                <body style="font-family: Arial, sans-serif; text-align: center; padding: 50px;">
                                    <h2>Cache Expired</h2>
                                    <p>Please connect to the internet to refresh AirCode</p>
                                    <p>Offline functionality requires going online at least once per week</p>
                                    <button onclick="window.location.reload()">Retry</button>
                                </body>
                            </html>
                        `, {
                            status: 503,
                            statusText: 'Cache Expired',
                            headers: { 'Content-Type': 'text/html' }
                        });
                    }
                    
                    return cachedResponse;
                }
            } catch (cacheError) {
                logSW('error', 'Cache strategy failed', { strategy: i, error: cacheError.message });
            }
        }

        logSW('error', 'No cached navigation response found', { pathname });
        return new Response(`
            <html>
                <body style="font-family: Arial, sans-serif; text-align: center; padding: 50px;">
                    <h2>Offline - Page Not Available</h2>
                    <p>This page is not cached for offline use</p>
                    <button onclick="window.location.href='/'">Go Home</button>
                </body>
            </html>
        `, {
            status: 503,
            statusText: 'Service Unavailable',
            headers: { 'Content-Type': 'text/html' }
        });
    }
}

async function handleAssetRequest(request) {
    const url = new URL(request.url);
    const cache = await caches.open(cacheName);

    logSW('debug', 'Handling asset request', { url: url.pathname });

    // Network-first for critical framework assets
    if (isFrameworkAsset(url.pathname)) {
        logSW('info', 'Framework asset detected, network-first strategy', { url: url.pathname });
        
        try {
            const networkResponse = await fetch(request, {
                signal: AbortSignal.timeout(3000)
            });
            
            if (networkResponse.ok) {
                logSW('info', 'Framework asset fetched from network', { url: url.pathname, status: networkResponse.status });
                addToCache(cache, request, networkResponse.clone()).catch(error => {
                    logSW('error', 'Failed to cache framework asset', { url: url.pathname, error: error.message });
                });
                return networkResponse;
            }
        } catch (error) {
            logSW('warn', 'Framework asset network failed, checking cache', { 
                url: url.pathname, 
                error: error.message,
                isTimeout: error.name === 'AbortError'
            });
        }

        // Check cache for framework assets
        const cachedResponse = await cache.match(request);
        if (cachedResponse) {
            const cacheTimestamp = cachedResponse.headers.get('sw-cached-at');
            const isExpired = isCacheExpired(cacheTimestamp);
            const cacheAge = cacheTimestamp ? Math.floor((Date.now() - parseInt(cacheTimestamp)) / (1000 * 60 * 60 * 24)) : 'unknown';
            
            logSW('info', 'Framework asset found in cache', { 
                url: url.pathname, 
                expired: isExpired,
                cacheAge
            });

            if (isExpired) {
                logSW('error', 'Critical framework asset expired', { 
                    url: url.pathname,
                    cacheAge
                });
                return new Response('Framework asset expired - Please go online', {
                    status: 503,
                    statusText: 'Framework Asset Expired'
                });
            }
            return cachedResponse;
        }

        logSW('error', 'Framework asset not available', { url: url.pathname });
        return new Response('Framework asset not available', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }

    // Cache-first for other assets
    const cachedResponse = await cache.match(request);
    if (cachedResponse) {
        const cacheTimestamp = cachedResponse.headers.get('sw-cached-at');
        const isExpired = isCacheExpired(cacheTimestamp);
        const cacheAge = cacheTimestamp ? Math.floor((Date.now() - parseInt(cacheTimestamp)) / (1000 * 60 * 60 * 24)) : 'unknown';

        logSW('debug', 'Asset found in cache', { 
            url: url.pathname, 
            expired: isExpired,
            cacheAge
        });

        if (!isExpired) {
            return cachedResponse;
        }
        
        logSW('info', 'Cached asset expired, fetching fresh', { url: url.pathname, cacheAge });
    }

    try {
        const networkResponse = await fetch(request, {
            signal: AbortSignal.timeout(5000)
        });

        if (networkResponse.ok && shouldCacheAsset(request.url)) {
            logSW('debug', 'Asset fetched and cached', { url: url.pathname, status: networkResponse.status });
            addToCache(cache, request, networkResponse.clone()).catch(error => {
                logSW('error', 'Failed to cache asset', { url: url.pathname, error: error.message });
            });
        }

        return networkResponse;
    } catch (error) {
        logSW('warn', 'Asset network request failed', { 
            url: url.pathname, 
            error: error.message,
            hasExpiredCache: !!cachedResponse
        });

        // Return expired cache as fallback if network fails
        if (cachedResponse) {
            logSW('info', 'Serving expired cached asset as fallback', { url: url.pathname });
            return cachedResponse;
        }
        
        logSW('error', 'Asset not available offline', { url: url.pathname });
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
