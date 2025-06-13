// wwwroot/js/gpuPerformance.js
console.log("gpuPerformance.js loaded");

window.enableGPUAcceleration = function(element) {
    if (!element) return;

    element.style.transform = 'translateZ(0)';
    element.style.backfaceVisibility = 'hidden';
    element.style.perspective = '1000px';
    element.style.willChange = 'transform, opacity';

    const children = element.querySelectorAll('*');
    children.forEach(child => {
        child.style.transform = 'translateZ(0)';
        child.style.backfaceVisibility = 'hidden';
    });
};

window.optimizeScrolling = function(element) {
    if (!element) return;

    let ticking = false;

    function updateScrollPosition() {
        element.style.willChange = 'scroll-position';
        ticking = false;
    }

    element.addEventListener('scroll', function() {
        if (!ticking) {
            requestAnimationFrame(updateScrollPosition);
            ticking = true;
        }
    });
};

window.debounce = function(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
};

// Connectivity checker with proper initialization
window.connectivityChecker = {
    isOnline: navigator.onLine,
    dotNetRef: null,

    init: function(dotNetRef) {
        console.log("connectivityChecker.init called");
        this.dotNetRef = dotNetRef;
        this.isOnline = navigator.onLine;

        // Add event listeners
        window.addEventListener('online', () => this.handleConnectivityChange(true));
        window.addEventListener('offline', () => this.handleConnectivityChange(false));

        // Initial check
        this.optimizeForConnection();
    },

    handleConnectivityChange: function(isOnline) {
        console.log("Connectivity changed to:", isOnline);
        this.isOnline = isOnline;
        this.optimizeForConnection();
        this.notifyDotNet(isOnline);
    },

    notifyDotNet: function(isOnline) {
        if (this.dotNetRef) {
            try {
                this.dotNetRef.invokeMethodAsync('OnConnectivityChanged', isOnline);
            } catch (error) {
                console.error("Error notifying .NET:", error);
            }
        }
    },

    getOnlineStatus: function() {
        return this.isOnline;
    },

    optimizeForConnection: function() {
        if (this.isOnline) {
            document.documentElement.classList.add('online-mode');
            document.documentElement.classList.remove('offline-mode');
        } else {
            document.documentElement.classList.add('offline-mode');
            document.documentElement.classList.remove('online-mode');
        }
    },

    dispose: function() {
        window.removeEventListener('online', this.handleConnectivityChange);
        window.removeEventListener('offline', this.handleConnectivityChange);
        this.dotNetRef = null;
    }
};