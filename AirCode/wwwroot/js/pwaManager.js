// Fixed PWA Manager - Critical Initialization Issues Resolved
class UnifiedPWAManager {
    constructor() {
        if (UnifiedPWAManager.instance) return UnifiedPWAManager.instance;

        this.isGitHubPages = window.location.hostname === 'mid-d-man.github.io';
        this.basePath = this.isGitHubPages ? '/AirCode/' : '/';
        this.serviceWorkerUrl = this.basePath + 'service-worker.js';
        this.registration = null;
        this.updateAvailable = false;
        this.deferredPrompt = null;
        this.dotNetRef = null;
        this.installable = false;
        this.initialized = false;
        this.initPromise = null;

        UnifiedPWAManager.instance = this;

        // Immediate initialization
        this.setupEventListeners();
        this.initPromise = this.init();
    }

    async init() {
        if (this.initialized) return;

        try {
            // Wait for DOM if not ready
            if (document.readyState === 'loading') {
                await new Promise(resolve => {
                    document.addEventListener('DOMContentLoaded', resolve, { once: true });
                });
            }

            await this.registerServiceWorker();
            this.initialized = true;
            console.log('PWA Manager initialized successfully');

            // Check install status after a brief delay
            setTimeout(() => this.checkInstallStatus(), 500);
        } catch (error) {
            console.error('PWA initialization failed:', error);
            this.initialized = true; // Mark as initialized even on failure
        }
    }

    async registerServiceWorker() {
        if (!('serviceWorker' in navigator)) {
            console.log('Service Worker not supported');
            return;
        }

        try {
            console.log('Registering SW at:', this.serviceWorkerUrl);

            this.registration = await navigator.serviceWorker.register(
                this.serviceWorkerUrl,
                {
                    scope: this.basePath,
                    updateViaCache: 'none'
                }
            );

            console.log('SW registered successfully:', this.registration.scope);

            // Handle updates
            this.registration.addEventListener('updatefound', () => {
                this.trackInstallation(this.registration.installing);
            });

            if (this.registration.waiting) {
                this.updateAvailable = true;
                this.notifyDotNet('OnUpdateAvailable');
            }

            // Check for updates
            await this.registration.update();
        } catch (error) {
            console.error('SW registration failed:', error);
            // Don't throw - allow app to continue without SW
        }
    }

    trackInstallation(worker) {
        if (!worker) return;

        worker.addEventListener('statechange', () => {
            if (worker.state === 'installed') {
                if (navigator.serviceWorker.controller) {
                    this.updateAvailable = true;
                    this.notifyDotNet('OnUpdateAvailable');
                } else {
                    this.notifyDotNet('OnAppInstalled');
                }
            }
        });
    }

