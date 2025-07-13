// Enhanced PWA Service Worker for Blazor WASM .NET 7
// Comprehensive offline support with dynamic path resolution

// Dynamic base path resolution for GitHub Pages
const getBasePath = () => {
  const pathname = self.location.pathname;
  if (pathname.includes('/AirCode/')) {
    return '/AirCode/';
  }
  const segments = pathname.split('/').filter(Boolean);
  return segments.length > 0 ? `/${segments[0]}/` : '/';
};

const GITHUB_PAGES_BASE = getBasePath();
const CACHE_NAME = 'aircode-pwa-cache-v4';
const OFFLINE_CACHE_NAME = 'aircode-offline-cache-v3';
const RUNTIME_CACHE_NAME = 'aircode-runtime-cache-v2';

// Define offline-accessible routes
const OFFLINE_ROUTES = [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'Admin/OfflineAttendanceEven',
  GITHUB_PAGES_BASE + 'Client/OfflineScan'
];

// Essential assets for offline boot - using dynamic base
const getEssentialAssets = () => [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'index.html',
  GITHUB_PAGES_BASE + 'manifest.json',
  GITHUB_PAGES_BASE + 'css/app.css',
  GITHUB_PAGES_BASE + 'css/bootstrap/bootstrap.min.css',
  GITHUB_PAGES_BASE + 'css/colors.css',
  GITHUB_PAGES_BASE + 'css/responsive.css',
  GITHUB_PAGES_BASE + 'AirCode.styles.css',
  GITHUB_PAGES_BASE + '_framework/blazor.webassembly.js',
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
  GITHUB_PAGES_BASE + 'js/qrCodeModule.js',
  GITHUB_PAGES_BASE + 'js/gpuPerformance.js',
  GITHUB_PAGES_BASE + 'js/search-helpers.js',
  GITHUB_PAGES_BASE + 'js/validateKeyAndIV.js',
  GITHUB_PAGES_BASE + 'favicon.png',
  GITHUB_PAGES_BASE + 'favicon.ico',
  GITHUB_PAGES_BASE + 'icon-512.png',
  GITHUB_PAGES_BASE + 'icon-192.png'
];

// Asset discovery and validation
let discoveredAssets = new Set();
let blazorBootManifest = null;

// Install event - comprehensive asset caching
self.addEventListener('install', event => {
  console.log('Service Worker installing with base path:', GITHUB_PAGES_BASE);

  event.waitUntil(
    (async () => {
      try {
        // Phase 1: Cache essential assets
        const cache = await caches.open(CACHE_NAME);
        const essentialAssets = getEssentialAssets();
        
        console.log('Caching essential assets...');
        const cachePromises = essentialAssets.map(async (asset) => {
          try {
            const response = await fetch(asset);
            if (response.ok) {
              await cache.put(asset, response);
              discoveredAssets.add(asset);
              console.log('Cached:', asset);
            } else {
              console.warn('Failed to cache (HTTP error):', asset, response.status);
            }
          } catch (error) {
            console.warn('Failed to cache (network error):', asset, error.message);
          }
        });
        
        await Promise.allSettled(cachePromises);
        
        // Phase 2: Discover and cache Blazor runtime assets
        await discoverAndCacheBlazorAssets();
        
        // Phase 3: Setup offline route fallbacks
        await setupOfflineRoutes();
        
        console.log('Service Worker installed successfully');
        return self.skipWaiting();
        
      } catch (error) {
        console.error('Service Worker installation failed:', error);
        throw error;
      }
    })()
  );
});

