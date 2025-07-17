// Blazor WASM PWA Service Worker - GitHub Pages Compatible
// Base path for GitHub Pages deployment
const GITHUB_PAGES_BASE = '/AirCode/';
const CACHE_NAME = 'aircode-cache-v1';

// Critical assets that must be cached for offline functionality
const CRITICAL_ASSETS = [
  GITHUB_PAGES_BASE,
  GITHUB_PAGES_BASE + 'index.html',
  GITHUB_PAGES_BASE + 'manifest.json',
  GITHUB_PAGES_BASE + 'css/app.css',
  GITHUB_PAGES_BASE + 'css/bootstrap/bootstrap.min.css',
  GITHUB_PAGES_BASE + '_framework/blazor.webassembly.js',
  GITHUB_PAGES_BASE + '_framework/blazor.boot.json',
  GITHUB_PAGES_BASE + '_framework/dotnet.wasm',
  GITHUB_PAGES_BASE + '_framework/dotnet.js',
  GITHUB_PAGES_BASE + 'js/site.js',
  GITHUB_PAGES_BASE + 'favicon.ico',
  GITHUB_PAGES_BASE + 'icon-192.png',
  GITHUB_PAGES_BASE + 'icon-512.png'
];

// Install event - cache critical assets and discover Blazor assemblies
self.addEventListener('install', event => {
  console.log('SW: Installing...');
  
  event.waitUntil(
    (async () => {
      const cache = await caches.open(CACHE_NAME);
      
      // Cache critical assets first
      await cache.addAll(CRITICAL_ASSETS);
      console.log('SW: Critical assets cached');
      
      // Discover and cache Blazor assemblies
      try {
        const bootResponse = await fetch(GITHUB_PAGES_BASE + '_framework/blazor.boot.json');
        const bootData = await bootResponse.json();
        
        // Cache the boot.json
        await cache.put(GITHUB_PAGES_BASE + '_framework/blazor.boot.json', bootResponse.clone());
        
        // Cache all assembly resources
        const assemblyUrls = [];
        
        // Runtime assemblies
        if (bootData.resources?.runtime) {
          Object.keys(bootData.resources.runtime).forEach(file => {
            assemblyUrls.push(GITHUB_PAGES_BASE + '_framework/' + file);
          });
        }
        
        // Application assemblies
        if (bootData.resources?.assembly) {
          Object.keys(bootData.resources.assembly).forEach(file => {
            assemblyUrls.push(GITHUB_PAGES_BASE + '_framework/' + file);
          });
        }
        
        // Cache assemblies with error handling
        const cachePromises = assemblyUrls.map(async (url) => {
          try {
            const response = await fetch(url);
            if (response.ok) {
              await cache.put(url, response);
            }
          } catch (error) {
            console.warn('SW: Failed to cache:', url);
          }
        });
        
        await Promise.allSettled(cachePromises);
        console.log('SW: Blazor assemblies cached');
        
      } catch (error) {
        console.error('SW: Failed to cache Blazor assemblies:', error);
      }
      
      self.skipWaiting();
    })()
  );
});

// Activate event
self.addEventListener('activate', event => {
  console.log('SW: Activating...');
  
  event.waitUntil(
    (async () => {
      // Clean up old caches
      const cacheNames = await caches.keys();
      await Promise.all(
        cacheNames.map(cacheName => {
          if (cacheName !== CACHE_NAME) {
            return caches.delete(cacheName);
          }
        })
      );
      
      // Take control of all pages
      await self.clients.claim();
      console.log('SW: Activated and claimed clients');
    })()
  );
});

// Fetch event - handle offline requests
self.addEventListener('fetch', event => {
  const request = event.request;
  
  // Only handle GET requests
  if (request.method !== 'GET') {
    return;
  }
  
  // Skip external requests
  if (!request.url.startsWith(self.location.origin)) {
    return;
  }
  
  event.respondWith(handleRequest(request));
});

async function handleRequest(request) {
  const url = new URL(request.url);
  
  // For navigation requests, always return index.html from cache
  if (request.mode === 'navigate') {
    try {
      // Try network first
      const response = await fetch(request);
      if (response.ok) {
        return response;
      }
    } catch (error) {
      // Network failed, serve from cache
    }
    
    // Serve index.html for all navigation requests (SPA routing)
    const cachedIndex = await caches.match(GITHUB_PAGES_BASE + 'index.html');
    if (cachedIndex) {
      return cachedIndex;
    }
    
    // Fallback offline page
    return new Response(`
      <!DOCTYPE html>
      <html>
      <head>
        <title>AirCode - Offline</title>
        <base href="${GITHUB_PAGES_BASE}" />
      </head>
      <body>
        <div style="text-align: center; margin-top: 50px;">
          <h1>AirCode</h1>
          <p>Loading offline mode...</p>
          <p>If this persists, please check your internet connection and refresh.</p>
        </div>
      </body>
      </html>
    `, {
      headers: { 'Content-Type': 'text/html' }
    });
  }
  
  // For asset requests, try cache first, then network
  try {
    const cachedResponse = await caches.match(request);
    if (cachedResponse) {
      return cachedResponse;
    }
    
    // Not in cache, try network
    const response = await fetch(request);
    
    if (response.ok) {
      // Cache the response for future use
      const cache = await caches.open(CACHE_NAME);
      cache.put(request, response.clone());
      return response;
    }
    
    throw new Error('Network response not ok');
    
  } catch (error) {
    console.error('SW: Request failed:', request.url, error);
    
    // Return a generic error response
    return new Response('Resource not available offline', {
      status: 503,
      statusText: 'Service Unavailable'
    });
  }
}

console.log('SW: Blazor PWA Service Worker loaded');
