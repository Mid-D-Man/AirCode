// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
//self.addEventListener('fetch', () => { });

// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Configurable, efficient caching with selective updates
 

// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Supports offline-first routing for specific pages

// Enhanced PWA Service Worker for Blazor WASM .NET 7
// GitHub Pages Compatible with Offline Route Support

const CACHE_NAME = 'aircode-pwa-cache-v2';
const GITHUB_PAGES_BASE = '/AirCode/';
const OFFLINE_CACHE_NAME = 'aircode-offline-cache-v1';

// Define offline-accessible routes
const OFFLINE_ROUTES = [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'Admin/OfflineAttendanceEven',
  GITHUB_PAGES_BASE + 'Client/OfflineScan'
];

// Assets to cache - core application files
const ASSETS_TO_CACHE = [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'index.html',
  GITHUB_PAGES_BASE + 'manifest.json',
  GITHUB_PAGES_BASE + 'css/app.css',
  GITHUB_PAGES_BASE + 'css/bootstrap/bootstrap.min.css',
  GITHUB_PAGES_BASE + 'js/app.js',
  GITHUB_PAGES_BASE + 'js/pwaManager.js',
  GITHUB_PAGES_BASE + 'js/pwaInterop.js',
  GITHUB_PAGES_BASE + '_framework/blazor.webassembly.js',
  GITHUB_PAGES_BASE + '_framework/dotnet.wasm',
  GITHUB_PAGES_BASE + '_framework/dotnet.js'
];

// Install event - cache core assets and offline routes
self.addEventListener('install', event => {
  console.log('Service Worker installing...');
  event.waitUntil(
    Promise.all([
      caches.open(CACHE_NAME).then(cache => {
        console.log('Caching core assets...');
        return cache.addAll(ASSETS_TO_CACHE);
      }),
      caches.open(OFFLINE_CACHE_NAME).then(cache => {
        console.log('Caching offline routes...');
        // Pre-cache offline route responses
        return Promise.all(
          OFFLINE_ROUTES.map(route => {
            return fetch(route).then(response => {
              if (response.ok) {
                return cache.put(route, response);
              }
            }).catch(() => {
              // If route doesn't exist yet, cache the index.html for it
              return cache.put(route, new Response('', {
                status: 200,
                statusText: 'OK',
                headers: new Headers({
                  'Content-Type': 'text/html',
                  'Cache-Control': 'no-cache'
                })
              }));
            });
          })
        );
      })
    ])
    .then(() => {
      console.log('Service Worker installed successfully');
      return self.skipWaiting();
    })
    .catch(error => {
      console.error('Service Worker installation failed:', error);
    })
  );
});

