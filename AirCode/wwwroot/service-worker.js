// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
//self.addEventListener('fetch', () => { });

// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Configurable, efficient caching with selective updates
 

// Custom service worker for Blazor WASM PWA on GitHub Pages
// This replaces the default service-worker.published.js with GitHub Pages compatibility

const CACHE_NAME = 'aircode-pwa-cache-v1';
const GITHUB_PAGES_BASE = '/AirCode/';

// Assets to cache - these will be populated by your build process
const ASSETS_TO_CACHE = [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'index.html',
  GITHUB_PAGES_BASE + 'manifest.json',
  GITHUB_PAGES_BASE + 'css/app.css',
  GITHUB_PAGES_BASE + 'css/bootstrap/bootstrap.min.css',
  GITHUB_PAGES_BASE + 'js/app.js',
  // Add other critical assets here
];

// Install event - cache core assets
self.addEventListener('install', event => {
  console.log('Service Worker installing...');
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => {
        console.log('Caching core assets...');
        return cache.addAll(ASSETS_TO_CACHE);
      })
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

// Fetch event - serve cached content or fetch from network
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
      caches.match(GITHUB_PAGES_BASE + 'index.html')
        .then(response => {
          if (response) {
            return response;
          }
          return fetch(request).then(response => {
            if (response.ok) {
              const responseClone = response.clone();
              caches.open(CACHE_NAME).then(cache => {
                cache.put(request, responseClone);
              });
            }
            return response;
          });
        })
        .catch(() => {
          // Return cached index.html for offline support
          return caches.match(GITHUB_PAGES_BASE + 'index.html');
        })
    );
    return;
  }
  
  // Handle asset requests
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
        console.error('Fetch failed:', error);
        // Return a fallback response if needed
        if (request.destination === 'document') {
          return caches.match(GITHUB_PAGES_BASE + 'index.html');
        }
      })
  );
});

// Handle service worker updates
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});

// Background sync for offline actions (optional)
self.addEventListener('sync', event => {
  if (event.tag === 'background-sync') {
    event.waitUntil(
      // Handle background sync logic here
      Promise.resolve()
    );
  }
});

// Push notification support (optional)
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

console.log('AirCode PWA Service Worker loaded successfully');
