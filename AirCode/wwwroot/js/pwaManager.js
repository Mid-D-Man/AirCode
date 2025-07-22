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

        UnifiedPWAManager.instance = this;
        this.init();
    }

    async init() {
        try {
            await this.registerServiceWorker();
            this.setupEventListeners();
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
        // Install prompt
        window.addEventListener('beforeinstallprompt', (e) => {
            e.preventDefault();
            this.deferredPrompt = e;
        });

        // App installed
        window.addEventListener('appinstalled', () => {
            this.deferredPrompt = null;
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
    }

    // Core API methods
    async installApp() {
        if (!this.deferredPrompt) return false;

        this.deferredPrompt.prompt();
        const result = await this.deferredPrompt.userChoice;
        this.deferredPrompt = null;
        return result.outcome === 'accepted';
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
        return {
            isInstallable: !!this.deferredPrompt,
            isInstalled: window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone,
            hasServiceWorker: !!this.registration,
            updateAvailable: this.updateAvailable,
            isOnline: navigator.onLine
        };
    }

    // Blazor integration
    setDotNetReference(dotNetRef) {
        this.dotNetRef = dotNetRef;
    }

    notifyDotNet(method, data = null) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync(method, data);
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

// Initialize
window.pwaManager = UnifiedPWAManager.getInstance();