// Enhanced Blazor asset discovery
async function discoverAndCacheBlazorAssets() {
  try {
    const runtimeCache = await caches.open(RUNTIME_CACHE_NAME);
    
    // Try to fetch blazor.boot.json
    const bootJsonUrl = GITHUB_PAGES_BASE + '_framework/blazor.boot.json';
    console.log('Fetching Blazor boot manifest:', bootJsonUrl);
    
    const bootResponse = await fetch(bootJsonUrl);
    if (!bootResponse.ok) {
      console.error('Failed to fetch blazor.boot.json:', bootResponse.status);
      return;
    }
    
    const bootData = await bootResponse.json();
    blazorBootManifest = bootData;
    
    // Cache the boot.json itself
    await runtimeCache.put(bootJsonUrl, bootResponse.clone());
    
    // Collect all resources from manifest
    const resourcesToCache = [];
    
    if (bootData.resources) {
      // Runtime resources (dotnet.wasm, etc.)
      Object.keys(bootData.resources.runtime || {}).forEach(file => {
        resourcesToCache.push(GITHUB_PAGES_BASE + '_framework/' + file);
      });
      
      // Assembly resources (.dll files)
      Object.keys(bootData.resources.assembly || {}).forEach(file => {
        resourcesToCache.push(GITHUB_PAGES_BASE + '_framework/' + file);
      });
      
      // Satellite resources (localization)
      Object.keys(bootData.resources.satelliteResources || {}).forEach(culture => {
        Object.keys(bootData.resources.satelliteResources[culture] || {}).forEach(file => {
          resourcesToCache.push(GITHUB_PAGES_BASE + '_framework/' + file);
        });
      });
    }
    
    console.log('Discovered', resourcesToCache.length, 'Blazor runtime assets');
    
    // Cache discovered resources with error handling
    const cachePromises = resourcesToCache.map(async (resource) => {
      try {
        const response = await fetch(resource);
        if (response.ok) {
          await runtimeCache.put(resource, response);
          discoveredAssets.add(resource);
          return { success: true, resource };
        } else {
          console.warn('Failed to cache runtime asset:', resource, response.status);
          return { success: false, resource, error: response.status };
        }
      } catch (error) {
        console.warn('Network error caching runtime asset:', resource, error.message);
        return { success: false, resource, error: error.message };
      }
    });
    
    const results = await Promise.allSettled(cachePromises);
    const successCount = results.filter(r => r.status === 'fulfilled' && r.value.success).length;
    
    console.log(`Cached ${successCount}/${resourcesToCache.length} runtime assets`);
    
  } catch (error) {
    console.error('Failed to discover Blazor assets:', error);
  }
}

// Setup offline route fallbacks
async function setupOfflineRoutes() {
  try {
    const offlineCache = await caches.open(OFFLINE_CACHE_NAME);
    console.log('Setting up offline routes...');
    
    const offlineHTML = getOfflineHTML();
    
    for (const route of OFFLINE_ROUTES) {
      await offlineCache.put(route, new Response(offlineHTML, {
        status: 200,
        statusText: 'OK',
        headers: new Headers({
          'Content-Type': 'text/html',
          'Cache-Control': 'no-cache'
        })
      }));
    }
    
    console.log('Offline routes configured');
  } catch (error) {
    console.error('Failed to setup offline routes:', error);
  }
}

