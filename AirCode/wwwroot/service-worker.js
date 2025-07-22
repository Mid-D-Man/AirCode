// Blazor WASM PWA Service Worker - GitHub Pages Compatible
const isGitHubPages = self.location.hostname === 'mid-d-man.github.io';
const BASE_PATH = isGitHubPages ? '/AirCode/' : '/';
const CACHE_NAME = 'aircode-cache-v2';

// Essential assets only
const CRITICAL_ASSETS = [
  BASE_PATH,
  BASE_PATH + 'index.html',
  BASE_PATH + 'manifest.json',
  BASE_PATH + 'icon-192.png',
  BASE_PATH + 'icon-512.png',
  BASE_PATH + 'favicon.ico'
];

self.addEventListener('install', event => {
  event.waitUntil(
      (async () => {
        const cache = await caches.open(CACHE_NAME);

        // Cache critical assets with error handling
        for (const asset of CRITICAL_ASSETS) {
          try {
            await cache.add(asset);
          } catch (error) {
            console.warn('Failed to cache:', asset);
          }
        }

        // Dynamic Blazor asset discovery
        try {
          const bootResponse = await fetch(BASE_PATH + '_framework/blazor.boot.json');
          if (bootResponse.ok) {
            const bootData = await bootResponse.json();
            await cache.put(BASE_PATH + '_framework/blazor.boot.json', bootResponse.clone());

            // Cache framework essentials
            const frameworkAssets = [
              '_framework/blazor.webassembly.js',
              '_framework/dotnet.wasm',
              '_framework/dotnet.js'
            ];

            for (const asset of frameworkAssets) {
              try {
                await cache.add(BASE_PATH + asset);
              } catch (error) {
                console.warn('Failed to cache framework asset:', asset);
              }
            }

            // Cache assemblies
            if (bootData.resources?.assembly) {
              for (const file of Object.keys(bootData.resources.assembly)) {
                try {
                  await cache.add(BASE_PATH + '_framework/' + file);
                } catch (error) {
                  console.warn('Failed to cache assembly:', file);
                }
              }
            }
          }
        } catch (error) {
          console.error('Blazor boot.json fetch failed:', error);
        }

        self.skipWaiting();
      })()
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
      (async () => {
        const cacheNames = await caches.keys();
        await Promise.all(
            cacheNames.map(cacheName => {
              if (cacheName !== CACHE_NAME) {
                return caches.delete(cacheName);
              }
            })
        );
        await self.clients.claim();
      })()
  );
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;
  if (!event.request.url.startsWith(self.location.origin)) return;

  event.respondWith(handleRequest(event.request));
});

async function handleRequest(request) {
  // Navigation requests - serve index.html
  if (request.mode === 'navigate') {
    try {
      const response = await fetch(request);
      if (response.ok) return response;
    } catch {}

    const cachedIndex = await caches.match(BASE_PATH + 'index.html');
    return cachedIndex || createOfflinePage();
  }

  // Asset requests - cache first
  const cachedResponse = await caches.match(request);
  if (cachedResponse) return cachedResponse;

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_NAME);
      cache.put(request, response.clone());
    }
    return response;
  } catch {
    return new Response('Offline', { status: 503 });
  }
}

function createOfflinePage() {
  return new Response(`
    <!DOCTYPE html>
    <html>
    <head>
      <title>AirCode - Offline</title>
      <base href="${BASE_PATH}" />
      <style>
        body { font-family: Arial, sans-serif; text-align: center; margin-top: 50px; }
        .retry { background: #007bff; color: white; border: none; padding: 10px 20px; border-radius: 5px; cursor: pointer; }
      </style>
    </head>
    <body>
      <h1>AirCode - Offline Mode</h1>
      <p>App is loading in offline mode...</p>
      <button class="retry" onclick="location.reload()">Retry</button>
      <script>
        setTimeout(() => location.reload(), 3000);
      </script>
    </body>
    </html>
  `, { headers: { 'Content-Type': 'text/html' } });
}