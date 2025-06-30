// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Configurable, efficient caching with selective updates

// Configuration - toggle PWA functionality
const PWA_CONFIG = {
    enabled: true, // Set to false to disable PWA features
    version: '1.0.0',
    debug: false, // Enable for development debugging
    updateCheckInterval: 24 * 60 * 60 * 1000, // 24 hours
    forceUpdateOnVersionChange: true
};

// Cache configuration
const CACHE_CONFIG = {
    staticCacheName: `aircode-static-v${PWA_CONFIG.version}`,
    dynamicCacheName: `aircode-dynamic-v${PWA_CONFIG.version}`,
    runtimeCacheName: `aircode-runtime-v${PWA_CONFIG.version}`,
    maxDynamicCacheSize: 50,
    maxRuntimeCacheSize: 30
};

// Asset categorization for selective caching
const CACHE_STRATEGIES = {
    // Critical assets - always cache, high priority
    critical: {
        patterns: [
            /\/_framework\//,
            /\.wasm$/,
            /\.dll$/,
            /\.dat$/,
            /\.blat$/,
            /blazor\.webassembly\.js$/,
            /dotnet\.[^.]+\.js$/
        ],
        strategy: 'cacheFirst',
        maxAge: 7 * 24 * 60 * 60 * 1000 // 7 days
    },
    
    // UI assets - cache with update checks
    ui: {
        patterns: [
            /\.css$/,
            /\.js$/ && !/dotnet\.[^.]+\.js$/,
            /\.(png|jpg|jpeg|gif|svg|ico)$/,
            /\.(woff|woff2|ttf|eot)$/
        ],
        strategy: 'staleWhileRevalidate',
        maxAge: 3 * 24 * 60 * 60 * 1000 // 3 days
    },
    
    // Content - network first with fallback
    content: {
        patterns: [
            /\.html$/,
            /\.json$/,
            /api\//
        ],
        strategy: 'networkFirst',
        maxAge: 24 * 60 * 60 * 1000 // 1 day
    }
};

// Utility functions
function log(message, level = 'info') {
    if (PWA_CONFIG.debug || level === 'error') {
        console[level](`[AirCode SW] ${message}`);
    }
}

function getCacheStrategy(url) {
    for (const [name, config] of Object.entries(CACHE_STRATEGIES)) {
        if (config.patterns.some(pattern => pattern.test(url))) {
            return { name, ...config };
        }
    }
    return { name: 'default', strategy: 'networkFirst', maxAge: 60 * 60 * 1000 };
}

async function cleanupOldCaches() {
    const cacheNames = await caches.keys();
    const oldCaches = cacheNames.filter(name => 
        name.startsWith('aircode-') && 
        !Object.values(CACHE_CONFIG).includes(name)
    );
    
    await Promise.all(oldCaches.map(name => {
        log(`Deleting old cache: ${name}`);
        return caches.delete(name);
    }));
}

async function limitCacheSize(cacheName, maxSize) {
    const cache = await caches.open(cacheName);
    const keys = await cache.keys();
    
    if (keys.length > maxSize) {
        const keysToDelete = keys.slice(0, keys.length - maxSize);
        await Promise.all(keysToDelete.map(key => cache.delete(key)));
        log(`Trimmed ${keysToDelete.length} items from ${cacheName}`);
    }
}

// Enhanced caching strategies
async function cacheFirst(request, cacheName, maxAge) {
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);
    
    if (cachedResponse) {
        const cachedDate = new Date(cachedResponse.headers.get('sw-cached-date') || 0);
        const isExpired = Date.now() - cachedDate.getTime() > maxAge;
        
        if (!isExpired) {
            return cachedResponse;
        }
    }
    
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            const responseToCache = networkResponse.clone();
            responseToCache.headers.set('sw-cached-date', new Date().toISOString());
            await cache.put(request, responseToCache);
        }
        return networkResponse;
    } catch (error) {
        if (cachedResponse) {
            log(`Network failed, serving stale cache for: ${request.url}`, 'warn');
            return cachedResponse;
        }
        throw error;
    }
}

async function networkFirst(request, cacheName, maxAge) {
    const cache = await caches.open(cacheName);
    
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            const responseToCache = networkResponse.clone();
            responseToCache.headers.set('sw-cached-date', new Date().toISOString());
            await cache.put(request, responseToCache);
            await limitCacheSize(cacheName, CACHE_CONFIG.maxDynamicCacheSize);
        }
        return networkResponse;
    } catch (error) {
        const cachedResponse = await cache.match(request);
        if (cachedResponse) {
            log(`Network failed, serving cache for: ${request.url}`, 'warn');
            return cachedResponse;
        }
        throw error;
    }
}

