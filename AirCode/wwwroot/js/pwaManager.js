// PWA Manager for Blazor WASM - GitHub Pages Compatible
// Place this in wwwroot/js/pwaManager.js

class PWAManager {
    constructor() {
        // Prevent multiple instantiation .
        if (PWAManager.instance) {
            return PWAManager.instance;
        }
        
        this.isGitHubPages = window.location.hostname === 'mid-d-man.github.io';
        this.basePath = this.isGitHubPages ? '/AirCode/' : '/';
        this.serviceWorkerUrl = this.basePath + 'service-worker.js';
        this.registration = null;
        this.updateAvailable = false;
        this.initialized = false;
        
        // Store singleton instance
        PWAManager.instance = this;
        
        // Initialize only once
        this.init();
    }

    async init() {
        if (this.initialized) {
            console.log('PWA Manager already initialized');
            return;
        }

        if ('serviceWorker' in navigator) {
            try {
                await this.registerServiceWorker();
                this.setupUpdateListener();
                this.setupInstallPrompt();
                this.initialized = true;
                console.log('PWA Manager initialized successfully');
            } catch (error) {
                console.error('PWA Manager initialization failed:', error);
            }
        } else {
            console.warn('Service Worker not supported in this browser');
            this.initialized = true;
        }
    }

    async registerServiceWorker() {
        try {
            console.log('Registering service worker:', this.serviceWorkerUrl);

            const swPath = this.basePath + 'service-worker.js';

            this.registration = await navigator.serviceWorker.register(swPath, {
                scope: this.basePath
            });

            console.log('Service Worker registered successfully:', this.registration.scope);

            // Handle different registration states
            if (this.registration.installing) {
                console.log('Service Worker installing...');
                this.trackInstallation(this.registration.installing);
            } else if (this.registration.waiting) {
                console.log('Service Worker waiting...');
                this.showUpdateAvailable();
            } else if (this.registration.active) {
                console.log('Service Worker active');
            }

            // Listen for updates
            this.registration.addEventListener('updatefound', () => {
                console.log('New service worker found');
                this.trackInstallation(this.registration.installing);
            });

        } catch (error) {
            console.error('Service Worker registration failed:', error);
            throw error;
        }
    }

    trackInstallation(worker) {
        worker.addEventListener('statechange', () => {
            if (worker.state === 'installed') {
                if (navigator.serviceWorker.controller) {
                    // New version available
                    console.log('New version available');
                    this.showUpdateAvailable();
                } else {
                    // First installation
                    console.log('App is ready for offline use');
                    this.showInstallSuccess();
                }
            }
        });
    }

    setupUpdateListener() {
        // Listen for messages from service worker
        navigator.serviceWorker.addEventListener('message', event => {
            if (event.data && event.data.type === 'UPDATE_AVAILABLE') {
                this.showUpdateAvailable();
            }
        });

        // Listen for controller change (new SW activation)
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (this.updateAvailable) {
                window.location.reload();
            }
        });
    }

    setupInstallPrompt() {
        let deferredPrompt;

        // Listen for beforeinstallprompt event
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('Install prompt available');
            e.preventDefault();
            deferredPrompt = e;
            this.showInstallButton(deferredPrompt);
        });

        // Listen for app installed event
        window.addEventListener('appinstalled', (evt) => {
            console.log('App installed successfully');
            this.hideInstallButton();
            this.showInstallSuccess();
        });
    }

    showUpdateAvailable() {
        this.updateAvailable = true;
        
        // Create or update notification
        const notification = this.createNotification(
            'Update Available',
            'A new version of AirCode is available. Click to update.',
            'update',
            () => this.applyUpdate()
        );
        
        this.showNotification(notification);
    }

    showInstallButton(deferredPrompt) {
        const installButton = this.createInstallButton(deferredPrompt);
        this.addToUI(installButton);
    }

    hideInstallButton() {
        const installButton = document.getElementById('pwa-install-button');
        if (installButton) {
            installButton.remove();
        }
    }

    showInstallSuccess() {
        const notification = this.createNotification(
            'App Installed',
            'AirCode is now installed and ready for offline use!',
            'success',
            null,
            3000
        );
        
        this.showNotification(notification);
    }

    async applyUpdate() {
        if (this.registration && this.registration.waiting) {
            // Send message to waiting service worker to skip waiting
            this.registration.waiting.postMessage({ type: 'SKIP_WAITING' });
        }
    }

    createNotification(title, message, type, action = null, duration = 0) {
        const notification = document.createElement('div');
        notification.className = `pwa-notification pwa-notification-${type}`;
        notification.innerHTML = `
            <div class="pwa-notification-content">
                <h4>${title}</h4>
                <p>${message}</p>
                ${action ? '<button class="pwa-notification-action">Update Now</button>' : ''}
                <button class="pwa-notification-close">×</button>
            </div>
        `;

        // Add event listeners
        if (action) {
            notification.querySelector('.pwa-notification-action').addEventListener('click', action);
        }

        notification.querySelector('.pwa-notification-close').addEventListener('click', () => {
            notification.remove();
        });

        // Auto-remove after duration
        if (duration > 0) {
            setTimeout(() => {
                if (notification.parentElement) {
                    notification.remove();
                }
            }, duration);
        }

        return notification;
    }

    createInstallButton(deferredPrompt) {
        const button = document.createElement('button');
        button.id = 'pwa-install-button';
        button.className = 'pwa-install-btn';
        button.innerHTML = '📱 Install App';
        
        button.addEventListener('click', async () => {
            if (deferredPrompt) {
                deferredPrompt.prompt();
                const result = await deferredPrompt.userChoice;
                console.log('Install prompt result:', result);
                
                if (result.outcome === 'accepted') {
                    console.log('User accepted the install prompt');
                } else {
                    console.log('User dismissed the install prompt');
                }
                
                deferredPrompt = null;
                button.remove();
            }
        });

        return button;
    }

    showNotification(notification) {
        // Remove existing notifications of the same type
        const existingNotifications = document.querySelectorAll('.pwa-notification');
        existingNotifications.forEach(existing => {
            if (existing.className === notification.className) {
                existing.remove();
            }
        });

        // Add to page
        document.body.appendChild(notification);

        // Animate in
        setTimeout(() => {
            notification.classList.add('pwa-notification-show');
        }, 100);
    }

    addToUI(element) {
        // Add to a specific container or append to body
        const container = document.querySelector('.pwa-controls') || document.body;
        container.appendChild(element);
    }

    // Public API methods
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

    async unregister() {
        if (this.registration) {
            try {
                await this.registration.unregister();
                console.log('Service Worker unregistered');
                this.registration = null;
            } catch (error) {
                console.error('Service Worker unregistration failed:', error);
            }
        }
    }

    isInstallable() {
        return 'serviceWorker' in navigator && 'PushManager' in window;
    }

    isInstalled() {
        return window.matchMedia('(display-mode: standalone)').matches ||
               window.navigator.standalone === true;
    }

    getInstallationStatus() {
        return {
            isInstallable: this.isInstallable(),
            isInstalled: this.isInstalled(),
            hasServiceWorker: !!this.registration,
            updateAvailable: this.updateAvailable
        };
    }

    // Static method to get singleton instance
    static getInstance() {
        if (!PWAManager.instance) {
            new PWAManager();
        }
        return PWAManager.instance;
    }
}

