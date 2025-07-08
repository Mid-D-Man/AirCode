// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Complete offline support with all required Blazor assets

const CACHE_NAME = 'aircode-pwa-cache-v3';
const GITHUB_PAGES_BASE = '/AirCode/';
const OFFLINE_CACHE_NAME = 'aircode-offline-cache-v2';
const RUNTIME_CACHE_NAME = 'aircode-runtime-cache-v1';

// Define offline-accessible routes
const OFFLINE_ROUTES = [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'Admin/OfflineAttendanceEven',
  GITHUB_PAGES_BASE + 'Client/OfflineScan'
];

// Essential Blazor WASM assets - these are required for offline boot
const ESSENTIAL_BLAZOR_ASSETS = [
  // Core HTML and manifest
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'index.html',
  GITHUB_PAGES_BASE + 'manifest.json',
  
  // Styling
  GITHUB_PAGES_BASE + 'css/app.css',
  GITHUB_PAGES_BASE + 'css/bootstrap/bootstrap.min.css',
  GITHUB_PAGES_BASE + 'css/colors.css',
  GITHUB_PAGES_BASE + 'css/responsive.css',
  GITHUB_PAGES_BASE + 'AirCode.styles.css',
  
  // Core Blazor framework files
  GITHUB_PAGES_BASE + '_framework/blazor.webassembly.js',
  GITHUB_PAGES_BASE + '_framework/dotnet.wasm',
  GITHUB_PAGES_BASE + '_framework/dotnet.js',
  
  // Application scripts
  GITHUB_PAGES_BASE + 'js/pwaManager.js',
  GITHUB_PAGES_BASE + 'js/pwaInterop.js',
  GITHUB_PAGES_BASE + 'js/connectivityServices.js',
  GITHUB_PAGES_BASE + 'js/authHelper.js',
  GITHUB_PAGES_BASE + 'js/site.js',
  GITHUB_PAGES_BASE + 'js/debug.js',
  GITHUB_PAGES_BASE + 'js/themeSwitcher.js',
  GITHUB_PAGES_BASE + 'js/cryptographyHandler.js',
  GITHUB_PAGES_BASE + 'js/offlineCredentialsHandler.js',
  GITHUB_PAGES_BASE + 'js/cameraUtil.js',
  GITHUB_PAGES_BASE + 'js/floatingQrDrag.js',
  GITHUB_PAGES_BASE + 'js/firestoreModule.js',
  GITHUB_PAGES_BASE + 'js/qrCodeModule.js',
  GITHUB_PAGES_BASE + 'js/gpuPerformance.js',
  GITHUB_PAGES_BASE + 'js/search-helpers.js',
  GITHUB_PAGES_BASE + 'js/validateKeyAndIV.js',
  
  // Icons
  GITHUB_PAGES_BASE + 'favicon.png',
  GITHUB_PAGES_BASE + 'favicon.ico',
  GITHUB_PAGES_BASE + 'icon-512.png',
  GITHUB_PAGES_BASE + 'icon-192.png'
];

// Common Blazor WASM runtime files (these get generated during build)
const BLAZOR_RUNTIME_PATTERNS = [
  '_framework/dotnet.*.js',
  '_framework/dotnet.*.wasm',
  '_framework/icudt*.dat',
  '_framework/blazor.boot.json',
  '_framework/*.dll',
  '_framework/*.pdb',
  '_framework/*.dll.br',
  '_framework/*.wasm.br',
  '_framework/Microsoft.*.dll',
  '_framework/System.*.dll',
  '_framework/netstandard.dll',
  '_framework/AirCode.dll'
];

// Cache discovered assets from network
let discoveredAssets = new Set();

