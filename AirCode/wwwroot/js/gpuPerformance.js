// wwwroot/js/gpuPerformance.js
console.log("gpuPerformance.js loaded");

window.enableGPUAcceleration = function(element) {
    if (!element) return;

    // Apply GPU acceleration styles
    element.style.transform = 'translateZ(0)';
    element.style.backfaceVisibility = 'hidden';
    element.style.perspective = '1000px';
    element.style.willChange = 'transform, opacity';

    // Enable hardware acceleration for children
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
        // Use transform instead of changing scroll position directly
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

// Enhanced connectivity checker with performance optimizations
window.connectivityChecker = {
    ...window.connectivityChecker,

    // Override the existing checkConnectivity with optimized version
    checkConnectivity: window.debounce(function() {
        const currentStatus = navigator.onLine;
        if (this.isOnline !== currentStatus) {
            console.log("checkConnectivity: status changed from", this.isOnline, "to", currentStatus);
            this.isOnline = currentStatus;
            this.notifyDotNet(currentStatus);
        }
    }, 1000), // Debounce to prevent excessive calls

    // Add method to optimize network requests
    optimizeForConnection: function() {
        if (this.isOnline) {
            // Online optimizations
            document.documentElement.classList.add('online-mode');
            document.documentElement.classList.remove('offline-mode');
        } else {
            // Offline optimizations
            document.documentElement.classList.add('offline-mode');
            document.documentElement.classList.remove('online-mode');
        }
    }
};
