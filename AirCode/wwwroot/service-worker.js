// Version identifier - increment this to force cache updates
const VERSION = 'v1.0.0';
const CACHE_NAME = `AirCode-${VERSION}`;
const BASE_URL = '/AirCode/';

// Critical resources for offline operation
const OFFLINE_RESOURCES = [
    BASE_URL,
    `${BASE_URL}index.html`,
    `${BASE_URL}_framework/blazor.webassembly.js`,
    `${BASE_URL}_framework/wasm/dotnet.wasm`,
    `${BASE_URL}_framework/wasm/dotnet.js`,
    `${BASE_URL}_framework/blazor.boot.json`,
    `${BASE_URL}manifest.webmanifest`,
    `${BASE_URL}icon-192.png`,
    `${BASE_URL}icon-512.png`
];

// Cache-first strategy for static assets
const CACHE_FIRST_PATTERNS = [
    /\/_framework\//,
    /\/css\//,
    /\/js\//,
    /\/icons?\//,
    /\.(css|js|woff2?|png|jpg|ico|svg)$/
];

// Network-first strategy for dynamic content
const NETWORK_FIRST_PATTERNS = [
    /\/api\//,
    /\.json$/
];

// Install event - precache critical resources
self.addEventListener('install', event => {
    console.log('[SW] Installing version:', VERSION);

    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(OFFLINE_RESOURCES))
            .then(() => self.skipWaiting())
    );
});

// Activate event - cleanup old caches . 
self.addEventListener('activate', event => {
    console.log('[SW] Activating version:', VERSION);

    event.waitUntil(
        Promise.all([
            // Delete old caches
            caches.keys().then(cacheNames =>
                Promise.all(
                    cacheNames
                        .filter(name => name.startsWith('AirCode-') && name !== CACHE_NAME)
                        .map(name => caches.delete(name))
                )
            ),
            // Take control immediately
            clients.claim()
        ])
    );
});

// Fetch event - implement caching strategies
self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);

    // Skip non-GET requests
    if (request.method !== 'GET') return;

    // Skip chrome-extension and other non-http protocols
    if (!url.protocol.startsWith('http')) return;

    event.respondWith(handleRequest(request));
});
//GitHub Pages specific handling
async function handleNavigation(request) {
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            return networkResponse;
        }
    } catch (error) {
        console.log('[SW] Network failed for navigation, serving from cache');
    }

    // GitHub Pages fallback - serve index.html for SPA routing
    const cache = await caches.open(CACHE_NAME);
    const cachedResponse = await cache.match(`${BASE_URL}index.html`);

    if (cachedResponse) {
        return cachedResponse;
    }

    return new Response('App is offline', {
        status: 200,
        headers: { 'Content-Type': 'text/html' }
    });
}
async function handleRequest(request) {
    const url = new URL(request.url);
    const relativePath = url.pathname;

    try {
        // Navigation requests - always serve index.html for SPA routing
        if (request.mode === 'navigate') {
            return await handleNavigation(request);
        }

        // Static assets - cache first
        if (shouldUseCacheFirst(relativePath)) {
            return await cacheFirst(request);
        }

        // Dynamic content - network first
        if (shouldUseNetworkFirst(relativePath)) {
            return await networkFirst(request);
        }

        // Default strategy - cache first
        return await cacheFirst(request);

    } catch (error) {
        console.error('[SW] Request failed:', error);
        return await getOfflineFallback(request);
    }
}

async function handleNavigation(request) {
    try {
        // Try network first for navigation
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            return networkResponse;
        }
    } catch (error) {
        console.log('[SW] Network failed for navigation, serving from cache');
    }

    // Fallback to cached index.html
    const cache = await caches.open(CACHE_NAME);
    const cachedResponse = await cache.match(`${BASE_URL}index.html`);

    if (cachedResponse) {
        return cachedResponse;
    }

    // Ultimate fallback
    return new Response('App is offline', {
        status: 200,
        headers: { 'Content-Type': 'text/html' }
    });
}

async function cacheFirst(request) {
    const cache = await caches.open(CACHE_NAME);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        // Serve from cache, update in background
        fetchAndCache(request).catch(() => {});
        return cachedResponse;
    }

    // Not in cache, fetch and cache
    const networkResponse = await fetch(request);
    if (networkResponse.ok) {
        await cache.put(request, networkResponse.clone());
    }
    return networkResponse;
}

async function networkFirst(request) {
    try {
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
            // Cache successful responses
            const cache = await caches.open(CACHE_NAME);
            await cache.put(request, networkResponse.clone());
        }
        return networkResponse;
    } catch (error) {
        // Network failed, try cache
        const cache = await caches.open(CACHE_NAME);
        const cachedResponse = await cache.match(request);
        if (cachedResponse) {
            return cachedResponse;
        }
        throw error;
    }
}

async function fetchAndCache(request) {
    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(CACHE_NAME);
            await cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        console.log('[SW] Background fetch failed:', error);
    }
}

async function getOfflineFallback(request) {
    const cache = await caches.open(CACHE_NAME);

    // Try to serve a cached version
    const cachedResponse = await cache.match(request);
    if (cachedResponse) {
        return cachedResponse;
    }

    // Navigation fallback
    if (request.mode === 'navigate') {
        const indexResponse = await cache.match(`${BASE_URL}index.html`);
        if (indexResponse) {
            return indexResponse;
        }
    }

    // Generic offline response
    return new Response('Offline - Resource not available', {
        status: 503,
        statusText: 'Service Unavailable',
        headers: { 'Content-Type': 'text/plain' }
    });
}

function shouldUseCacheFirst(path) {
    return CACHE_FIRST_PATTERNS.some(pattern => pattern.test(path));
}

function shouldUseNetworkFirst(path) {
    return NETWORK_FIRST_PATTERNS.some(pattern => pattern.test(path));
}

// Handle Blazor framework updates
self.addEventListener('message', event => {
    if (event.data && event.data.type === 'CACHE_BLAZOR_ASSETS') {
        event.waitUntil(cacheBlazorAssets(event.data.assets));
    }
});

async function cacheBlazorAssets(assets) {
    const cache = await caches.open(CACHE_NAME);
    const requests = assets.map(asset => new Request(asset, { cache: 'no-cache' }));

    try {
        await cache.addAll(requests);
        console.log('[SW] Blazor assets cached successfully');
    } catch (error) {
        console.error('[SW] Failed to cache Blazor assets:', error);
    }
}