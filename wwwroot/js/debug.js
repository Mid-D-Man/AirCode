// Enhanced debug script for troubleshooting Blazor WASM deployment issues
console.log('Debug script loaded');

// Log all network errors
window.addEventListener('error', function(e) {
    if (e && e.target && (e.target.tagName === 'LINK' || e.target.tagName === 'SCRIPT' || e.target.tagName === 'IMG')) {
        console.error('Resource failed to load:', e.target.src || e.target.href);
    }
});

// Check paths when page loads
window.addEventListener('DOMContentLoaded', function() {
    console.log('Current base href:', document.querySelector('base')?.href);
    console.log('Current path:', window.location.pathname);
    console.log('Document URL:', document.URL);
    console.log('Service worker scope:', navigator.serviceWorker?.controller?.scriptURL || 'No active service worker');

    // Get the base path from base href
    const baseHref = document.querySelector('base')?.href || '/';

    // Check if critical files are accessible
    const criticalFiles = [
        '_framework/blazor.webassembly.js',
        'manifest.json',
        'css/app.css',
        'favicon.png',
        'service-worker.js'
    ];

    criticalFiles.forEach(file => {
        const img = new Image();
        const timestamp = new Date().getTime(); // Cache-busting
        img.onload = () => console.log(`✅ File accessible: ${file}`);
        img.onerror = () => {
            console.error(`❌ File NOT accessible: ${file}`);
            // Try with different path combinations
            console.log(`Trying alternate paths for: ${file}`);

            // Try with and without base path
            const alternativePaths = [
                `/${file}`,
                `/AirCode/${file}`,
                `../${file}`,
                `./${file}`
            ];

            alternativePaths.forEach(path => {
                fetch(`${path}?t=${timestamp}`)
                    .then(response => {
                        if (response.ok) {
                            console.log(`✅ Found at alternate path: ${path}`);
                        }
                    })
                    .catch(() => {});
            });
        };
        img.src = `${file}?t=${timestamp}`;
    });

    // List all loaded scripts
    console.log('Loaded scripts:');
    document.querySelectorAll('script').forEach(script => {
        console.log(`- ${script.src || 'Inline script'}`);
    });
});

// Add connectivity checker if it doesn't exist
window.connectivityChecker = window.connectivityChecker || {
    getOnlineStatus: function() {
        return navigator.onLine;
    },
    init: function(dotNetRef) {
        console.log('Connectivity checker initialized');
        return true;
    }
};

// Add offline manager if it doesn't exist
window.offlineManager = window.offlineManager || {
    showOfflinePrompt: function() {
        console.log('Offline prompt would show here');
        return Promise.resolve(true);
    },
    hasStoredCredentials: function() {
        return Promise.resolve(false);
    },
    showNoCredentialsMessage: function() {
        console.log('No credentials message would show here');
        return Promise.resolve();
    },
    showOnlinePrompt: function() {
        console.log('Online prompt would show here');
        return Promise.resolve(true);
    }
};

// Add credential manager if it doesn't exist
window.credentialManager = window.credentialManager || {
    storeCredentials: function(username, password, isAdmin, adminId) {
        console.log('Credentials would be stored here');
        return Promise.resolve();
    }
};