    setupEventListeners() {
        // Install prompt handling
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('beforeinstallprompt event captured');
            e.preventDefault();
            this.deferredPrompt = e;
            this.installable = true;
            this.notifyDotNet('OnInstallPromptReady');
        });

        window.addEventListener('appinstalled', () => {
            console.log('App was installed');
            this.deferredPrompt = null;
            this.installable = false;
            this.notifyDotNet('OnAppInstalled');
        });

        // Connectivity monitoring
        ['online', 'offline'].forEach(event => {
            window.addEventListener(event, () => {
                this.notifyDotNet('OnConnectivityChanged', navigator.onLine);
            });
        });

        // Visibility change
        document.addEventListener('visibilitychange', () => {
            this.notifyDotNet('OnVisibilityChange', !document.hidden);
        });

        // Service Worker messages
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.addEventListener('message', event => {
                if (event.data?.type === 'UPDATE_AVAILABLE') {
                    this.updateAvailable = true;
                    this.notifyDotNet('OnUpdateAvailable');
                }
            });

            navigator.serviceWorker.addEventListener('controllerchange', () => {
                if (this.updateAvailable) {
                    console.log('SW controller changed, reloading...');
                    window.location.reload();
                }
            });
        }

        // Initialize check when DOM is ready
        this.scheduleInstallCheck();
    }

    scheduleInstallCheck() {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                setTimeout(() => this.checkInstallStatus(), 100);
            });
        } else {
            setTimeout(() => this.checkInstallStatus(), 100);
        }
    }

    checkInstallStatus() {
        const isInstalled = this.isAppInstalled();
        console.log('Install status check:', {
            installed: isInstalled,
            installable: this.installable,
            hasDeferredPrompt: !!this.deferredPrompt,
            isHTTPS: this.isHTTPS(),
            hasManifest: this.hasManifest(),
            canBeInstalled: this.canBeInstalled()
        });

        if (isInstalled) {
            this.installable = false;
            return;
        }

        // Manual installability detection for browsers that support PWA without beforeinstallprompt
        if (!this.deferredPrompt && this.canBeInstalled()) {
            console.log('Manual installability detected');
            this.installable = true;
            this.notifyDotNet('OnInstallPromptReady');
        }
    }

    isAppInstalled() {
        return window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone === true ||
            document.referrer.includes('android-app://') ||
            window.matchMedia('(display-mode: fullscreen)').matches ||
            window.matchMedia('(display-mode: minimal-ui)').matches;
    }

    isHTTPS() {
        return location.protocol === 'https:' ||
            location.hostname === 'localhost' ||
            location.hostname === '127.0.0.1';
    }

    hasManifest() {
        return !!document.querySelector('link[rel="manifest"]');
    }

    canBeInstalled() {
        return this.isHTTPS() &&
            this.hasManifest() &&
            'serviceWorker' in navigator &&
            this.isChromiumBrowser();
    }

    isChromiumBrowser() {
        const userAgent = navigator.userAgent;
        const isChrome = /Chrome/.test(userAgent) && /Google Inc/.test(navigator.vendor);
        const isEdge = /Edg/.test(userAgent);
        const isOpera = /OPR/.test(userAgent);
        const isBrave = navigator.brave !== undefined;
        const isFirefox = /Firefox/.test(userAgent);
        const isSafari = /Safari/.test(userAgent) && !/Chrome/.test(userAgent);

        return (isChrome || isEdge || isOpera || isBrave) && !isFirefox && !isSafari;
    }

    async installApp() {
        console.log('Install attempt:', {
            hasDeferredPrompt: !!this.deferredPrompt,
            installable: this.installable
        });

        if (this.deferredPrompt) {
            try {
                this.deferredPrompt.prompt();
                const result = await this.deferredPrompt.userChoice;
                console.log('Install result:', result);

                this.deferredPrompt = null;
                this.installable = false;

                return result.outcome === 'accepted';
            } catch (error) {
                console.error('Install failed:', error);
                return false;
            }
        }

        return false;
    }

    async applyUpdate() {
        if (this.registration?.waiting) {
            console.log('Applying update...');
            this.registration.waiting.postMessage({ type: 'SKIP_WAITING' });
            return true;
        }
        return false;
    }

    async checkForUpdates() {
        if (this.registration) {
            try {
                await this.registration.update();
                console.log('Update check completed');
                return true;
            } catch (error) {
                console.error('Update check failed:', error);
                return false;
            }
        }
        return false;
    }

    getStatus() {
        return {
            isInstallable: this.installable || !!this.deferredPrompt,
            isInstalled: this.isAppInstalled(),
            hasServiceWorker: !!this.registration,
            updateAvailable: this.updateAvailable,
            isOnline: navigator.onLine,
            isChromiumBased: this.isChromiumBrowser(),
            canBeInstalled: this.canBeInstalled(),
            isInitialized: this.initialized
        };
    }

    async setDotNetReference(dotNetRef) {
        this.dotNetRef = dotNetRef;
        console.log('.NET reference set');

        // Wait for initialization before sending status
        if (this.initPromise) {
            await this.initPromise;
        }

        // Send current status after brief delay
        setTimeout(() => {
            const status = this.getStatus();
            console.log('Sending status to .NET:', status);

            if (status.isInstallable) {
                this.notifyDotNet('OnInstallPromptReady');
            }
            if (status.updateAvailable) {
                this.notifyDotNet('OnUpdateAvailable');
            }

            // Send connectivity status
            this.notifyDotNet('OnConnectivityChanged', navigator.onLine);
        }, 200);
    }

    notifyDotNet(method, data = null) {
        if (this.dotNetRef) {
            try {
                console.log(`Notifying .NET: ${method}`, data);
                this.dotNetRef.invokeMethodAsync(method, data)
                    .catch(error => console.error(`Failed to invoke ${method}:`, error));
            } catch (error) {
                console.error(`Failed to notify .NET ${method}:`, error);
            }
        } else {
            console.log(`Queued notification: ${method}`, data);
        }
    }

    static getInstance() {
        return UnifiedPWAManager.instance || new UnifiedPWAManager();
    }
}

// Global functions for Blazor
window.getPWAManager = () => UnifiedPWAManager.getInstance();

window.setupPWAMonitoring = async (dotNetRef) => {
    const manager = UnifiedPWAManager.getInstance();
    await manager.setDotNetReference(dotNetRef);
    return manager.getStatus();
};

// Initialize PWA Manager immediately
window.pwaManager = UnifiedPWAManager.getInstance();
console.log('PWA Manager loaded and initialized');