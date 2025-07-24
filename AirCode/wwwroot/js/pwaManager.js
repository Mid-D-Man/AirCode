// PWA Manager - Enhanced Service Worker Integration for Blazor
// Handles PWA installation, updates, and offline status

class PWAManager {
    constructor() {
        this.serviceWorker = null;
        this.deferredPrompt = null;
        this.isOnline = navigator.onLine;
        this.offlineReady = false;

        this.init();
    }

    async init() {
        // Register service worker
        await this.registerServiceWorker();

        // Setup PWA installation prompt
        this.setupInstallPrompt();

        // Monitor online/offline status
        this.setupConnectivityMonitoring();

        // Check cache status
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

                // Handle service worker updates
                registration.addEventListener('updatefound', () => {
                    const newWorker = registration.installing;
                    if (newWorker) {
                        newWorker.addEventListener('statechange', () => {
                            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                                // New version available
                                this.showUpdateNotification();
                            }
                        });
                    }
                });

                // Listen for service worker messages
                navigator.serviceWorker.addEventListener('message', event => {
                    this.handleServiceWorkerMessage(event.data);
                });

                // If there's a waiting service worker, prompt for update
                if (registration.waiting) {
                    this.showUpdateNotification();
                }

            } catch (error) {
                console.error('Service Worker registration failed:', error);
            }
        } else {
            console.warn('Service Workers not supported');
        }
    }

    handleServiceWorkerMessage(data) {
        switch (data.type) {
            case 'SW_ACTIVATED':
                console.log('Service Worker activated:', data);
                this.offlineReady = data.offlineReady;
                this.showOfflineReadyNotification();
                break;

            case 'CACHE_STATUS':
                console.log('Cache status:', data);
                this.offlineReady = data.offlineReady;
                this.updateOfflineIndicator();
                break;

            default:
                console.log('SW Message:', data);
        }
    }

    async checkCacheStatus() {
        if (this.serviceWorker && this.serviceWorker.active) {
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
            console.log('PWA install prompt available');
            event.preventDefault();
            this.deferredPrompt = event;
            this.showInstallButton();
        });

        window.addEventListener('appinstalled', () => {
            console.log('PWA installed successfully');
            this.hideInstallButton();
            this.deferredPrompt = null;
        });
    }

    async installPWA() {
        if (!this.deferredPrompt) {
            console.log('PWA install prompt not available');
            return false;
        }

        try {
            this.deferredPrompt.prompt();
            const { outcome } = await this.deferredPrompt.userChoice;

            console.log('PWA install outcome:', outcome);

            if (outcome === 'accepted') {
                this.hideInstallButton();
            }

            this.deferredPrompt = null;
            return outcome === 'accepted';

        } catch (error) {
            console.error('PWA installation failed:', error);
            return false;
        }
    }

    setupConnectivityMonitoring() {
        window.addEventListener('online', () => {
            console.log('Connection restored');
            this.isOnline = true;
            this.updateConnectivityStatus();
            this.syncWhenOnline();
        });

        window.addEventListener('offline', () => {
            console.log('Connection lost');
            this.isOnline = false;
            this.updateConnectivityStatus();
        });

        // Initial status
        this.updateConnectivityStatus();
    }

    updateConnectivityStatus() {
        const statusElement = document.getElementById('connectivity-status');
        if (statusElement) {
            if (this.isOnline) {
                statusElement.textContent = 'Online';
                statusElement.className = 'connectivity-status online';
            } else {
                statusElement.textContent = this.offlineReady ? 'Offline (Ready)' : 'Offline (Limited)';
                statusElement.className = `connectivity-status offline ${this.offlineReady ? 'ready' : 'limited'}`;
            }
        }

        // Update body class for CSS styling
        document.body.classList.toggle('app-online', this.isOnline);
        document.body.classList.toggle('app-offline', !this.isOnline);
        document.body.classList.toggle('offline-ready', this.offlineReady);
    }

    updateOfflineIndicator() {
        const indicator = document.getElementById('offline-indicator');
        if (indicator) {
            if (this.offlineReady) {
                indicator.textContent = '✓ Offline Ready';
                indicator.className = 'offline-indicator ready';
            } else {
                indicator.textContent = '⚠ Limited Offline';
                indicator.className = 'offline-indicator limited';
            }
        }
    }

    showInstallButton() {
        const installBtn = document.getElementById('pwa-install-btn');
        if (installBtn) {
            installBtn.style.display = 'block';
            installBtn.addEventListener('click', () => this.installPWA());
        } else {
            // Create install button if it doesn't exist
            this.createInstallButton();
        }
    }

    hideInstallButton() {
        const installBtn = document.getElementById('pwa-install-btn');
        if (installBtn) {
            installBtn.style.display = 'none';
        }
    }

    createInstallButton() {
        const button = document.createElement('button');
        button.id = 'pwa-install-btn';
        button.textContent = 'Install App';
        button.className = 'pwa-install-button';
        button.addEventListener('click', () => this.installPWA());

        // Add to page (you might want to customize this)
        document.body.appendChild(button);
    }

    showUpdateNotification() {
        // Create or show update notification
        const notification = document.getElementById('update-notification') || this.createUpdateNotification();
        notification.style.display = 'block';
    }

    createUpdateNotification() {
        const notification = document.createElement('div');
        notification.id = 'update-notification';
        notification.className = 'update-notification';
        notification.innerHTML = `
            <div class="update-content">
                <span>New version available!</span>
                <button onclick="window.pwaManager.applyUpdate()">Update</button>
                <button onclick="window.pwaManager.dismissUpdate()">Later</button>
            </div>
        `;

        document.body.appendChild(notification);
        return notification;
    }

    async applyUpdate() {
        if (this.serviceWorker && this.serviceWorker.waiting) {
            this.serviceWorker.waiting.postMessage({ type: 'SKIP_WAITING' });

            // Listen for controlling change
            navigator.serviceWorker.addEventListener('controllerchange', () => {
                window.location.reload();
            });
        }
    }

    dismissUpdate() {
        const notification = document.getElementById('update-notification');
        if (notification) {
            notification.style.display = 'none';
        }
    }

    showOfflineReadyNotification() {
        console.log('App is ready for offline use!');

        // You can customize this notification
        const toast = document.createElement('div');
        toast.className = 'offline-ready-toast';
        toast.textContent = 'App is ready for offline use!';
        toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: #28a745;
            color: white;
            padding: 12px 20px;
            border-radius: 4px;
            z-index: 9999;
            font-family: Arial, sans-serif;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        `;

        document.body.appendChild(toast);

        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 5000);
    }

    syncWhenOnline() {
        if (this.isOnline) {
            // Trigger any pending sync operations
            console.log('Triggering sync operations...');

            // You can dispatch custom events here for your Blazor components to handle
            window.dispatchEvent(new CustomEvent('pwa-online', {
                detail: { timestamp: new Date().toISOString() }
            }));
        }
    }

    async forceUpdateCache() {
        if (this.serviceWorker && this.serviceWorker.active) {
            this.serviceWorker.active.postMessage({ type: 'FORCE_UPDATE_CACHE' });

            // Wait a bit then reload
            setTimeout(() => {
                window.location.reload();
            }, 1000);
        }
    }

    // Get current PWA status for debugging
    getStatus() {
        return {
            isOnline: this.isOnline,
            offlineReady: this.offlineReady,
            serviceWorkerRegistered: !!this.serviceWorker,
            installPromptAvailable: !!this.deferredPrompt,
            isPWAInstalled: window.matchMedia('(display-mode: standalone)').matches
        };
    }

    // Utility method for Blazor interop
    async getStatusForBlazor() {
        const status = this.getStatus();
        const cacheStatus = await this.checkCacheStatus();
        return { ...status, ...cacheStatus };
    }
}

// Initialize PWA Manager when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.pwaManager = new PWAManager();
    });
} else {
    window.pwaManager = new PWAManager();
}

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = PWAManager;
}