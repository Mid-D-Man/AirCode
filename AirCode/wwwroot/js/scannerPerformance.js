// wwwroot/js/scannerPerformance.js
// Enhanced Performance Optimization for QR Scanner

(function() {
    'use strict';

    // Performance optimization utilities
    window.ScannerPerformance = {
        
        // Device detection and optimization
        detectDevice: function() {
            const userAgent = navigator.userAgent || navigator.vendor || window.opera;
            const devicePixelRatio = window.devicePixelRatio || 1;
            const isMobile = /android|iphone|ipad|ipod|blackberry|iemobile|opera mini/i.test(userAgent);
            const isLowEnd = devicePixelRatio <= 1.5 && (
                /android.*4\./i.test(userAgent) || 
                performance.memory?.totalJSHeapSize < 100000000
            );

            return {
                isMobile,
                isLowEnd,
                devicePixelRatio,
                supportsOffscreenCanvas: typeof OffscreenCanvas !== 'undefined',
                supportsWebGL: !!window.WebGLRenderingContext,
                cores: navigator.hardwareConcurrency || 2
            };
        },

        // Optimize video element for scanning
        optimizeVideoElement: function(video) {
            if (!video) return false;

            try {
                const device = this.detectDevice();
                
                // Set optimal video attributes
                video.setAttribute('playsinline', 'true');
                video.setAttribute('webkit-playsinline', 'true');
                video.muted = true;
                video.autoplay = true;
                
                // Apply CSS optimizations
                Object.assign(video.style, {
                    transform: 'translate3d(-50%, -50%, 0)',
                    willChange: 'transform',
                    backfaceVisibility: 'hidden',
                    imageRendering: device.isLowEnd ? 'pixelated' : 'auto',
                    filter: device.isLowEnd ? 'contrast(1.1) brightness(1.05)' : 'none'
                });

                // Request optimal frame rate
                if (video.requestVideoFrameCallback) {
                    const targetFPS = device.isLowEnd ? 15 : 30;
                    let frameCount = 0;
                    let lastTime = performance.now();

                    const frameCallback = (now) => {
                        frameCount++;
                        if (now - lastTime >= 1000) {
                            const currentFPS = Math.round(frameCount * 1000 / (now - lastTime));
                            if (currentFPS > targetFPS * 1.5) {
                                // Skip frames if running too fast
                                setTimeout(() => {
                                    video.requestVideoFrameCallback(frameCallback);
                                }, 1000 / targetFPS);
                            } else {
                                video.requestVideoFrameCallback(frameCallback);
                            }
                            frameCount = 0;
                            lastTime = now;
                        } else {
                            video.requestVideoFrameCallback(frameCallback);
                        }
                    };
                    video.requestVideoFrameCallback(frameCallback);
                }

                return true;
            } catch (error) {
                console.error('Video optimization failed:', error);
                return false;
            }
        },

        // Get optimal camera constraints based on device
        getOptimalConstraints: function() {
            const device = this.detectDevice();
            
            if (device.isLowEnd) {
                return {
                    video: {
                        width: { ideal: 640, max: 854 },
                        height: { ideal: 480, max: 640 },
                        frameRate: { ideal: 15, max: 24 },
                        facingMode: 'environment',
                        focusMode: 'continuous'
                    }
                };
            } else if (device.isMobile) {
                return {
                    video: {
                        width: { ideal: 1280, max: 1920 },
                        height: { ideal: 720, max: 1080 },
                        frameRate: { ideal: 30, max: 30 },
                        facingMode: 'environment',
                        focusMode: 'continuous',
                        torch: false
                    }
                };
            } else {
                return {
                    video: {
                        width: { ideal: 1920, max: 3840 },
                        height: { ideal: 1080, max: 2160 },
                        frameRate: { ideal: 30, max: 60 },
                        facingMode: 'environment'
                    }
                };
            }
        },

        // Memory management for canvas operations
        optimizeCanvasMemory: function(canvas) {
            if (!canvas) return false;

            try {
                const ctx = canvas.getContext('2d');
                if (!ctx) return false;

                // Enable image smoothing optimizations
                ctx.imageSmoothingEnabled = false;
                ctx.imageSmoothingQuality = 'low';
                
                // Memory cleanup method
                canvas._cleanup = function() {
                    const imageData = ctx.createImageData(1, 1);
                    ctx.putImageData(imageData, 0, 0);
                    ctx.clearRect(0, 0, canvas.width, canvas.height);
                };

                return true;
            } catch (error) {
                console.error('Canvas optimization failed:', error);
                return false;
            }
        },

        // Throttle function for performance-critical operations
        throttle: function(func, limit) {
            let inThrottle;
            return function() {
                const args = arguments;
                const context = this;
                if (!inThrottle) {
                    func.apply(context, args);
                    inThrottle = true;
                    setTimeout(() => inThrottle = false, limit);
                }
            };
        },

        // Debounce function for UI updates
        debounce: function(func, wait, immediate) {
            let timeout;
            return function() {
                const context = this;
                const args = arguments;
                const later = function() {
                    timeout = null;
                    if (!immediate) func.apply(context, args);
                };
                const callNow = immediate && !timeout;
                clearTimeout(timeout);
                timeout = setTimeout(later, wait);
                if (callNow) func.apply(context, args);
            };
        },

        // Performance monitoring
        createPerformanceMonitor: function() {
            let frameCount = 0;
            let lastTime = performance.now();
            let fps = 0;

            return {
                tick: function() {
                    frameCount++;
                    const now = performance.now();
                    if (now - lastTime >= 1000) {
                        fps = Math.round(frameCount * 1000 / (now - lastTime));
                        frameCount = 0;
                        lastTime = now;
                    }
                    return fps;
                },
                getFPS: function() {
                    return fps;
                },
                getMemoryUsage: function() {
                    if (performance.memory) {
                        return {
                            used: Math.round(performance.memory.usedJSHeapSize / 1048576) + ' MB',
                            total: Math.round(performance.memory.totalJSHeapSize / 1048576) + ' MB',
                            limit: Math.round(performance.memory.jsHeapSizeLimit / 1048576) + ' MB'
                        };
                    }
                    return null;
                }
            };
        },

        // Initialize all optimizations
        initialize: function() {
            console.log('Scanner Performance module initialized');
            
            // Enable passive event listeners where possible
            if ('passive' in document.addEventListener.options) {
                ['scroll', 'touchstart', 'touchmove', 'wheel'].forEach(event => {
                    document.addEventListener(event, () => {}, { passive: true });
                });
            }

            // Optimize for 60fps where possible
            if (window.requestIdleCallback) {
                window.requestIdleCallback(() => {
                    // Non-critical optimizations in idle time
                    this.preloadOptimizations();
                });
            }
        },

        // Preload optimizations during idle time
        preloadOptimizations: function() {
            // Pre-create canvas context for better performance
            const canvas = document.createElement('canvas');
            canvas.width = 640;
            canvas.height = 480;
            const ctx = canvas.getContext('2d');
            
            // Warm up the rendering pipeline
            ctx.fillRect(0, 0, 1, 1);
            ctx.clearRect(0, 0, 640, 480);
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.ScannerPerformance.initialize();
        });
    } else {
        window.ScannerPerformance.initialize();
    }

})();
