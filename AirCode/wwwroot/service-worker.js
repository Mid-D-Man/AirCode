// Enhanced Service Worker - Aggressive Offline Caching
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v4';

// Comprehensive asset list
const CRITICAL_ASSETS = [
  BASE_PATH,
  BASE_PATH + 'index.html',
  BASE_PATH + 'manifest.json',
  BASE_PATH + 'icon-192.png',
  BASE_PATH + 'icon-512.png',
  BASE_PATH + 'favicon.ico',
  BASE_PATH + 'css/bootstrap/bootstrap.min.css',
  BASE_PATH + 'css/app.css',
  BASE_PATH + 'css/colors.css',
  BASE_PATH + 'css/responsive.css',
  BASE_PATH + '_framework/blazor.webassembly.js',
  BASE_PATH + 'js/pwaManager.js',
  BASE_PATH + 'js/pwaInterop.js',
  BASE_PATH + 'js/connectivityServices.js'
];

self.addEventListener('install', event => {
  console.log('SW: Installing');
  event.waitUntil(
      caches.open(CACHE_NAME)
          .then(cache => {
            // Cache critical assets first
            return Promise.allSettled(
                CRITICAL_ASSETS.map(url =>
                    cache.add(url).catch(err => console.warn('Cache miss:', url))
                )
            );
          })
          .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  console.log('SW: Activating');
  event.waitUntil(
      caches.keys()
          .then(names => Promise.all(
              names.map(name => name !== CACHE_NAME ? caches.delete(name) : null)
          ))
          .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;

  event.respondWith(
      caches.match(event.request)
          .then(response => {
            if (response) return response;

            return fetch(event.request)
                .then(fetchResponse => {
                  if (!fetchResponse.ok) throw new Error('Network error');

                  const responseClone = fetchResponse.clone();
                  caches.open(CACHE_NAME)
                      .then(cache => cache.put(event.request, responseClone));

                  return fetchResponse;
                })
                .catch(() => {
                  // Fallback for navigation requests
                  if (event.request.mode === 'navigate') {
                    return caches.match(BASE_PATH + 'index.html');
                  }
                  return new Response('Offline', { status: 503 });
                });
          })
  );
});

// Handle messages from main thread
self.addEventListener('message', event => {
  if (event.data?.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});
