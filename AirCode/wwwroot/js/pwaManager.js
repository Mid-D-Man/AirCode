// PWA Manager for AirCode - Service Worker Registration and Management
(function() {
    'use strict';

    let deferredPrompt;
    let waitingServiceWorker = null;
    let blazorComponent = null;
    let isInitialized = false;

    // Service Worker Registration with enhanced error handling
    function registerServiceWorker() {
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('./service-worker.js', { scope: './' })
                .then(registration => {
                    console.log('AirCode SW registered: ', registration);

                    // FIXED: Enhanced update detection
                    registration.addEventListener('updatefound', () => {
                        const newWorker = registration.installing;
                        if (newWorker) {
                            console.log('AirCode: New service worker installing');
                            newWorker.addEventListener('statechange', () => {
                                console.log('AirCode: Service worker state changed to:', newWorker.state);
                                if (newWorker.state === 'installed') {
                                    if (navigator.serviceWorker.controller) {
                                        // New version available
                                        waitingServiceWorker = newWorker;
                                        console.log('AirCode: Update available');
                                        notifyUpdateAvailable();
                                    } else {
                                        // First install
                                        console.log('AirCode: Service worker installed for first time');
                                    }
                                }
                            });
                        }
                    });

                    // FIXED: Check for existing updates
                    if (registration.waiting) {
                        waitingServiceWorker = registration.waiting;
                        notifyUpdateAvailable();
                    }

                    // FIXED: Periodic update check
                    setInterval(() => {
                        registration.update();
                    }, 60000); // Check every minute
                })
                .catch(registrationError => {
                    console.error('AirCode SW registration failed: ', registrationError);
                });

            // Enhanced message handling
            navigator.serviceWorker.addEventListener('message', event => {
                console.log('AirCode: Received message from SW:', event.data);
                if (event.data && event.data.type === 'NEW_VERSION_AVAILABLE') {
                    notifyUpdateAvailable();
                }
            });

            // FIXED: Handle controller changes
            navigator.serviceWorker.addEventListener('controllerchange', () => {
                console.log('AirCode: Service worker controller changed');
                if (!window.location.href.includes('reloading')) {
                    window.location.reload();
                }
            });
        }
    }

    // PWA Install Prompt Handling
    function setupInstallPrompt() {
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('AirCode: Install prompt available');
            e.preventDefault();
            deferredPrompt = e;
            notifyInstallReady();
        });

        window.addEventListener('appinstalled', () => {
            console.log('AirCode PWA installed');
            deferredPrompt = null;
            notifyAppInstalled();
        });
    }

    // FIXED: Enhanced connectivity monitoring with proper disposal handling
    function setupConnectivityMonitoring(dotNetRef) {
        // Dispose previous reference if exists
        if (blazorComponent && blazorComponent !== dotNetRef) {
            try {
                blazorComponent.dispose();
            } catch (error) {
                console.warn('AirCode: Failed to dispose previous Blazor reference:', error);
            }
        }

        blazorComponent = dotNetRef;

        const updateConnectivity = () => {
            if (blazorComponent) {
                try {
                    blazorComponent.invokeMethodAsync('OnConnectivityChanged', navigator.onLine);
                } catch (error) {
                    console.warn('AirCode: Failed to notify connectivity change:', error);
                    // If the component is disposed, clear the reference
                    if (error.message.includes('disposed')) {
                        blazorComponent = null;
                    }
                }
            }
        };

        // Remove existing listeners to prevent duplicates
        window.removeEventListener('online', updateConnectivity);
        window.removeEventListener('offline', updateConnectivity);

        window.addEventListener('online', updateConnectivity);
        window.addEventListener('offline', updateConnectivity);

        // Initial check with delay to ensure component is ready
        setTimeout(updateConnectivity, 500);
    }

    // FIXED: Enhanced update handling with error recovery
    function applyUpdate() {
        console.log('AirCode: Applying update');

        if (waitingServiceWorker) {
            try {
                waitingServiceWorker.postMessage({ type: 'SKIP_WAITING' });
                waitingServiceWorker = null;
            } catch (error) {
                console.error('AirCode: Failed to message waiting service worker:', error);
            }
        }

        // Force reload with cache busting
        setTimeout(() => {
            const url = new URL(window.location.href);
            url.searchParams.set('reloading', 'true');
            window.location.href = url.toString();
        }, 100);
    }

    // Install PWA with better error handling
    async function installPWA() {
        if (!deferredPrompt) {
            console.log('AirCode: No install prompt available');
            return false;
        }

        try {
            await deferredPrompt.prompt();
            const { outcome } = await deferredPrompt.userChoice;
            console.log(`AirCode install prompt result: ${outcome}`);
            deferredPrompt = null;
            return outcome === 'accepted';
        } catch (error) {
            console.error('AirCode PWA installation failed:', error);
            return false;
        }
    }

    // Status check functions
    function isInstalled() {
        return window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone === true ||
            document.referrer.includes('android-app://');
    }

    function canInstall() {
        return !!deferredPrompt;
    }

    function hasUpdate() {
        return !!waitingServiceWorker;
    }

    // FIXED: Enhanced notification functions with better error handling
    function notifyInstallReady() {
        console.log('AirCode: Notifying install ready');
        if (blazorComponent) {
            try {
                blazorComponent.invokeMethodAsync('OnInstallPromptReady');
            } catch (error) {
                console.warn('AirCode: Failed to notify install ready:', error);
                if (error.message.includes('disposed')) {
                    blazorComponent = null;
                }
            }
        }
        window.dispatchEvent(new CustomEvent('pwa-install-ready'));
    }

    function notifyUpdateAvailable() {
        console.log('AirCode: Notifying update available');
        if (blazorComponent) {
            try {
                blazorComponent.invokeMethodAsync('OnUpdateAvailable');
            } catch (error) {
                console.warn('AirCode: Failed to notify update available:', error);
                if (error.message.includes('disposed')) {
                    blazorComponent = null;
                }
            }
        }
        window.dispatchEvent(new CustomEvent('pwa-update-available'));
    }

    function notifyAppInstalled() {
        console.log('AirCode: Notifying app installed');
        if (blazorComponent) {
            try {
                blazorComponent.invokeMethodAsync('OnAppInstalled');
            } catch (error) {
                console.warn('AirCode: Failed to notify app installed:', error);
                if (error.message.includes('disposed')) {
                    blazorComponent = null;
                }
            }
        }
        window.dispatchEvent(new CustomEvent('pwa-app-installed'));
    }

    // FIXED: Enhanced cleanup function
    function cleanup() {
        if (blazorComponent) {
            try {
                blazorComponent.dispose();
            } catch (error) {
                console.warn('AirCode: Error disposing Blazor component:', error);
            }
            blazorComponent = null;
        }
    }
