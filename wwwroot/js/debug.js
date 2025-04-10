// Enhanced debug script with comprehensive storage management
console.log('Enhanced debug script loaded');

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

    // Add diagnostics panel after a short delay
    setTimeout(addDiagnosticsPanel, 1000);

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

// Enhanced diagnostics panel with storage management
function addDiagnosticsPanel() {
    if (!document.getElementById('debug-panel')) {
        const panel = document.createElement('div');
        panel.id = 'debug-panel';
        panel.style.cssText = 'position:fixed; bottom:10px; right:10px; background:#f0f0f0; padding:8px; ' +
            'border:1px solid #ccc; z-index:10000; font-size:12px; max-width:350px; ' +
            'max-height:80vh; overflow:auto; opacity:0.9; border-radius:5px; box-shadow:0 0 10px rgba(0,0,0,0.2);';

        const header = document.createElement('div');
        header.textContent = 'AirCode Diagnostics';
        header.style.cssText = 'font-weight:bold; cursor:pointer; padding-bottom:5px; ' +
            'border-bottom:1px solid #ccc; margin-bottom:8px; color:#333; font-size:14px;';

        const content = document.createElement('div');
        content.id = 'debug-content';

        // Toggle button
        const toggleButton = document.createElement('button');
        toggleButton.textContent = '▼';
        toggleButton.style.cssText = 'float:right; cursor:pointer; background:none; border:none; font-size:14px;';
        header.appendChild(toggleButton);

        toggleButton.addEventListener('click', () => {
            if (content.style.display === 'none') {
                content.style.display = 'block';
                toggleButton.textContent = '▼';
            } else {
                content.style.display = 'none';
                toggleButton.textContent = '►';
            }
        });

        panel.appendChild(header);
        panel.appendChild(content);

        // Storage Management Section
        const storageSection = document.createElement('div');
        storageSection.style.cssText = 'margin-top:10px; padding-top:5px; border-top:1px solid #ccc;';

        const storageTitle = document.createElement('div');
        storageTitle.textContent = 'Storage Management';
        storageTitle.style.cssText = 'font-weight:bold; margin-bottom:5px;';
        storageSection.appendChild(storageTitle);

        // 1. Button to clear ALL localStorage
        const clearAllButton = document.createElement('button');
        clearAllButton.textContent = 'Clear ALL localStorage';
        clearAllButton.style.cssText = 'margin:3px 0; padding:3px 8px; cursor:pointer; display:block; width:100%; background:#ff4d4d; color:white; border:none; border-radius:3px;';
        clearAllButton.addEventListener('click', () => {
            if (confirm('Are you sure you want to clear ALL localStorage? This will affect all sites on this domain.')) {
                localStorage.clear();
                console.log('🗑️ All localStorage cleared');
                alert('All localStorage cleared');

                updateStorageInfo();
            }
        });
        storageSection.appendChild(clearAllButton);

        // 2. Button to clear only AirCode_ prefixed items
        const clearAppButton = document.createElement('button');
        clearAppButton.textContent = 'Clear AirCode_ Storage Only';
        clearAppButton.style.cssText = 'margin:3px 0; padding:3px 8px; cursor:pointer; display:block; width:100%; background:#ff9900; color:white; border:none; border-radius:3px;';
        clearAppButton.addEventListener('click', () => {
            const prefix = 'AirCode_';

            // Get all keys that start with our prefix
            const keysToRemove = [];
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key && key.startsWith(prefix)) {
                    keysToRemove.push(key);
                }
            }

            // Remove the keys
            keysToRemove.forEach(key => {
                localStorage.removeItem(key);
            });

            console.log(`🗑️ ${keysToRemove.length} AirCode storage items cleared`);
            alert(`${keysToRemove.length} AirCode storage items cleared`);

            updateStorageInfo();
        });
        storageSection.appendChild(clearAppButton);

        // 3. Specific key deletion with input field
        const keyInputGroup = document.createElement('div');
        keyInputGroup.style.cssText = 'display:flex; margin:5px 0; gap:5px;';

        const keyInput = document.createElement('input');
        keyInput.type = 'text';
        keyInput.placeholder = 'Enter storage key';
        keyInput.style.cssText = 'flex:1; padding:3px 5px; border:1px solid #ccc; border-radius:3px;';

        const deleteKeyButton = document.createElement('button');
        deleteKeyButton.textContent = 'Delete';
        deleteKeyButton.style.cssText = 'padding:3px 8px; cursor:pointer; background:#dc3545; color:white; border:none; border-radius:3px;';
        deleteKeyButton.addEventListener('click', () => {
            const key = keyInput.value.trim();
            if (!key) {
                alert('Please enter a key');
                return;
            }

            // Check if the key exists
            let fullKey = key;
            if (!fullKey.startsWith('AirCode_')) {
                fullKey = 'AirCode_' + key;
            }

            if (localStorage.getItem(fullKey)) {
                localStorage.removeItem(fullKey);
                console.log(`🗑️ Removed item with key: ${fullKey}`);
                alert(`Removed item with key: ${fullKey}`);
                keyInput.value = '';
                updateStorageInfo();
            } else {
                alert(`Key not found: ${fullKey}`);
            }
        });

        keyInputGroup.appendChild(keyInput);
        keyInputGroup.appendChild(deleteKeyButton);
        storageSection.appendChild(keyInputGroup);

        // 4. Firestore data deletion (if available)
        const clearFirestoreButton = document.createElement('button');
        clearFirestoreButton.textContent = 'Clear Firestore Cache';
        clearFirestoreButton.style.cssText = 'margin:3px 0; padding:3px 8px; cursor:pointer; display:block; width:100%; background:#5f9ea0; color:white; border:none; border-radius:3px;';
        clearFirestoreButton.addEventListener('click', () => {
            if (window.firebase && window.firebase.firestore) {
                window.firebase.firestore().clearPersistence()
                    .then(() => {
                        console.log('🗑️ Firestore persistence cleared');
                        alert('Firestore cache cleared successfully');
                    })
                    .catch(err => {
                        console.error('Failed to clear Firestore cache:', err);
                        alert(`Failed to clear Firestore cache: ${err.message}`);
                    });
            } else {
                alert('Firestore is not available or not initialized');
            }
        });
        storageSection.appendChild(clearFirestoreButton);

        // Storage info display
        const storageInfo = document.createElement('div');
        storageInfo.id = 'storage-info';
        storageInfo.style.cssText = 'margin-top:5px; font-size:11px; color:#666;';
        storageSection.appendChild(storageInfo);

        // Function to update storage info
        function updateStorageInfo() {
            let totalItems = localStorage.length;
            let appItems = 0;
            let appSize = 0;

            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key && key.startsWith('AirCode_')) {
                    appItems++;
                    appSize += localStorage.getItem(key).length * 2; // UTF-16 uses 2 bytes per character
                }
            }

            storageInfo.innerHTML = `
                <div>Total localStorage items: ${totalItems}</div>
                <div>AirCode_ items: ${appItems}</div>
                <div>AirCode_ size: ${(appSize / 1024).toFixed(2)} KB</div>
                <div>Last updated: ${new Date().toLocaleTimeString()}</div>
            `;
        }

        // Call initially
        updateStorageInfo();

        // Storage keys list button
        const toggleKeysButton = document.createElement('button');
        toggleKeysButton.textContent = 'Show Storage Keys';
        toggleKeysButton.style.cssText = 'margin:5px 0; padding:3px 8px; cursor:pointer; width:100%; background:#6c757d; color:white; border:none; border-radius:3px;';

        const keysList = document.createElement('div');
        keysList.id = 'keys-list';
        keysList.style.cssText = 'margin-top:5px; display:none; max-height:150px; overflow-y:auto; border:1px solid #ddd; padding:3px; font-size:10px;';

        toggleKeysButton.addEventListener('click', () => {
            if (keysList.style.display === 'none') {
                // Populate keys list
                keysList.innerHTML = '';
                const keys = [];
                for (let i = 0; i < localStorage.length; i++) {
                    const key = localStorage.key(i);
                    if (key) keys.push(key);
                }

                keys.sort().forEach(key => {
                    const keyItem = document.createElement('div');
                    keyItem.style.cssText = 'padding:2px; border-bottom:1px solid #eee; word-break:break-all;';

                    if (key.startsWith('AirCode_')) {
                        keyItem.style.color = '#28a745';
                    }

                    keyItem.textContent = key;
                    keyItem.title = 'Click to copy key to input field';
                    keyItem.style.cursor = 'pointer';

                    keyItem.addEventListener('click', () => {
                        keyInput.value = key;
                    });

                    keysList.appendChild(keyItem);
                });

                keysList.style.display = 'block';
                toggleKeysButton.textContent = 'Hide Storage Keys';
            } else {
                keysList.style.display = 'none';
                toggleKeysButton.textContent = 'Show Storage Keys';
            }
        });

        storageSection.appendChild(toggleKeysButton);
        storageSection.appendChild(keysList);

        // Add storage section to content
        content.appendChild(storageSection);

        // Add initial diagnostics section
        const diagnosticsSection = document.createElement('div');
        diagnosticsSection.id = 'system-diagnostics';
        diagnosticsSection.style.cssText = 'margin-top:10px;';
        content.insertBefore(diagnosticsSection, storageSection);

        // Add the panel to the document
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
    const diagnosticsSection = document.getElementById('system-diagnostics');
    if (diagnosticsSection) {
        diagnosticsSection.innerHTML = '';

        const title = document.createElement('div');
        title.textContent = 'System Diagnostics';
        title.style.cssText = 'font-weight:bold; margin-bottom:5px;';
        diagnosticsSection.appendChild(title);

        Object.entries(info).forEach(([key, value]) => {
            const line = document.createElement('div');
            line.style.cssText = 'padding:2px 0; font-size:11px;';

            // Format the key for display
            const formattedKey = key.replace(/([A-Z])/g, ' $1')
                .replace(/^./, str => str.toUpperCase());

            line.innerHTML = `<strong>${formattedKey}:</strong> ${value}`;
            diagnosticsSection.appendChild(line);
        });
    }
};