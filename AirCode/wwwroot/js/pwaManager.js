// PWA Manager for AirCode - Service Worker Registration and Management
(function() {
    'use strict';

    let deferredPrompt;
    let waitingServiceWorker = null;
    let blazorComponent = null;

    // Service Worker Registration
    function registerServiceWorker() {
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('./service-worker.js', { scope: './' })
                .then(registration => {
                    console.log('AirCode SW registered: ', registration);

                    registration.addEventListener('updatefound', () => {
                        const newWorker = registration.installing;
                        newWorker.addEventListener('statechange', () => {
                            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                                waitingServiceWorker = newWorker;
                                notifyUpdateAvailable();
                            }
                        });
                    });
                })
                .catch(registrationError => {
                    console.log('AirCode SW registration failed: ', registrationError);
                });

            navigator.serviceWorker.addEventListener('message', event => {
                if (event.data && event.data.type === 'NEW_VERSION_AVAILABLE') {
                    notifyUpdateAvailable();
                }
            });

            navigator.serviceWorker.addEventListener('controllerchange', () => {
                window.location.reload();
            });
        }
    }

    // PWA Install Prompt Handling
    function setupInstallPrompt() {
        window.addEventListener('beforeinstallprompt', (e) => {
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

    // Connectivity Monitoring
    function setupConnectivityMonitoring(dotNetRef) {
        blazorComponent = dotNetRef;

        const updateConnectivity = () => {
            if (blazorComponent) {
                blazorComponent.invokeMethodAsync('OnConnectivityChanged', navigator.onLine);
            }
        };

        window.addEventListener('online', updateConnectivity);
        window.addEventListener('offline', updateConnectivity);

        // Initial check
        updateConnectivity();
    }

    // Update handling
    function applyUpdate() {
        if (waitingServiceWorker) {
            waitingServiceWorker.postMessage({ type: 'SKIP_WAITING' });
            waitingServiceWorker = null;
        }
        setTimeout(() => window.location.reload(), 100);
    }

    // Install PWA
    async function installPWA() {
        if (!deferredPrompt) {
            return false;
        }

        try {
            deferredPrompt.prompt();
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
            window.navigator.standalone === true;
    }

    function canInstall() {
        return !!deferredPrompt;
    }

    function hasUpdate() {
        return !!waitingServiceWorker;
    }

    // Notification functions
    function notifyInstallReady() {
        if (blazorComponent) {
            blazorComponent.invokeMethodAsync('OnInstallPromptReady');
        }
        window.dispatchEvent(new CustomEvent('pwa-install-ready'));
    }

    function notifyUpdateAvailable() {
        if (blazorComponent) {
            blazorComponent.invokeMethodAsync('OnUpdateAvailable');
        }
        window.dispatchEvent(new CustomEvent('pwa-update-available'));
    }

    function notifyAppInstalled() {
        if (blazorComponent) {
            blazorComponent.invokeMethodAsync('OnAppInstalled');
        }
        window.dispatchEvent(new CustomEvent('pwa-app-installed'));
    }

    // Global PWA object for Blazor interop
    window.AirCodePWA = {
        install: installPWA,
        applyUpdate: applyUpdate,
        isInstalled: isInstalled,
        canInstall: canInstall,
        hasUpdate: hasUpdate
    };

    // Setup connectivity monitoring function for Blazor
    window.setupConnectivityMonitoring = setupConnectivityMonitoring;

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            registerServiceWorker();
            setupInstallPrompt();
        });
    } else {
        registerServiceWorker();
        setupInstallPrompt();
    }
})();