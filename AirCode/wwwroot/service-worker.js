// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
//self.addEventListener('fetch', () => { });

// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Configurable, efficient caching with selective updates
 

// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Supports offline-first routing for specific pages

const CACHE_NAME = 'aircode-pwa-cache-v2';
const GITHUB_PAGES_BASE = '/AirCode/';

// Define offline-accessible routes
const OFFLINE_ROUTES = [
  '/',
  
  '/Admin/OfflineAttendanceEvent',
  '/Client/OfflineScan',
 
];

// Critical assets that must be cached for offline functionality
const CRITICAL_ASSETS = [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'index.html',
  GITHUB_PAGES_BASE + 'manifest.json',
  GITHUB_PAGES_BASE + 'css/app.css',
  GITHUB_PAGES_BASE + 'css/bootstrap/bootstrap.min.css',
  GITHUB_PAGES_BASE + 'js/app.js',
  GITHUB_PAGES_BASE + '_framework/blazor.webassembly.js',
  GITHUB_PAGES_BASE + '_framework/dotnet.wasm',
  GITHUB_PAGES_BASE + '_framework/dotnet.js',
  // Add your main assembly
  GITHUB_PAGES_BASE + '_framework/AirCode.dll',
  // Add other critical Blazor assets
];

// Install event - aggressively cache critical assets
self.addEventListener('install', event => {
  console.log('Service Worker installing...');
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => {
        console.log('Caching critical assets for offline support...');
        return cache.addAll(CRITICAL_ASSETS);
      })
      .then(() => {
        console.log('Service Worker installed successfully');
        return self.skipWaiting();
      })
      .catch(error => {
        console.error('Service Worker installation failed:', error);
        // Don't fail silently - cache what we can
        return caches.open(CACHE_NAME).then(cache => {
          return Promise.allSettled(
            CRITICAL_ASSETS.map(asset => cache.add(asset))
          );
        });
      })
  );
});

// Activate event - cleanup and take control
self.addEventListener('activate', event => {
  console.log('Service Worker activating...');
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => {
          if (cacheName !== CACHE_NAME) {
            console.log('Deleting old cache:', cacheName);
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => {
      console.log('Service Worker activated successfully');
      return self.clients.claim();
    })
  );
});

// Utility function to check if route is offline-accessible
function isOfflineRoute(url) {
  const pathname = new URL(url).pathname;
  const relativePath = pathname.replace(GITHUB_PAGES_BASE, '/');
  
  return OFFLINE_ROUTES.some(route => {
    if (route === '/') return relativePath === '/' || relativePath === '';
    return relativePath.startsWith(route);
  });
}

// Utility function to get offline fallback content
async function getOfflineFallback(request) {
  const cache = await caches.open(CACHE_NAME);
  
  // First try to get the exact cached response
  let response = await cache.match(request);
  if (response) return response;
  
  // For navigation requests, return cached index.html
  if (request.mode === 'navigate') {
    response = await cache.match(GITHUB_PAGES_BASE + 'index.html');
    if (response) return response;
  }
  
  // Return a basic offline page as last resort
  return new Response(
    `<!DOCTYPE html>
    <html>
    <head>
        <title>AirCode - Offline</title>
        <style>
            body { font-family: Arial, sans-serif; text-align: center; padding: 50px; }
            .offline-message { color: #666; }
        </style>
    </head>
    <body>
        <h1>AirCode</h1>
        <div class="offline-message">
            <p>You are currently offline.</p>
            <p>Limited functionality is available.</p>
            <button onclick="window.location.reload()">Retry Connection</button>
        </div>
    </body>
    </html>`,
    {
      headers: { 'Content-Type': 'text/html' }
    }
  );
}

// Main fetch handler with offline-first strategy for designated routes
self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);
  
  // Skip non-GET requests
  if (request.method !== 'GET') {
    return;
  }
  
  // Skip requests to external domains
  if (url.origin !== location.origin) {
    return;
  }
  
  // Handle navigation requests (HTML pages)
  if (request.mode === 'navigate') {
    event.respondWith(
      (async () => {
        try {
          // Check if this is an offline-accessible route
          if (isOfflineRoute(request.url)) {
            // Offline-first: try cache first, then network
            const cachedResponse = await caches.match(GITHUB_PAGES_BASE + 'index.html');
            if (cachedResponse) {
              console.log('Serving offline route from cache:', request.url);
              return cachedResponse;
            }
          }
          
          // Network-first for online-only routes
          const networkResponse = await fetch(request);
          if (networkResponse.ok) {
            // Cache successful responses
            const responseClone = networkResponse.clone();
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, responseClone);
          }
          return networkResponse;
          
        } catch (error) {
          console.log('Network failed, checking offline capability:', request.url);
          
          // If network fails, check if route is offline-accessible
          if (isOfflineRoute(request.url)) {
            return await getOfflineFallback(request);
          }
          
          // For online-only routes, return network error
          throw error;
        }
      })()
    );
    return;
  }
  
  // Handle asset requests (CSS, JS, WASM, etc.)
  event.respondWith(
    (async () => {
      try {
        // Check cache first for critical assets
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
          console.log('Serving asset from cache:', request.url);
          return cachedResponse;
        }
        
        // Fetch from network
        const networkResponse = await fetch(request);
        if (networkResponse.ok) {
          // Cache successful responses
          const responseClone = networkResponse.clone();
          const cache = await caches.open(CACHE_NAME);
          cache.put(request, responseClone);
        }
        return networkResponse;
        
      } catch (error) {
        console.error('Asset fetch failed:', error);
        
        // Return cached version if available
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
          return cachedResponse;
        }
        
        // For critical Blazor assets, this will cause app failure
        throw error;
      }
    })()
  );
});

// Handle service worker updates
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});

// Background sync for offline actions
self.addEventListener('sync', event => {
  if (event.tag === 'offline-data-sync') {
    event.waitUntil(
      // Implement your offline data synchronization logic here
      syncOfflineData()
    );
  }
});

async function syncOfflineData() {
  // Placeholder for offline data sync logic
  // This would handle syncing attendance data, scan results, etc.
  console.log('Syncing offline data...');
}

console.log('AirCode PWA Service Worker with offline routing loaded successfully');
