// PWA Manager - Enhanced Service Worker Integration for Blazor
// Handles PWA installation, updates, and offline status

class PWAManager {
    constructor() {
        this.serviceWorker = null;
        this.deferredPrompt = null;
        this.isOnline = navigator.onLine;
        this.offlineReady = false;
        this.dotNetRef = null;
        this.initialized = false;

        this.init();
    }

    async init() {
        await this.registerServiceWorker();
        this.setupInstallPrompt();
        this.setupConnectivityMonitoring();
        await this.checkCacheStatus();
        this.initialized = true;
        console.log('PWA Manager initialized');
    }

    async registerServiceWorker() {
        if ('serviceWorker' in navigator) {
            try {
                const registration = await navigator.serviceWorker.register('/AirCode/service-worker.js', {
                    scope: '/AirCode/'
                });

                this.serviceWorker = registration;
                console.log('Service Worker registered:', registration.scope);

                // Listen for updates
                registration.addEventListener('updatefound', () => {
                    const newWorker = registration.installing;
                    if (newWorker) {
                        newWorker.addEventListener('statechange', () => {
                            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                                this.notifyBlazorUpdate();
                            }
                        });
                    }
                });

                // Listen for messages from service worker
                navigator.serviceWorker.addEventListener('message', event => {
                    this.handleServiceWorkerMessage(event.data);
                });

                // Check if there's already an update waiting
                if (registration.waiting) {
                    this.notifyBlazorUpdate();
                }

                // Send initial cache status request after a brief delay
                setTimeout(() => this.checkCacheStatus(), 1000);

            } catch (error) {
                console.error('Service Worker registration failed:', error);
            }
        }
    }

    handleServiceWorkerMessage(data) {
        if (!data || !data.type) return;

        switch (data.type) {
            case 'SW_ACTIVATED':
                this.offlineReady = data.offlineReady || false;
                console.log('[PWA] Service Worker activated, offline ready:', this.offlineReady);
                break;
            case 'CACHE_STATUS':
                this.offlineReady = data.offlineReady || false;
                console.log('[PWA] Cache status updated, offline ready:', this.offlineReady);
                break;
        }
    }

    async checkCacheStatus() {
        if (this.serviceWorker?.active) {
            try {
                const messageChannel = new MessageChannel();

                const response = await new Promise((resolve, reject) => {
                    messageChannel.port1.onmessage = (event) => {
                        resolve(event.data);
                    };

                    // Timeout after 3 seconds
                    setTimeout(() => reject(new Error('Timeout')), 3000);

                    this.serviceWorker.active.postMessage(
                        { type: 'GET_CACHE_STATUS' },
                        [messageChannel.port2]
                    );
                });

                this.handleServiceWorkerMessage(response);
                return response;
            } catch (error) {
                console.warn('[PWA] Cache status check failed:', error.message);
                return null;
            }
        }
        return null;
    }

    setupInstallPrompt() {
        window.addEventListener('beforeinstallprompt', (event) => {
            event.preventDefault();
            this.deferredPrompt = event;
            this.notifyBlazorInstallReady();
        });

        window.addEventListener('appinstalled', () => {
            this.deferredPrompt = null;
            this.notifyBlazorInstalled();
        });
    }

    async installApp() {
        if (!this.deferredPrompt) return false;

        try {
            this.deferredPrompt.prompt();
            const { outcome } = await this.deferredPrompt.userChoice;
            this.deferredPrompt = null;
            return outcome === 'accepted';
        } catch (error) {
            console.error('PWA installation failed:', error);
            return false;
        }
    }

    setupConnectivityMonitoring() {
        window.addEventListener('online', () => {
            this.isOnline = true;
            this.notifyBlazorConnectivity(true);
        });

        window.addEventListener('offline', () => {
            this.isOnline = false;
            this.notifyBlazorConnectivity(false);
        });

        // Monitor visibility changes
        document.addEventListener('visibilitychange', () => {
            this.notifyBlazorVisibility(!document.hidden);
        });
    }

    async applyUpdate() {
        if (this.serviceWorker?.waiting) {
            this.serviceWorker.waiting.postMessage({ type: 'SKIP_WAITING' });

            // Wait for controller change, then reload
            navigator.serviceWorker.addEventListener('controllerchange', () => {
                window.location.reload();
            }, { once: true });
        }
    }

    async checkForUpdates() {
        if (this.serviceWorker) {
            await this.serviceWorker.update();
            await this.updateStatus();
        }
    }

    async updateStatus() {
        // Force status update
        await this.checkCacheStatus();
    }

    isChromiumBrowser() {
        return /Chrome|Chromium|Edge/i.test(navigator.userAgent);
    }

    getStatus() {
        return {
            IsOnline: this.isOnline,
            IsInstallable: !!this.deferredPrompt,
            IsInstalled: window.matchMedia('(display-mode: standalone)').matches,
            HasServiceWorker: !!this.serviceWorker,
            UpdateAvailable: !!this.serviceWorker?.waiting,
            IsChromiumBased: this.isChromiumBrowser(),
            OfflineReady: this.offlineReady
        };
    }

    // Blazor notification methods
    notifyBlazorInstallReady() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnInstallPromptReady').catch(console.error);
        }
    }

    notifyBlazorUpdate() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnUpdateAvailable').catch(console.error);
        }
    }

    notifyBlazorInstalled() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnAppInstalled').catch(console.error);
        }
    }

    notifyBlazorConnectivity(isOnline) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnConnectivityChanged', isOnline).catch(console.error);
        }
    }

    notifyBlazorVisibility(visible) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnVisibilityChange', visible).catch(console.error);
        }
    }
}

// Global PWA Manager instance
window.pwaManager = new PWAManager();

// Blazor Interop Functions
window.getPWAManager = () => {
    return window.pwaManager;
};

window.setupPWAMonitoring = (dotNetRef) => {
    if (window.pwaManager) {
        window.pwaManager.dotNetRef = dotNetRef;
        console.log('PWA monitoring setup complete');
    }
};