// Install event - comprehensive asset caching
self.addEventListener('install', event => {
  console.log('Service Worker installing...');
  
  event.waitUntil(
    Promise.all([
      // Cache essential assets first
      caches.open(CACHE_NAME).then(cache => {
        console.log('Caching essential assets...');
        return cache.addAll(ESSENTIAL_BLAZOR_ASSETS.filter(asset => asset));
      }),
      
      // Discover and cache Blazor runtime assets
      discoverAndCacheBlazorAssets(),
      
      // Cache offline route fallbacks
      caches.open(OFFLINE_CACHE_NAME).then(cache => {
        console.log('Preparing offline routes...');
        return Promise.all(
          OFFLINE_ROUTES.map(route => {
            return cache.put(route, new Response(getOfflineHTML(), {
              status: 200,
              statusText: 'OK',
              headers: new Headers({
                'Content-Type': 'text/html',
                'Cache-Control': 'no-cache'
              })
            }));
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

// Discover and cache Blazor runtime assets
async function discoverAndCacheBlazorAssets() {
  try {
    const cache = await caches.open(RUNTIME_CACHE_NAME);
    
    // Try to fetch blazor.boot.json to get the asset manifest
    const bootResponse = await fetch(GITHUB_PAGES_BASE + '_framework/blazor.boot.json');
    if (bootResponse.ok) {
      const bootData = await bootResponse.json();
      
      // Cache the boot.json itself
      await cache.put(GITHUB_PAGES_BASE + '_framework/blazor.boot.json', bootResponse.clone());
      
      // Cache all resources listed in blazor.boot.json
      const resourcesToCache = [];
      
      // Add runtime resources
      if (bootData.resources) {
        Object.keys(bootData.resources.runtime || {}).forEach(file => {
          resourcesToCache.push(GITHUB_PAGES_BASE + '_framework/' + file);
        });
        
        // Add assembly resources
        Object.keys(bootData.resources.assembly || {}).forEach(file => {
          resourcesToCache.push(GITHUB_PAGES_BASE + '_framework/' + file);
        });
        
        // Add satellite resources
        Object.keys(bootData.resources.satelliteResources || {}).forEach(culture => {
          Object.keys(bootData.resources.satelliteResources[culture] || {}).forEach(file => {
            resourcesToCache.push(GITHUB_PAGES_BASE + '_framework/' + file);
          });
        });
      }
      
      // Cache discovered resources
      console.log('Discovered', resourcesToCache.length, 'Blazor assets');
      for (const resource of resourcesToCache) {
        try {
          const response = await fetch(resource);
          if (response.ok) {
            await cache.put(resource, response);
            discoveredAssets.add(resource);
          }
        } catch (error) {
          console.warn('Failed to cache resource:', resource, error);
        }
      }
    }
  } catch (error) {
    console.error('Failed to discover Blazor assets:', error);
  }
}

// Generate offline HTML with proper structure
function getOfflineHTML() {
  return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <title>AirCode - Offline</title>
    <base href="${GITHUB_PAGES_BASE}" />
    
    <!-- CSS -->
    <link href="css/bootstrap/bootstrap.min.css" rel="stylesheet" />
    <link href="css/app.css" rel="stylesheet" />
    <link href="css/colors.css" rel="stylesheet" />
    <link href="css/responsive.css" rel="stylesheet" />
    <link href="AirCode.styles.css" rel="stylesheet" />
    
    <!-- Icons -->
    <link rel="icon" type="image/png" href="favicon.png" />
    <link href="manifest.json" rel="manifest" />
</head>
<body>
    <div id="app">
        <svg class="loading-progress">
            <circle r="40%" cx="50%" cy="50%" />
            <circle r="40%" cx="50%" cy="50%" />
        </svg>
        <div class="loading-progress-text">Loading offline mode...</div>
    </div>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="" class="reload">Reload</a>
        <a class="dismiss">🗙</a>
    </div>

    <!-- Framework -->
    <script src="_framework/blazor.webassembly.js"></script>
    
    <!-- PWA Scripts -->
    <script src="js/pwaManager.js"></script>
    <script src="js/pwaInterop.js"></script>
    <script src="js/connectivityServices.js"></script>
    <script src="js/authHelper.js"></script>
    <script src="js/site.js"></script>
    <script src="js/debug.js"></script>
    
    <!-- Essential app scripts -->
    <script src="js/themeSwitcher.js"></script>
    <script src="js/cryptographyHandler.js"></script>
    <script src="js/offlineCredentialsHandler.js"></script>
    <script src="js/cameraUtil.js"></script>
    <script src="js/floatingQrDrag.js"></script>
    <script src="js/qrCodeModule.js"></script>
    <script src="js/gpuPerformance.js"></script>
    <script src="js/search-helpers.js"></script>
    <script src="js/validateKeyAndIV.js"></script>
    
    <!-- Offline mode indicator -->
    <script>
        console.log('Running in offline mode');
        
        // Set offline flag for the app
        window.isOfflineMode = true;
        
        // Override fetch for offline mode
        const originalFetch = window.fetch;
        window.fetch = function(url, options) {
            // Allow service worker to handle all requests
            return originalFetch.apply(this, arguments);
        };
        
        // Notify when Blazor fails to load
        window.addEventListener('error', (event) => {
            console.error('Offline loading error:', event.error);
        });
    </script>
</body>
</html>`;
}

// Activate event - cleanup old caches
self.addEventListener('activate', event => {
  console.log('Service Worker activating...');
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => {
          if (cacheName !== CACHE_NAME && 
              cacheName !== OFFLINE_CACHE_NAME && 
              cacheName !== RUNTIME_CACHE_NAME) {
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

// Enhanced fetch handler with comprehensive offline support
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
            // Cache successful responses
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
          // Return offline version
          return caches.match(request)
            .then(cachedResponse => {
              if (cachedResponse) {
                return cachedResponse;
              }
              
              // Return appropriate offline response
              if (isOfflineAccessible(request.url)) {
                return new Response(getOfflineHTML(), {
                  status: 200,
                  statusText: 'OK',
                  headers: new Headers({
                    'Content-Type': 'text/html',
                    'Cache-Control': 'no-cache'
                  })
                });
              } else {
                return new Response(`
                  <!DOCTYPE html>
                  <html>
                  <head>
                    <meta charset="utf-8">
                    <title>AirCode - Offline</title>
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
            });
        })
    );
    return;
  }
  
  // Handle asset requests with comprehensive caching
  event.respondWith(
    // Try cache first (cache-first strategy for assets)
    caches.match(request)
      .then(response => {
        if (response) {
          console.log('Serving from cache:', request.url);
          return response;
        }
        
        // Not in cache, try to fetch from network
        return fetch(request)
          .then(response => {
            if (response.ok) {
              const responseClone = response.clone();
              
              // Determine which cache to use
              let cacheName = CACHE_NAME;
              if (request.url.includes('_framework/')) {
                cacheName = RUNTIME_CACHE_NAME;
              }
              
              // Cache the response
              caches.open(cacheName).then(cache => {
                cache.put(request, responseClone);
              });
              
              return response;
            }
            throw new Error('Network response was not ok');
          })
          .catch(error => {
            console.error('Fetch failed for:', request.url, error);
            
            // Try to find in any cache as fallback
            return caches.match(request)
              .then(cachedResponse => {
                if (cachedResponse) {
                  return cachedResponse;
                }
                
                // For critical framework files, return a meaningful error
                if (request.url.includes('_framework/')) {
                  return new Response('Framework resource not available offline', {
                    status: 503,
                    statusText: 'Service Unavailable'
                  });
                }
                
                // For other assets, return generic error
                return new Response('Asset not available offline', {
                  status: 503,
                  statusText: 'Service Unavailable'
                });
              });
          });
      })
  );
});

// Handle service worker messages
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
  
  if (event.data && event.data.type === 'UPDATE_ASSETS') {
    // Handle dynamic asset updates
    const assets = event.data.assets || [];
    caches.open(CACHE_NAME).then(cache => {
      assets.forEach(asset => {
        if (!discoveredAssets.has(asset)) {
          fetch(asset).then(response => {
            if (response.ok) {
              cache.put(asset, response);
              discoveredAssets.add(asset);
            }
          }).catch(error => {
            console.warn('Failed to cache discovered asset:', asset, error);
          });
        }
      });
    });
  }
});

// Background sync for offline actions
self.addEventListener('sync', event => {
  if (event.tag === 'background-sync') {
    event.waitUntil(
      Promise.resolve()
    );
  }
});

console.log('AirCode PWA Service Worker loaded with comprehensive offline support');