// CSS for PWA notifications and install button
const pwaStyles = `
    .pwa-notification {
        position: fixed;
        top: 20px;
        right: 20px;
        background: #fff;
        border: 1px solid #ddd;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        z-index: 10000;
        max-width: 350px;
        opacity: 0;
        transform: translateX(100%);
        transition: all 0.3s ease;
    }

    .pwa-notification-show {
        opacity: 1;
        transform: translateX(0);
    }

    .pwa-notification-content {
        padding: 16px;
    }

    .pwa-notification h4 {
        margin: 0 0 8px 0;
        color: #333;
        font-size: 16px;
    }

    .pwa-notification p {
        margin: 0 0 12px 0;
        color: #666;
        font-size: 14px;
    }

    .pwa-notification-action {
        background: #007bff;
        color: white;
        border: none;
        padding: 8px 16px;
        border-radius: 4px;
        cursor: pointer;
        font-size: 14px;
        margin-right: 8px;
    }

    .pwa-notification-action:hover {
        background: #0056b3;
    }

    .pwa-notification-close {
        background: none;
        border: none;
        font-size: 18px;
        cursor: pointer;
        color: #999;
        padding: 4px;
        position: absolute;
        top: 8px;
        right: 8px;
    }

    .pwa-notification-update {
        border-left: 4px solid #007bff;
    }

    .pwa-notification-success {
        border-left: 4px solid #28a745;
    }

    .pwa-install-btn {
        background: #007bff;
        color: white;
        border: none;
        padding: 12px 24px;
        border-radius: 6px;
        cursor: pointer;
        font-size: 14px;
        font-weight: 500;
        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        transition: all 0.2s ease;
        position: fixed;
        bottom: 20px;
        right: 20px;
        z-index: 9999;
    }

    .pwa-install-btn:hover {
        background: #0056b3;
        transform: translateY(-1px);
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
    }

    @media (max-width: 768px) {
        .pwa-notification {
            left: 20px;
            right: 20px;
            max-width: none;
        }
        
        .pwa-install-btn {
            left: 20px;
            right: 20px;
            bottom: 20px;
        }
    }
`;

// Inject styles only once
if (!document.getElementById('pwa-styles')) {
    const styleSheet = document.createElement('style');
    styleSheet.id = 'pwa-styles';
    styleSheet.textContent = pwaStyles;
    document.head.appendChild(styleSheet);
}

// Initialize PWA Manager singleton - safe to call multiple times
if (!window.pwaManager) {
    window.pwaManager = PWAManager.getInstance();
}

// Export for use in Blazor components
window.PWAManager = PWAManager;