async function staleWhileRevalidate(request, cacheName, maxAge) {
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);
    
    const fetchPromise = fetch(request).then(async (networkResponse) => {
        if (networkResponse.ok) {
            const responseToCache = networkResponse.clone();
            responseToCache.headers.set('sw-cached-date', new Date().toISOString());
            await cache.put(request, responseToCache);
        }
        return networkResponse;
    }).catch(error => {
        log(`Network update failed for: ${request.url}`, 'warn');
        return null;
    });
    
    return cachedResponse || await fetchPromise;
}

// Service Worker Event Handlers
self.addEventListener('install', event => {
    if (!PWA_CONFIG.enabled) {
        log('PWA disabled, skipping install');
        return;
    }
    
    log('Service Worker installing...');
    
    event.waitUntil(
        (async () => {
            try {
                // Pre-cache critical assets from manifest
                if (self.assetsManifest) {
                    const cache = await caches.open(CACHE_CONFIG.staticCacheName);
                    const criticalAssets = self.assetsManifest.assets
                        .filter(asset => 
                            CACHE_STRATEGIES.critical.patterns.some(pattern => 
                                pattern.test(asset.url)
                            )
                        )
                        .map(asset => new Request(asset.url, { 
                            integrity: asset.hash,
                            cache: 'no-cache' 
                        }));
                    
                    if (criticalAssets.length > 0) {
                        await cache.addAll(criticalAssets);
                        log(`Pre-cached ${criticalAssets.length} critical assets`);
                    }
                }
                
                // Skip waiting to activate immediately
                await self.skipWaiting();
                log('Service Worker installed successfully');
            } catch (error) {
                log(`Install failed: ${error.message}`, 'error');
            }
        })()
    );
});

self.addEventListener('activate', event => {
    if (!PWA_CONFIG.enabled) {
        log('PWA disabled, skipping activate');
        return;
    }
    
    log('Service Worker activating...');
    
    event.waitUntil(
        (async () => {
            try {
                await cleanupOldCaches();
                await self.clients.claim();
                
                // Notify clients about update
                const clients = await self.clients.matchAll();
                clients.forEach(client => {
                    client.postMessage({
                        type: 'SW_UPDATED',
                        version: PWA_CONFIG.version
                    });
                });
                
                log('Service Worker activated successfully');
            } catch (error) {
                log(`Activation failed: ${error.message}`, 'error');
            }
        })()
    );
});

self.addEventListener('fetch', event => {
    if (!PWA_CONFIG.enabled) {
        return;
    }
    
    const { request } = event;
    const url = new URL(request.url);
    
    // Skip non-GET requests and chrome-extension
    if (request.method !== 'GET' || url.protocol !== 'https:') {
        return;
    }
    
    // Handle navigation requests (SPA routing)
    if (request.mode === 'navigate') {
        event.respondWith(
            cacheFirst(new Request('/index.html'), CACHE_CONFIG.staticCacheName, 24 * 60 * 60 * 1000)
                .catch(() => fetch('/index.html'))
        );
        return;
    }
    
    // Apply caching strategy based on URL
    const strategy = getCacheStrategy(request.url);
    let cachePromise;
    
    switch (strategy.strategy) {
        case 'cacheFirst':
            cachePromise = cacheFirst(request, CACHE_CONFIG.staticCacheName, strategy.maxAge);
            break;
        case 'networkFirst':
            cachePromise = networkFirst(request, CACHE_CONFIG.dynamicCacheName, strategy.maxAge);
            break;
        case 'staleWhileRevalidate':
            cachePromise = staleWhileRevalidate(request, CACHE_CONFIG.runtimeCacheName, strategy.maxAge);
            break;
        default:
            return; // Let browser handle
    }
    
    event.respondWith(
        cachePromise.catch(error => {
            log(`Fetch failed for ${request.url}: ${error.message}`, 'error');
            return fetch(request);
        })
    );
});

// Handle messages from the app
self.addEventListener('message', event => {
    const { type, payload } = event.data || {};
    
    switch (type) {
        case 'SKIP_WAITING':
            self.skipWaiting();
            break;
            
        case 'GET_VERSION':
            event.ports[0].postMessage({
                version: PWA_CONFIG.version,
                enabled: PWA_CONFIG.enabled
            });
            break;
            
        case 'CLEAR_CACHE':
            if (payload && payload.confirm === true) {
                caches.keys().then(cacheNames => {
                    return Promise.all(
                        cacheNames
                            .filter(name => name.startsWith('aircode-'))
                            .map(name => caches.delete(name))
                    );
                }).then(() => {
                    event.ports[0].postMessage({ success: true });
                    log('All caches cleared by user request');
                });
            }
            break;
    }
});

// Background sync for offline data (if needed)
self.addEventListener('sync', event => {
    if (!PWA_CONFIG.enabled) return;
    
    if (event.tag === 'attendance-sync') {
        event.waitUntil(
            // Your offline sync logic here
            log('Background sync triggered for attendance data')
        );
    }
});

log(`Service Worker initialized - PWA: ${PWA_CONFIG.enabled ? 'Enabled' : 'Disabled'}, Version: ${PWA_CONFIG.version}`);



 