// Generate comprehensive offline HTML
function getOfflineHTML() {
  return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <title>AirCode - Offline Mode</title>
    <base href="${GITHUB_PAGES_BASE}" />
    
    <!-- SPA routing fix for offline -->
    <script type="text/javascript">
        (function(l) {
            if (l.search[1] === '/' ) {
                let decoded = l.search.slice(1).split('&').map(function(s) {
                    return s.replace(/~and~/g, '&')
                }).join('?');
                window.history.replaceState(null, null,
                    l.pathname.slice(0, -1) + decoded + l.hash
                );
            }
        }(window.location))
    </script>
    
    <!-- CSS -->
    <link href="css/bootstrap/bootstrap.min.css" rel="stylesheet" />
    <link href="css/app.css" rel="stylesheet" />
    <link href="css/colors.css" rel="stylesheet" />
    <link href="css/responsive.css" rel="stylesheet" />
    <link href="AirCode.styles.css" rel="stylesheet" />
    
    <!-- Icons -->
    <link rel="icon" type="image/png" href="favicon.png" />
    <link href="manifest.json" rel="manifest" />
    <link rel="apple-touch-icon" sizes="512x512" href="icon-512.png" />
    <link rel="apple-touch-icon" sizes="192x192" href="icon-192.png" />
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
    <script src="js/themeSwitcher.js"></script>
    <script src="js/cryptographyHandler.js"></script>
    <script src="js/offlineCredentialsHandler.js"></script>
    <script src="js/cameraUtil.js"></script>
    <script src="js/floatingQrDrag.js"></script>
    <script src="js/qrCodeModule.js"></script>
    <script src="js/gpuPerformance.js"></script>
    <script src="js/search-helpers.js"></script>
    <script src="js/validateKeyAndIV.js"></script>
    
    <!-- Offline mode initialization -->
    <script>
        console.log('Initializing offline mode with base path:', '${GITHUB_PAGES_BASE}');
        
        // Set global offline flag
        window.isOfflineMode = true;
        window.isOnline = false;
        
        // Override network-dependent functions
        window.addEventListener('load', () => {
            // Disable network-dependent features
            if (window.disableNetworkFeatures) {
                window.disableNetworkFeatures();
            }
            
            // Initialize offline-specific UI
            if (window.initOfflineUI) {
                window.initOfflineUI();
            }
        });
        
        // Enhanced error handling for offline mode
        window.addEventListener('error', (event) => {
            console.error('Offline mode error:', event.error);
            
            // Handle specific Blazor loading errors
            if (event.error && event.error.message && 
                event.error.message.includes('_framework')) {
                console.warn('Framework loading issue in offline mode');
            }
        });
        
        // Override fetch to handle offline scenarios
        const originalFetch = window.fetch;
        window.fetch = function(url, options) {
            // For offline mode, all requests go through service worker
            return originalFetch.apply(this, arguments)
                .catch(error => {
                    console.warn('Offline fetch failed:', url, error);
                    throw error;
                });
        };
    </script>
</body>
</html>`;
}

// Activate event - cleanup and optimization
self.addEventListener('activate', event => {
  console.log('Service Worker activating...');
  event.waitUntil(
    (async () => {
      // Clean up old caches
      const cacheNames = await caches.keys();
      const deletePromises = cacheNames.map(cacheName => {
        if (cacheName !== CACHE_NAME && 
            cacheName !== OFFLINE_CACHE_NAME && 
            cacheName !== RUNTIME_CACHE_NAME) {
          console.log('Deleting old cache:', cacheName);
          return caches.delete(cacheName);
        }
      });
      
      await Promise.all(deletePromises);
      
      // Take control of all pages
      await self.clients.claim();
      
      console.log('Service Worker activated successfully');
      
      // Notify all clients about activation
      const clients = await self.clients.matchAll();
      clients.forEach(client => {
        client.postMessage({
          type: 'SW_ACTIVATED',
          basePath: GITHUB_PAGES_BASE,
          offlineRoutes: OFFLINE_ROUTES
        });
      });
    })()
  );
});

// Enhanced fetch handler with robust offline support
self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);
  
  // Skip non-GET requests
  if (request.method !== 'GET') {
    return;
  }
  
  // Skip external requests
  if (url.origin !== location.origin) {
    return;
  }
  
  // Handle navigation requests
  if (request.mode === 'navigate') {
    event.respondWith(handleNavigationRequest(request));
    return;
  }
  
  // Handle asset requests
  event.respondWith(handleAssetRequest(request));
});

// Navigation request handler
async function handleNavigationRequest(request) {
  try {
    // Try network first for navigation
    const response = await fetch(request);
    
    if (response.ok) {
      // Cache successful navigation responses for offline routes
      if (isOfflineAccessible(request.url)) {
        const responseClone = response.clone();
        const offlineCache = await caches.open(OFFLINE_CACHE_NAME);
        await offlineCache.put(request, responseClone);
      }
      return response;
    }
    
    throw new Error('Network response not ok');
    
  } catch (error) {
    console.log('Navigation request failed, trying cache:', request.url);
    
    // Try to get from cache
    const cachedResponse = await caches.match(request);
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
    }
    
    // Return offline notice for non-offline accessible routes
    return new Response(getOfflineNoticeHTML(), {
      status: 200,
      statusText: 'OK',
      headers: new Headers({
        'Content-Type': 'text/html'
      })
    });
  }
}

// Asset request handler
async function handleAssetRequest(request) {
  try {
    // Try cache first for assets (cache-first strategy)
    const cachedResponse = await caches.match(request);
    if (cachedResponse) {
      console.log('Serving from cache:', request.url);
      return cachedResponse;
    }
    
    // Not in cache, fetch from network
    const response = await fetch(request);
    
    if (response.ok) {
      // Determine cache to use
      let cacheName = CACHE_NAME;
      if (request.url.includes('_framework/')) {
        cacheName = RUNTIME_CACHE_NAME;
      }
      
      // Cache the response
      const responseClone = response.clone();
      const cache = await caches.open(cacheName);
      await cache.put(request, responseClone);
      
      return response;
    }
    
    throw new Error('Network response not ok');
    
  } catch (error) {
    console.error('Asset request failed:', request.url, error);
    
    // Try to find in any cache as fallback
    const fallbackResponse = await caches.match(request);
    if (fallbackResponse) {
      return fallbackResponse;
    }
    
    // Return appropriate error response
    return new Response('Asset not available offline', {
      status: 503,
      statusText: 'Service Unavailable'
    });
  }
}

// Helper functions
function isOfflineAccessible(url) {
  const pathname = new URL(url).pathname;
  return OFFLINE_ROUTES.some(route => {
    const routePath = new URL(route, self.location.origin).pathname;
    return pathname === routePath || pathname.startsWith(routePath + '/');
  });
}

function getOfflineNoticeHTML() {
  return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <title>AirCode - Offline</title>
    <base href="${GITHUB_PAGES_BASE}">
    <link href="css/bootstrap/bootstrap.min.css" rel="stylesheet">
    <link href="css/app.css" rel="stylesheet">
</head>
<body>
    <div class="d-flex justify-content-center align-items-center min-vh-100">
        <div class="text-center">
            <div class="mb-4">
                <i class="fas fa-wifi-slash fa-3x text-muted"></i>
            </div>
            <h3>No Internet Connection</h3>
            <p class="text-muted">This page requires an internet connection.</p>
            <div class="mt-4">
                <h5>Available Offline:</h5>
                <div class="list-group">
                    <a href="${GITHUB_PAGES_BASE}" class="list-group-item list-group-item-action">
                        <i class="fas fa-home"></i> Dashboard
                    </a>
                    <a href="${GITHUB_PAGES_BASE}Admin/OfflineAttendanceEven" class="list-group-item list-group-item-action">
                        <i class="fas fa-users"></i> Offline Attendance
                    </a>
                    <a href="${GITHUB_PAGES_BASE}Client/OfflineScan" class="list-group-item list-group-item-action">
                        <i class="fas fa-qrcode"></i> Offline Scan
                    </a>
                </div>
            </div>
            <button class="btn btn-primary mt-4" onclick="window.location.reload()">
                <i class="fas fa-sync-alt"></i> Try Again
            </button>
        </div>
    </div>
</body>
</html>`;
}