async function requestPersistentStorage() {
    if ('storage' in navigator && 'persist' in navigator.storage) {
        try {
            const isPersisted = await navigator.storage.persisted();
            console.log(`AirCode: Persisted storage granted: ${isPersisted}`);
            
            if (!isPersisted) {
                const result = await navigator.storage.persist();
                console.log(`AirCode: Persistence request result: ${result}`);
                
                if (result) {
                    // Notify user that offline mode is fully enabled
                    console.log('AirCode: Persistent storage granted - offline mode secured');
                } else {
                    console.warn('AirCode: Persistent storage denied - app may lose offline capability after inactivity');
                }
            }
        } catch (error) {
            console.error('AirCode: Error requesting persistent storage:', error);
        }
    }
}

    // Initialize PWA Manager
    function initializePWAManager() {
        if (isInitialized) {
            console.log('AirCode: PWA Manager already initialized');
            return;
        }

        console.log('AirCode: Initializing PWA Manager');

        // Create the global PWA object for Blazor interop
        window.AirCodePWA = {
            install: installPWA,
            applyUpdate: applyUpdate,
            isInstalled: isInstalled,
            canInstall: canInstall,
            hasUpdate: hasUpdate,
            cleanup: cleanup
        };

        // Setup connectivity monitoring function for Blazor
        window.setupConnectivityMonitoring = setupConnectivityMonitoring;

        // Register service worker and setup install prompt
        registerServiceWorker();
        setupInstallPrompt();

// Request persistent storage requestPersistentStorage();

        // FIXED: Add cleanup on page unload
        window.addEventListener('beforeunload', cleanup);

        isInitialized = true;
        console.log('AirCode PWA Manager initialized');

        // Dispatch event to signal initialization complete
        window.dispatchEvent(new CustomEvent('pwa-manager-ready'));
    }

    // Initialize immediately if DOM is ready, otherwise wait
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializePWAManager);
    } else {
        // Use setTimeout to ensure this runs after other scripts
        setTimeout(initializePWAManager, 0);
    }

    // Also expose initialization function for manual triggering if needed
    window.initializeAirCodePWA = initializePWAManager;
})();
