// PWA Manager - Enhanced Service Worker Integration for Blazor
// Handles PWA installation, updates, and offline status

class PWAManager {
    constructor() {
        this.serviceWorker = null;
        this.deferredPrompt = null;
        this.isOnline = navigator.onLine;
        this.offlineReady = false;
        this.dotNetRef = null;

        this.init();
    }

    async init() {
        await this.registerServiceWorker();
        this.setupInstallPrompt();
        this.setupConnectivityMonitoring();
        await this.checkCacheStatus();
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

                navigator.serviceWorker.addEventListener('message', event => {
                    this.handleServiceWorkerMessage(event.data);
                });

                if (registration.waiting) {
                    this.notifyBlazorUpdate();
                }

            } catch (error) {
                console.error('Service Worker registration failed:', error);
            }
        }
    }

    handleServiceWorkerMessage(data) {
        switch (data.type) {
            case 'SW_ACTIVATED':
                this.offlineReady = data.offlineReady;
                break;
            case 'CACHE_STATUS':
                this.offlineReady = data.offlineReady;
                break;
        }
    }

    async checkCacheStatus() {
        if (this.serviceWorker?.active) {
            const messageChannel = new MessageChannel();
            return new Promise((resolve) => {
                messageChannel.port1.onmessage = (event) => {
                    this.handleServiceWorkerMessage(event.data);
                    resolve(event.data);
                };
                this.serviceWorker.active.postMessage(
                    { type: 'GET_CACHE_STATUS' },
                    [messageChannel.port2]
                );
            });
        }
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
            navigator.serviceWorker.addEventListener('controllerchange', () => {
                window.location.reload();
            });
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
            IsChromiumBased: this.isChromiumBrowser()
        };
    }

    // Blazor notification methods
    notifyBlazorInstallReady() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnInstallPromptReady');
        }
    }

    notifyBlazorUpdate() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnUpdateAvailable');
        }
    }

    notifyBlazorInstalled() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnAppInstalled');
        }
    }

    notifyBlazorConnectivity(isOnline) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnConnectivityChanged', isOnline);
        }
    }

    notifyBlazorVisibility(visible) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnVisibilityChange', visible);
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