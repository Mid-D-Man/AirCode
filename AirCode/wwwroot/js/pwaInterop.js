// PWA JavaScript Interop Helper
// Place this in wwwroot/js/pwaInterop.js

// Global functions for Blazor interop
window.getPWAManager = () => {
    return window.pwaManager;
};

window.setupPWAStatusMonitoring = (dotNetRef) => {
    if (window.pwaManager) {
        // Monitor for PWA status changes
        const originalShowUpdateAvailable = window.pwaManager.showUpdateAvailable;
        const originalShowInstallSuccess = window.pwaManager.showInstallSuccess;
        
        window.pwaManager.showUpdateAvailable = function() {
            originalShowUpdateAvailable.call(this);
            dotNetRef.invokeMethodAsync('OnUpdateAvailable');
        };
        
        window.pwaManager.showInstallSuccess = function() {
            originalShowInstallSuccess.call(this);
            dotNetRef.invokeMethodAsync('OnAppInstalled');
        };
        
        // Set up periodic status checks
        setInterval(() => {
            const status = window.pwaManager.getInstallationStatus();
            dotNetRef.invokeMethodAsync('OnPWAStatusChanged', status);
        }, 30000); // Check every 30 seconds
    }
};

// Enhanced PWA utilities
window.pwaUtils = {
    // Check if running in PWA mode
    isPWA: () => {
        return window.matchMedia('(display-mode: standalone)').matches ||
               window.navigator.standalone === true;
    },
    
    // Get PWA display mode
    getDisplayMode: () => {
        const displayModes = ['fullscreen', 'standalone', 'minimal-ui', 'browser'];
        return displayModes.find(mode => window.matchMedia(`(display-mode: ${mode})`).matches) || 'browser';
    },
    
    // Share API support
    canShare: () => {
        return 'share' in navigator;
    },
    
    // Share content
    share: async (data) => {
        if ('share' in navigator) {
            try {
                await navigator.share(data);
                return { success: true };
            } catch (error) {
                return { success: false, error: error.message };
            }
        }
        return { success: false, error: 'Share API not supported' };
    },
    
    // Get network status
    getNetworkStatus: () => {
        return {
            online: navigator.onLine,
            connection: navigator.connection ? {
                effectiveType: navigator.connection.effectiveType,
                downlink: navigator.connection.downlink,
                rtt: navigator.connection.rtt
            } : null
        };
    },
    
    // Battery API support
    getBatteryStatus: async () => {
        if ('getBattery' in navigator) {
            try {
                const battery = await navigator.getBattery();
                return {
                    charging: battery.charging,
                    level: battery.level,
                    chargingTime: battery.chargingTime,
                    dischargingTime: battery.dischargingTime
                };
            } catch (error) {
                return { error: error.message };
            }
        }
        return { error: 'Battery API not supported' };
    },
    
    // Vibration API
    vibrate: (pattern) => {
        if ('vibrate' in navigator) {
            return navigator.vibrate(pattern);
        }
        return false;
    },
    
    // Notification permission
    getNotificationPermission: () => {
        return 'Notification' in window ? Notification.permission : 'denied';
    },
    
    // Request notification permission
    requestNotificationPermission: async () => {
        if ('Notification' in window) {
            const permission = await Notification.requestPermission();
            return permission;
        }
        return 'denied';
    },
    
    // Show notification
    showNotification: (title, options) => {
        if ('Notification' in window && Notification.permission === 'granted') {
            return new Notification(title, options);
        }
        return null;
    },
    
    // Cache management
    clearCache: async () => {
        if ('caches' in window) {
            try {
                const cacheNames = await caches.keys();
                await Promise.all(cacheNames.map(name => caches.delete(name)));
                return { success: true, cleared: cacheNames.length };
            } catch (error) {
                return { success: false, error: error.message };
            }
        }
        return { success: false, error: 'Cache API not supported' };
    },
    
    // Get cache size
    getCacheSize: async () => {
        if ('caches' in window && 'storage' in navigator && 'estimate' in navigator.storage) {
            try {
                const estimate = await navigator.storage.estimate();
                return {
                    usage: estimate.usage,
                    quota: estimate.quota,
                    usageDetails: estimate.usageDetails
                };
            } catch (error) {
                return { error: error.message };
            }
        }
        return { error: 'Storage API not supported' };
    }
};

// Network status monitoring
window.setupNetworkMonitoring = (dotNetRef) => {
    const updateNetworkStatus = () => {
        const status = window.pwaUtils.getNetworkStatus();
        dotNetRef.invokeMethodAsync('OnNetworkStatusChanged', status);
    };
    
    window.addEventListener('online', updateNetworkStatus);
    window.addEventListener('offline', updateNetworkStatus);
    
    // Connection change events
    if (navigator.connection) {
        navigator.connection.addEventListener('change', updateNetworkStatus);
    }
    
    // Return cleanup function
    return () => {
        window.removeEventListener('online', updateNetworkStatus);
        window.removeEventListener('offline', updateNetworkStatus);
        if (navigator.connection) {
            navigator.connection.removeEventListener('change', updateNetworkStatus);
        }
    };
};

// PWA lifecycle events
window.setupPWALifecycleEvents = (dotNetRef) => {
    // App visibility change
    document.addEventListener('visibilitychange', () => {
        dotNetRef.invokeMethodAsync('OnVisibilityChange', !document.hidden);
    });
    
    // App focus/blur
    window.addEventListener('focus', () => {
        dotNetRef.invokeMethodAsync('OnAppFocus');
    });
    
    window.addEventListener('blur', () => {
        dotNetRef.invokeMethodAsync('OnAppBlur');
    });
    
    // Page freeze/resume (mobile)
    window.addEventListener('freeze', () => {
        dotNetRef.invokeMethodAsync('OnAppFreeze');
    });
    
    window.addEventListener('resume', () => {
        dotNetRef.invokeMethodAsync('OnAppResume');
    });
    
    // Before unload
    window.addEventListener('beforeunload', (e) => {
        dotNetRef.invokeMethodAsync('OnBeforeUnload');
    });
};

// Initialize PWA features when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    console.log('PWA Interop initialized');
    
    // Log PWA capabilities
    console.log('PWA Capabilities:', {
        serviceWorker: 'serviceWorker' in navigator,
        pushManager: 'PushManager' in window,
        notifications: 'Notification' in window,
        share: 'share' in navigator,
        battery: 'getBattery' in navigator,
        vibration: 'vibrate' in navigator,
        cache: 'caches' in window,
        storage: 'storage' in navigator
    });
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { pwaUtils: window.pwaUtils };
}
