// Enhanced debug script for troubleshooting Blazor WASM deployment issues
console.log('Debug script loaded');

// Global error handler
window.addEventListener('error', function(e) {
    if (e && e.target && (e.target.tagName === 'LINK' || e.target.tagName === 'SCRIPT' || e.target.tagName === 'IMG')) {
        console.error('Resource failed to load:', e.target.src || e.target.href);

        // Try to detect pattern in failing URLs
        const failedUrl = e.target.src || e.target.href;
        if (failedUrl) {
            console.log('Analyzing failed URL:', failedUrl);

            // Check if it's a relative path issue
            if (failedUrl.includes('//')) {
                const url = new URL(failedUrl);
                console.log('URL analysis:', {
                    origin: url.origin,
                    pathname: url.pathname,
                    baseHref: document.querySelector('base')?.href
                });
            }
        }
    }
});

// Improved path analysis when page loads
window.addEventListener('DOMContentLoaded', function() {
    console.log('Current base href:', document.querySelector('base')?.href);
    console.log('Current path:', window.location.pathname);
    console.log('Document URL:', document.URL);
    console.log('Service worker scope:', navigator.serviceWorker?.controller?.scriptURL || 'No active service worker');

    // Simple diagnostics panel
    function addDiagnosticsPanel() {
        if (!document.getElementById('debug-panel')) {
            const panel = document.createElement('div');
            panel.id = 'debug-panel';
            panel.style.cssText = 'position:fixed; bottom:10px; right:10px; background:#f0f0f0; padding:5px; ' +
                'border:1px solid #ccc; z-index:10000; font-size:12px; max-width:300px; ' +
                'max-height:200px; overflow:auto; opacity:0.8;';

            const header = document.createElement('div');
            header.textContent = 'Diagnostics (click to toggle)';
            header.style.cssText = 'font-weight:bold; cursor:pointer; padding-bottom:5px;';

            const content = document.createElement('div');
            content.id = 'debug-content';
            content.style.display = 'none';

            header.addEventListener('click', () => {
                content.style.display = content.style.display === 'none' ? 'block' : 'none';
            });

            panel.appendChild(header);
            panel.appendChild(content);
            document.body.appendChild(panel);

            // Add initial diagnostics
            updateDiagnostics({
                baseHref: document.querySelector('base')?.href,
                path: window.location.pathname,
                blazorLoaded: !!window.Blazor,
                serviceWorker: !!navigator.serviceWorker.controller,
                online: navigator.onLine
            });
        }
    }

    // Update diagnostics panel with new info
    window.updateDiagnostics = function(info) {
        const content = document.getElementById('debug-content');
        if (content) {
            content.innerHTML = '';

            Object.entries(info).forEach(([key, value]) => {
                const line = document.createElement('div');
                line.style.cssText = 'padding:2px 0;';
                line.textContent = `${key}: ${value}`;
                content.appendChild(line);
            });
        }
    }

    // Add diagnostics panel after a short delay
    setTimeout(addDiagnosticsPanel, 2000);

    // Check if critical files are accessible with retry logic
    const criticalFiles = [
        '_framework/blazor.webassembly.js',
        'manifest.json',
        'css/app.css',
        'favicon.png',
        'service-worker.js'
    ];

    criticalFiles.forEach(checkFile);

    function checkFile(file) {
        // Try fetching the file with different path combinations
        const basePaths = [
            '', // Use base href
            '/',
            document.baseURI,
            '/AirCode/',
            './'
        ];

        let fileFound = false;

        basePaths.forEach((basePath, index) => {
            // Add small delay between requests
            setTimeout(() => {
                if (!fileFound) {
                    const url = basePath + file;
                    const timestamp = new Date().getTime(); // Cache-busting

                    fetch(`${url}?t=${timestamp}`)
                        .then(response => {
                            if (response.ok && !fileFound) {
                                fileFound = true;
                                console.log(`✅ Found at path: ${url}`);

                                // If this is not the direct path (index 0), log it as a path issue
                                if (index > 0) {
                                    console.warn(`Path issue detected: ${file} works at ${url} but not at base path`);
                                }
                            }
                        })
                        .catch(() => {});
                }
            }, index * 100);
        });
    }

    // Monitor Blazor loading state
    let blazorCheckInterval = setInterval(() => {
        if (window.Blazor) {
            console.log('✅ Blazor loaded successfully');
            clearInterval(blazorCheckInterval);

            // Update diagnostics if panel exists
            if (document.getElementById('debug-content')) {
                window.updateDiagnostics({
                    blazorLoaded: true,
                    loadTime: `${(performance.now() / 1000).toFixed(2)}s`
                });
            }
        }
    }, 500);

    // Check service worker registration
    if (navigator.serviceWorker) {
        navigator.serviceWorker.getRegistrations().then(registrations => {
            console.log('Service Worker registrations:', registrations.length);
            registrations.forEach(reg => {
                console.log('- Scope:', reg.scope);
                console.log('- Active:', !!reg.active);
            });
        });
    }
});