// Fixed Unified PWA Manager - Install Button Issue Resolution
class UnifiedPWAManager {
    constructor() {
        if (UnifiedPWAManager.instance) return UnifiedPWAManager.instance;

        this.isGitHubPages = window.location.hostname === 'mid-d-man.github.io';
        this.basePath = this.isGitHubPages ? '/AirCode/' : '/';
        this.serviceWorkerUrl = this.basePath + 'service-worker.js'; // Fixed path construction
        this.registration = null;
        this.updateAvailable = false;
        this.deferredPrompt = null;
        this.dotNetRef = null;
        this.installable = false;
        this.initialized = false;

        UnifiedPWAManager.instance = this;

        // Critical: Setup listeners IMMEDIATELY, before DOM ready
        this.setupEventListeners();
        this.init();
    }

    async init() {
        if (this.initialized) return;

        try {
            await this.registerServiceWorker();
            this.initialized = true;
            console.log('Unified PWA Manager initialized');

            // Force check after initialization
            setTimeout(() => this.checkInstallStatus(), 1000);
        } catch (error) {
            console.error('PWA initialization failed:', error);
            this.initialized = true; // Don't block on SW failure
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
                    updateViaCache: 'none' // Force update checks
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

            // Check for updates
            await this.registration.update();
        } catch (error) {
            console.error('SW registration failed:', error);
            // Don't throw - PWA features can work without SW
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
        // Critical Fix: Install prompt listener
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('🎯 beforeinstallprompt event captured!');
            e.preventDefault();
            this.deferredPrompt = e;
            this.installable = true;

            // Immediate notification
            this.notifyDotNet('OnInstallPromptReady');

            // Also trigger UI update for non-Blazor scenarios
            this.triggerInstallUI();
        });

        // App installed
        window.addEventListener('appinstalled', () => {
            console.log('✅ App was installed');
            this.deferredPrompt = null;
            this.installable = false;
            this.notifyDotNet('OnAppInstalled');
            this.hideInstallUI();
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

        // DOM ready handling
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.checkInstallStatus());
        } else {
            // Already loaded - check immediately
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

        // Force installability check for testing
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

    // UI Methods for fallback install button
    triggerInstallUI() {
        // Create install button if it doesn't exist
        if (!document.getElementById('pwa-install-btn')) {
            this.createInstallButton();
        }
    }

    createInstallButton() {
        const button = document.createElement('button');
        button.id = 'pwa-install-btn';
        button.className = 'pwa-install-button';
        button.innerHTML = '📱 Install App';
        button.style.cssText = `
            position: fixed;
            bottom: 20px;
            right: 20px;
            background: #007bff;
            color: white;
            border: none;
            padding: 12px 20px;
            border-radius: 8px;
            font-size: 14px;
            cursor: pointer;
            z-index: 10000;
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        `;

        button.addEventListener('click', () => this.installApp());
        document.body.appendChild(button);
    }

    hideInstallUI() {
        const button = document.getElementById('pwa-install-btn');
        if (button) button.remove();
    }

    // Core API methods
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

                if (result.outcome === 'accepted') {
                    this.hideInstallUI();
                }

                return result.outcome === 'accepted';
            } catch (error) {
                console.error('Install failed:', error);
            }
        }

        // Fallback instructions
        if (this.installable || this.canBeInstalled()) {
            this.showInstallInstructions();
        }

        return false;
    }

    showInstallInstructions() {
        const userAgent = navigator.userAgent.toLowerCase();
        let instructions = 'To install this app:\n';

        if (userAgent.includes('chrome')) {
            instructions += '1. Click the menu (⋮) in the address bar\n2. Select "Install AirCode"\n3. Click Install';
        } else if (userAgent.includes('firefox')) {
            instructions += '1. Click the menu button\n2. Look for "Install" or "Add to Home Screen"\n3. Follow prompts';
        } else if (userAgent.includes('safari')) {
            instructions += '1. Tap the Share button\n2. Select "Add to Home Screen"\n3. Tap Add';
        } else {
            instructions += '1. Look for "Add to Home Screen" in your browser menu\n2. Follow the prompts';
        }

        alert(instructions);
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

    // Blazor integration
    setDotNetReference(dotNetRef) {
        this.dotNetRef = dotNetRef;
        console.log('🔗 .NET reference set');

        // Send current status immediately
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
    return manager.getStatus(); // Return immediate status
};

// Debug helpers
window.debugPWA = () => {
    const manager = UnifiedPWAManager.getInstance();
    console.log('🐛 PWA Debug Info:', {
        status: manager.getStatus(),
        registration: manager.registration,
        deferredPrompt: manager.deferredPrompt,
        installable: manager.installable
    });
};

// Force install button for testing
window.forceInstallButton = () => {
    const manager = UnifiedPWAManager.getInstance();
    manager.installable = true;
    manager.triggerInstallUI();
};

// Initialize immediately
window.pwaManager = UnifiedPWAManager.getInstance();
console.log('🎉 PWA Manager loaded and initialized');