// Handle service worker messages
self.addEventListener('message', event => {
  const { type, data } = event.data || {};
  
  switch (type) {
    case 'SKIP_WAITING':
      self.skipWaiting();
      break;
      
    case 'GET_OFFLINE_ROUTES':
      event.ports[0].postMessage({
        type: 'OFFLINE_ROUTES_RESPONSE',
        routes: OFFLINE_ROUTES,
        basePath: GITHUB_PAGES_BASE
      });
      break;
      
    case 'CACHE_ASSETS':
      handleAssetCaching(data.assets || []);
      break;
      
    case 'CLEAR_CACHE':
      handleCacheClear();
      break;
  }
});

// Handle dynamic asset caching
async function handleAssetCaching(assets) {
  try {
    const cache = await caches.open(CACHE_NAME);
    
    const cachePromises = assets.map(async (asset) => {
      if (!discoveredAssets.has(asset)) {
        try {
          const response = await fetch(asset);
          if (response.ok) {
            await cache.put(asset, response);
            discoveredAssets.add(asset);
            console.log('Dynamically cached:', asset);
          }
        } catch (error) {
          console.warn('Failed to cache discovered asset:', asset, error);
        }
      }
    });
    
    await Promise.allSettled(cachePromises);
  } catch (error) {
    console.error('Asset caching failed:', error);
  }
}

// Handle cache clearing
async function handleCacheClear() {
  try {
    const cacheNames = await caches.keys();
    await Promise.all(cacheNames.map(name => caches.delete(name)));
    console.log('All caches cleared');
  } catch (error) {
    console.error('Cache clearing failed:', error);
  }
}

// Background sync for offline actions
self.addEventListener('sync', event => {
  if (event.tag === 'background-sync') {
    event.waitUntil(
      // Handle background sync operations
      Promise.resolve()
    );
  }
});

console.log('AirCode Enhanced PWA Service Worker loaded');
console.log('Base Path:', GITHUB_PAGES_BASE);
console.log('Offline Routes:', OFFLINE_ROUTES);