// Activate event - cleanup old caches
self.addEventListener('activate', event => {
  console.log('Service Worker activating...');
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => {
          if (cacheName !== CACHE_NAME && cacheName !== OFFLINE_CACHE_NAME) {
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

// Helper function to check if route is offline accessible
function isOfflineAccessible(url) {
  const pathname = new URL(url).pathname;
  return OFFLINE_ROUTES.some(route => {
    const routePath = new URL(route, self.location.origin).pathname;
    return pathname === routePath || pathname.startsWith(routePath + '/');
  });
}

// Helper function to create offline fallback response
function createOfflineFallback(request) {
  const url = new URL(request.url);
  
  if (isOfflineAccessible(request.url)) {
    // Return cached index.html for offline-accessible routes
    return caches.match(GITHUB_PAGES_BASE + 'index.html')
      .then(response => {
        if (response) {
          return response;
        }
        // Fallback HTML for offline routes
        return new Response(`
          <!DOCTYPE html>
          <html>
          <head>
            <meta charset="utf-8">
            <title>AirCode - Offline</title>
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <base href="${GITHUB_PAGES_BASE}">
            <link href="css/bootstrap/bootstrap.min.css" rel="stylesheet">
            <link href="css/app.css" rel="stylesheet">
          </head>
          <body>
            <div id="app">
              <div class="d-flex justify-content-center align-items-center min-vh-100">
                <div class="text-center">
                  <h3>AirCode - Offline Mode</h3>
                  <p>Limited functionality available offline</p>
                  <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                  </div>
                </div>
              </div>
            </div>
            <script src="_framework/blazor.webassembly.js"></script>
          </body>
          </html>
        `, {
          status: 200,
          statusText: 'OK',
          headers: new Headers({
            'Content-Type': 'text/html',
            'Cache-Control': 'no-cache'
          })
        });
      });
  } else {
    // Return offline page for non-accessible routes
    return new Response(`
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <title>AirCode - Offline</title>
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <base href="${GITHUB_PAGES_BASE}">
        <link href="css/bootstrap/bootstrap.min.css" rel="stylesheet">
      </head>
      <body>
        <div class="d-flex justify-content-center align-items-center min-vh-100">
          <div class="text-center">
            <h3>🔌 No Internet Connection</h3>
            <p>This page requires an internet connection.</p>
            <p>Available offline pages:</p>
            <ul class="list-unstyled">
              <li><a href="${GITHUB_PAGES_BASE}">Dashboard</a></li>
              <li><a href="${GITHUB_PAGES_BASE}Admin/OfflineAttendanceEven">Offline Attendance</a></li>
              <li><a href="${GITHUB_PAGES_BASE}Client/OfflineScan">Offline Scan</a></li>
            </ul>
            <button class="btn btn-primary mt-3" onclick="window.location.reload()">
              Try Again
            </button>
          </div>
        </div>
      </body>
      </html>
    `, {
      status: 200,
      statusText: 'OK',
      headers: new Headers({
        'Content-Type': 'text/html'
      })
    });
  }
}

// Fetch event - enhanced offline support
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
      fetch(request)
        .then(response => {
          if (response.ok) {
            // Cache successful navigation responses for offline routes
            if (isOfflineAccessible(request.url)) {
              const responseClone = response.clone();
              caches.open(OFFLINE_CACHE_NAME).then(cache => {
                cache.put(request, responseClone);
              });
            }
            return response;
          }
          throw new Error('Network response was not ok');
        })
        .catch(() => {
          // Check if we have a cached version
          return caches.match(request)
            .then(cachedResponse => {
              if (cachedResponse) {
                return cachedResponse;
              }
              // Return appropriate offline response
              return createOfflineFallback(request);
            });
        })
    );
    return;
  }
  
  // Handle asset requests (CSS, JS, images, etc.)
  event.respondWith(
    caches.match(request)
      .then(response => {
        if (response) {
          console.log('Serving from cache:', request.url);
          return response;
        }
        
        // Not in cache, fetch from network
        return fetch(request).then(response => {
          // Only cache successful responses
          if (response.ok) {
            const responseClone = response.clone();
            caches.open(CACHE_NAME).then(cache => {
              cache.put(request, responseClone);
            });
          }
          return response;
        });
      })
      .catch(error => {
        console.error('Fetch failed for asset:', request.url, error);
        
        // For critical assets, try to serve from any cache
        return caches.match(request)
          .then(cachedResponse => {
            if (cachedResponse) {
              return cachedResponse;
            }
            
            // For Blazor framework files, return appropriate fallback
            if (request.url.includes('_framework/')) {
              return caches.match(GITHUB_PAGES_BASE + 'index.html');
            }
            
            // Return a basic error response
            return new Response('Asset not available offline', {
              status: 503,
              statusText: 'Service Unavailable'
            });
          });
      })
  );
});

// Handle service worker updates
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
  
  if (event.data && event.data.type === 'GET_OFFLINE_ROUTES') {
    event.ports[0].postMessage({
      type: 'OFFLINE_ROUTES_RESPONSE',
      routes: OFFLINE_ROUTES
    });
  }
});

// Background sync for offline actions
self.addEventListener('sync', event => {
  if (event.tag === 'background-sync') {
    event.waitUntil(
      // Handle background sync logic here
      Promise.resolve()
    );
  }
});

// Push notification support
self.addEventListener('push', event => {
  if (event.data) {
    const options = {
      body: event.data.text(),
      icon: GITHUB_PAGES_BASE + 'icons/icon-192x192.png',
      badge: GITHUB_PAGES_BASE + 'icons/icon-72x72.png',
      vibrate: [200, 100, 200],
      data: {
        url: GITHUB_PAGES_BASE
      }
    };
    
    event.waitUntil(
      self.registration.showNotification('AirCode', options)
    );
  }
});

// Notification click handler
self.addEventListener('notificationclick', event => {
  event.notification.close();
  
  if (event.notification.data && event.notification.data.url) {
    event.waitUntil(
      clients.openWindow(event.notification.data.url)
    );
  }
});

console.log('AirCode PWA Service Worker loaded successfully with offline route support');
