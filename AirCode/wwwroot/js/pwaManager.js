// Unified PWA Manager - Single source of truth
class UnifiedPWAManager {
    constructor() {
        if (UnifiedPWAManager.instance) return UnifiedPWAManager.instance;

        this.isGitHubPages = window.location.hostname === 'mid-d-man.github.io';
        this.basePath = this.isGitHubPages ? '/AirCode/' : '/';
        this.registration = null;
        this.updateAvailable = false;
        this.deferredPrompt = null;
        this.dotNetRef = null;
        this.installable = false; // Add explicit installable state

        UnifiedPWAManager.instance = this;
        this.init();
    }

    async init() {
        try {
            // Setup listeners BEFORE service worker registration
            this.setupEventListeners();
            await this.registerServiceWorker();
            console.log('Unified PWA Manager initialized');
        } catch (error) {
            console.error('PWA initialization failed:', error);
        }
    }

    async registerServiceWorker() {
        if (!('serviceWorker' in navigator)) return;

        try {
            this.registration = await navigator.serviceWorker.register(
                this.basePath + 'service-worker.js',
                { scope: this.basePath }
            );

            this.registration.addEventListener('updatefound', () => {
                this.trackInstallation(this.registration.installing);
            });

            if (this.registration.waiting) {
                this.updateAvailable = true;
                this.notifyDotNet('OnUpdateAvailable');
            }

            // Force update check after registration
            await this.registration.update();
        } catch (error) {
            console.error('SW registration failed:', error);
        }
    }

    trackInstallation(worker) {
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
        // Install prompt - Critical fix
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('beforeinstallprompt event fired');
            e.preventDefault();
            this.deferredPrompt = e;
            this.installable = true;
            // Notify Blazor immediately when prompt is available
            this.notifyDotNet('OnInstallPromptReady');
        });

        // App installed
        window.addEventListener('appinstalled', () => {
            console.log('App was installed');
            this.deferredPrompt = null;
            this.installable = false;
            this.notifyDotNet('OnAppInstalled');
        });

        // Network status
        ['online', 'offline'].forEach(event => {
            window.addEventListener(event, () => {
                this.notifyDotNet('OnConnectivityChanged', navigator.onLine);
            });
        });

        // App lifecycle
        document.addEventListener('visibilitychange', () => {
            this.notifyDotNet('OnVisibilityChange', !document.hidden);
        });

        // SW messages
        navigator.serviceWorker?.addEventListener('message', event => {
            if (event.data?.type === 'UPDATE_AVAILABLE') {
                this.updateAvailable = true;
                this.notifyDotNet('OnUpdateAvailable');
            }
        });

        // Controller change
        navigator.serviceWorker?.addEventListener('controllerchange', () => {
            if (this.updateAvailable) window.location.reload();
        });

        // DOM ready check for install criteria
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.checkInstallCriteria());
        } else {
            this.checkInstallCriteria();
        }
    }

    checkInstallCriteria() {
        // Force check PWA install criteria
        console.log('Checking PWA install criteria...');

        // Check if already installed
        const isStandalone = window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone ||
            document.referrer.includes('android-app://');

        if (isStandalone) {
            console.log('App already installed');
            this.installable = false;
            return;
        }

        // Check manifest
        const manifestLink = document.querySelector('link[rel="manifest"]');
        if (!manifestLink) {
            console.warn('No manifest link found');
        }

        // Manual trigger for testing (remove in production)
        setTimeout(() => {
            if (!this.deferredPrompt && !isStandalone) {
                console.log('Install prompt not triggered automatically');
                // For debugging - check if we can manually create installability
                this.checkManualInstall();
            }
        }, 3000);
    }

    checkManualInstall() {
        // Check if browser supports manual install detection
        if ('getInstalledRelatedApps' in navigator) {
            navigator.getInstalledRelatedApps().then(apps => {
                console.log('Installed apps:', apps);
                if (apps.length === 0 && !this.installable) {
                    // App not installed and no prompt - check criteria manually
                    this.installable = this.isHTTPS() && this.hasManifest() && this.hasServiceWorker();
                    if (this.installable) {
                        console.log('Manual installability detected');
                        this.notifyDotNet('OnInstallPromptReady');
                    }
                }
            });
        }
    }

    isHTTPS() {
        return location.protocol === 'https:' || location.hostname === 'localhost';
    }

    hasManifest() {
        return !!document.querySelector('link[rel="manifest"]');
    }

    hasServiceWorker() {
        return !!this.registration;
    }

    // Core API methods
    async installApp() {
        if (this.deferredPrompt) {
            this.deferredPrompt.prompt();
            const result = await this.deferredPrompt.userChoice;
            this.deferredPrompt = null;
            this.installable = false;
            return result.outcome === 'accepted';
        }

        // Fallback for browsers without beforeinstallprompt
        if (this.installable) {
            alert('To install this app:\n1. Open browser menu\n2. Look for "Add to Home Screen" or "Install App"\n3. Follow the prompts');
            return false;
        }

        return false;
    }

    async applyUpdate() {
        if (this.registration?.waiting) {
            this.registration.waiting.postMessage({ type: 'SKIP_WAITING' });
        }
    }

    async checkForUpdates() {
        if (this.registration) {
            await this.registration.update();
        }
    }

    getStatus() {
        const isInstalled = window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone ||
            document.referrer.includes('android-app://');

        return {
            isInstallable: this.installable || !!this.deferredPrompt,
            isInstalled: isInstalled,
            hasServiceWorker: !!this.registration,
            updateAvailable: this.updateAvailable,
            isOnline: navigator.onLine
        };
    }

    // Blazor integration
    setDotNetReference(dotNetRef) {
        this.dotNetRef = dotNetRef;
        // Immediately send current status
        setTimeout(() => {
            if (this.installable || this.deferredPrompt) {
                this.notifyDotNet('OnInstallPromptReady');
            }
        }, 100);
    }

    notifyDotNet(method, data = null) {
        if (this.dotNetRef) {
            try {
                this.dotNetRef.invokeMethodAsync(method, data);
            } catch (error) {
                console.error('Failed to notify .NET:', error);
            }
        }
    }

    static getInstance() {
        return UnifiedPWAManager.instance || new UnifiedPWAManager();
    }
}

// Global functions for Blazor
window.getPWAManager = () => UnifiedPWAManager.getInstance();
window.setupPWAMonitoring = (dotNetRef) => {
    const manager = UnifiedPWAManager.getInstance();
    manager.setDotNetReference(dotNetRef);
};

// Authentication Service stub to prevent errors
window.AuthenticationService = {
    init: () => Promise.resolve(),
    getUser: () => Promise.resolve(null),
    login: () => Promise.resolve(),
    logout: () => Promise.resolve()
};

// Initialize immediately
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.pwaManager = UnifiedPWAManager.getInstance();
    });
} else {
    window.pwaManager = UnifiedPWAManager.getInstance();
}