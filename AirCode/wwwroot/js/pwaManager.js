// PWA Configuration and Update Management
// Add this to wwwroot/js/pwaManager.js

class PWAManager {
    constructor() {
        this.config = {
            enabled: true, // Toggle for production
            updateCheckInterval: 60000, // 1 minute in dev, 24h in prod
            showUpdateNotifications: true,
            autoUpdate: false // Manual updates for better UX
        };

        this.serviceWorker = null;
        this.updateAvailable = false;
        this.callbacks = {
            onUpdateAvailable: [],
            onUpdateInstalled: [],
            onOfflineReady: []
        };

        this.init();
    }

    async init() {
        if (!('serviceWorker' in navigator) || !this.config.enabled) {
            console.log('[PWA] Service Worker not supported or disabled');
            return;
        }

        try {
            // Register service worker
            const registration = await navigator.serviceWorker.register('/service-worker.js');
            this.serviceWorker = registration;

            console.log('[PWA] Service Worker registered successfully');

            // Set up event listeners
            this.setupEventListeners(registration);

            // Check for updates periodically
            if (this.config.updateCheckInterval > 0) {
                setInterval(() => this.checkForUpdates(), this.config.updateCheckInterval);
            }

            // Initial update check
            setTimeout(() => this.checkForUpdates(), 5000);

        } catch (error) {
            console.error('[PWA] Service Worker registration failed:', error);
        }
    }

    setupEventListeners(registration) {
        // Listen for updates
        registration.addEventListener('updatefound', () => {
            const newWorker = registration.installing;
            console.log('[PWA] New service worker found');

            newWorker.addEventListener('statechange', () => {
                if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                    console.log('[PWA] New service worker installed, update available');
                    this.updateAvailable = true;
                    this.notifyCallbacks('onUpdateAvailable', {
                        registration,
                        newWorker
                    });
                }
            });
        });

        // Listen for controlling service worker changes
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            console.log('[PWA] Service worker controller changed');
            this.notifyCallbacks('onUpdateInstalled');

            if (this.config.autoUpdate) {
                window.location.reload();
            }
        });

        // Listen for messages from service worker
        navigator.serviceWorker.addEventListener('message', (event) => {
            const { type, version } = event.data || {};

            switch (type) {
                case 'SW_UPDATED':
                    console.log(`[PWA] Service worker updated to version ${version}`);
                    break;
                case 'OFFLINE_READY':
                    this.notifyCallbacks('onOfflineReady');
                    break;
            }
        });
    }

    async checkForUpdates() {
        if (!this.serviceWorker) return;

        try {
            await this.serviceWorker.update();
            console.log('[PWA] Update check completed');
        } catch (error) {
            console.warn('[PWA] Update check failed:', error);
        }
    }

    async applyUpdate() {
        if (!this.updateAvailable || !this.serviceWorker) {
            console.warn('[PWA] No update available or service worker not ready');
            return false;
        }

        try {
            // Tell the new service worker to skip waiting
            const newWorker = this.serviceWorker.waiting || this.serviceWorker.installing;
            if (newWorker) {
                newWorker.postMessage({ type: 'SKIP_WAITING' });
                return true;
            }
        } catch (error) {
            console.error('[PWA] Failed to apply update:', error);
        }

        return false;
    }

    async clearCache() {
        if (!navigator.serviceWorker.controller) {
            console.warn('[PWA] No active service worker to clear cache');
            return false;
        }

        try {
            const messageChannel = new MessageChannel();

            return new Promise((resolve) => {
                messageChannel.port1.onmessage = (event) => {
                    resolve(event.data.success === true);
                };

                navigator.serviceWorker.controller.postMessage(
                    { type: 'CLEAR_CACHE', payload: { confirm: true } },
                    [messageChannel.port2]
                );
            });
        } catch (error) {
            console.error('[PWA] Failed to clear cache:', error);
            return false;
        }
    }

    async getServiceWorkerInfo() {
        if (!navigator.serviceWorker.controller) {
            return { enabled: false, version: null };
        }

        try {
            const messageChannel = new MessageChannel();

            return new Promise((resolve) => {
                messageChannel.port1.onmessage = (event) => {
                    resolve(event.data);
                };

                navigator.serviceWorker.controller.postMessage(
                    { type: 'GET_VERSION' },
                    [messageChannel.port2]
                );

                // Timeout after 5 seconds
                setTimeout(() => resolve({ enabled: false, version: null }), 5000);
            });
        } catch (error) {
            console.error('[PWA] Failed to get service worker info:', error);
            return { enabled: false, version: null };
        }
    }

    // Event subscription methods
    onUpdateAvailable(callback) {
        this.callbacks.onUpdateAvailable.push(callback);
    }

    onUpdateInstalled(callback) {
        this.callbacks.onUpdateInstalled.push(callback);
    }

    onOfflineReady(callback) {
        this.callbacks.onOfflineReady.push(callback);
    }

    notifyCallbacks(event, data = null) {
        this.callbacks[event].forEach(callback => {
            try {
                callback(data);
            } catch (error) {
                console.error(`[PWA] Callback error for ${event}:`, error);
            }
        });
    }

    // Configuration methods
    enablePWA() {
        this.config.enabled = true;
        console.log('[PWA] PWA features enabled');
    }

    disablePWA() {
        this.config.enabled = false;
        console.log('[PWA] PWA features disabled');
    }

    isPWAEnabled() {
        return this.config.enabled;
    }

    // Installation prompt handling
    setupInstallPrompt() {
        let deferredPrompt = null;

        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('[PWA] Install prompt available');
            e.preventDefault();
            deferredPrompt = e;

            // Show custom install button
            this.notifyCallbacks('onInstallAvailable', { prompt: deferredPrompt });
        });

        return {
            showInstallPrompt: async () => {
                if (!deferredPrompt) {
                    console.warn('[PWA] Install prompt not available');
                    return false;
                }

                try {
                    const result = await deferredPrompt.prompt();
                    console.log('[PWA] Install prompt result:', result.outcome);
                    deferredPrompt = null;
                    return result.outcome === 'accepted';
                } catch (error) {
                    console.error('[PWA] Install prompt failed:', error);
                    return false;
                }
            }
        };
    }
}

// Global PWA manager instance
window.pwaManager = new PWAManager();

// Blazor interop functions
window.blazorPWA = {
    checkForUpdates: () => window.pwaManager.checkForUpdates(),
    applyUpdate: () => window.pwaManager.applyUpdate(),
    clearCache: () => window.pwaManager.clearCache(),
    getInfo: () => window.pwaManager.getServiceWorkerInfo(),
    enablePWA: () => window.pwaManager.enablePWA(),
    disablePWA: () => window.pwaManager.disablePWA(),
    isPWAEnabled: () => window.pwaManager.isPWAEnabled(),

    // Event subscriptions for Blazor
    onUpdateAvailable: (dotNetRef, methodName) => {
        window.pwaManager.onUpdateAvailable((data) => {
            dotNetRef.invokeMethodAsync(methodName, data || {});
        });
    },

    onUpdateInstalled: (dotNetRef, methodName) => {
        window.pwaManager.onUpdateInstalled(() => {
            dotNetRef.invokeMethodAsync(methodName);
        });
    }
};

console.log('[PWA] PWA Manager initialized');