// Fixed Unified PWA Manager - Install Button Issue Resolution
// PWA Manager - Install Button Removed, Component-Only Approach
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

        UnifiedPWAManager.instance = this;

        this.setupEventListeners();
        this.init();
    }

    async init() {
        if (this.initialized) return;

        try {
            await this.registerServiceWorker();
            this.initialized = true;
            console.log('Unified PWA Manager initialized');

            setTimeout(() => this.checkInstallStatus(), 1000);
        } catch (error) {
            console.error('PWA initialization failed:', error);
            this.initialized = true;
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

            this.registration.addEventListener('updatefound', () => {
                this.trackInstallation(this.registration.installing);
            });

            if (this.registration.waiting) {
                this.updateAvailable = true;
                this.notifyDotNet('OnUpdateAvailable');
            }

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
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('🎯 beforeinstallprompt event captured!');
            e.preventDefault();
            this.deferredPrompt = e;
            this.installable = true;
            this.notifyDotNet('OnInstallPromptReady');
        });

        window.addEventListener('appinstalled', () => {
            console.log('✅ App was installed');
            this.deferredPrompt = null;
            this.installable = false;
            this.notifyDotNet('OnAppInstalled');
        });

        ['online', 'offline'].forEach(event => {
            window.addEventListener(event, () => {
                this.notifyDotNet('OnConnectivityChanged', navigator.onLine);
            });
        });

        document.addEventListener('visibilitychange', () => {
            this.notifyDotNet('OnVisibilityChange', !document.hidden);
        });

        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.addEventListener('message', event => {
                if (event.data?.type === 'UPDATE_AVAILABLE') {
                    this.updateAvailable = true;
                    this.notifyDotNet('OnUpdateAvailable');
                }
            });

            navigator.serviceWorker.addEventListener('controllerchange', () => {
                if (this.updateAvailable) window.location.reload();
            });
        }

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.checkInstallStatus());
        } else {
            setTimeout(() => this.checkInstallStatus(), 100);
        }
    }

    checkInstallStatus() {
        const isInstalled = this.isAppInstalled();
        console.log('📱 Install status check:', {
            installed: isInstalled,
            installable: this.installable,
            hasDeferredPrompt: !!this.deferredPrompt,
            isHTTPS: this.isHTTPS(),
            hasManifest: this.hasManifest()
        });

        if (isInstalled) {
            this.installable = false;
            return;
        }

        if (!this.deferredPrompt && this.canBeInstalled()) {
            console.log('🔧 Manual installability detected');
            this.installable = true;
            this.notifyDotNet('OnInstallPromptReady');
        }
    }

    isAppInstalled() {
        return window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone ||
            document.referrer.includes('android-app://');
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
        return this.isHTTPS() && this.hasManifest() && 'serviceWorker' in navigator;
    }

    async installApp() {
        console.log('🚀 Install attempt:', {
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
            }
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
            try {
                await this.registration.update();
                console.log('Update check completed');
            } catch (error) {
                console.error('Update check failed:', error);
            }
        }
    }

    getStatus() {
        return {
            isInstallable: this.installable || !!this.deferredPrompt,
            isInstalled: this.isAppInstalled(),
            hasServiceWorker: !!this.registration,
            updateAvailable: this.updateAvailable,
            isOnline: navigator.onLine,
            canBeInstalled: this.canBeInstalled()
        };
    }

    setDotNetReference(dotNetRef) {
        this.dotNetRef = dotNetRef;
        console.log('🔗 .NET reference set');

        setTimeout(() => {
            const status = this.getStatus();
            console.log('📤 Sending status to .NET:', status);

            if (status.isInstallable) {
                this.notifyDotNet('OnInstallPromptReady');
            }
            if (status.updateAvailable) {
                this.notifyDotNet('OnUpdateAvailable');
            }
        }, 100);
    }

    notifyDotNet(method, data = null) {
        console.log(`📡 Notifying .NET: ${method}`, data);
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
    return manager.getStatus();
};

window.pwaManager = UnifiedPWAManager.getInstance();
console.log('🎉 PWA Manager loaded and initialized');