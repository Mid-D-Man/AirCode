// wwwroot/js/androidCameraOptimization.js
// Targeted Android Performance Fixes

(function() {
    'use strict';

    window.AndroidCameraFixes = {
        
        // Device-specific constraints based on user agent
        getAndroidOptimalConstraints: function() {
            const isAndroid = /Android/i.test(navigator.userAgent);
            const isLowEndAndroid = /Android [4-6]/i.test(navigator.userAgent);
            
            if (!isAndroid) {
                return this.getDefaultConstraints();
            }

            // Android-specific optimizations
            const baseConstraints = {
                video: {
                    // Force exact constraints for Android consistency
                    width: { exact: 640 },
                    height: { exact: 480 },
                    frameRate: { exact: 15 }, // Critical for Android performance
                    facingMode: 'environment',
                    
                    // Android-specific camera settings
                    focusMode: 'continuous',
                    exposureMode: 'continuous',
                    whiteBalanceMode: 'continuous',
                    
                    // Reduce processing overhead
                    aspectRatio: { exact: 4/3 },
                    resizeMode: 'none'
                }
            };

            // Further reduce for low-end Android
            if (isLowEndAndroid) {
                baseConstraints.video.width = { exact: 480 };
                baseConstraints.video.height = { exact: 360 };
                baseConstraints.video.frameRate = { exact: 10 };
            }

            return baseConstraints;
        },

        // Apply Android-specific video element optimizations
        optimizeAndroidVideo: function(videoElement) {
            if (!videoElement || !/Android/i.test(navigator.userAgent)) {
                return false;
            }

            try {
                // Critical Android video attributes
                videoElement.setAttribute('playsinline', 'true');
                videoElement.setAttribute('webkit-playsinline', 'true');
                videoElement.setAttribute('x5-playsinline', 'true'); // Tencent X5 browser
                videoElement.muted = true;
                videoElement.autoplay = true;
                
                // Android-specific CSS optimizations
                Object.assign(videoElement.style, {
                    // Hardware acceleration
                    transform: 'translate3d(-50%, -50%, 0) scale(1)',
                    willChange: 'transform',
                    backfaceVisibility: 'hidden',
                    
                    // Image quality for Android
                    imageRendering: 'optimizeSpeed',
                    filter: 'contrast(1.15) brightness(1.1) saturate(1.1)',
                    
                    // Force GPU compositing
                    webkitTransform: 'translate3d(-50%, -50%, 0)',
                    webkitBackfaceVisibility: 'hidden',
                    
                    // Remove Android blur
                    webkitFilter: 'blur(0px)',
                    msFilter: 'blur(0px)',
                    filter: 'blur(0px) contrast(1.15) brightness(1.1)'
                });

                return true;
            } catch (error) {
                console.error('Android video optimization failed:', error);
                return false;
            }
        },

        // Enhanced getUserMedia with Android fallbacks
        getAndroidCameraStream: async function(preferredConstraints) {
            const constraints = preferredConstraints || this.getAndroidOptimalConstraints();
            
            try {
                // Primary attempt with optimal constraints
                return await navigator.mediaDevices.getUserMedia(constraints);
            } catch (error) {
                console.warn('Primary Android constraints failed, trying fallbacks:', error);
                
                // Fallback 1: Simplified constraints
                try {
                    const fallback1 = {
                        video: {
                            width: 640,
                            height: 480,
                            frameRate: 15,
                            facingMode: 'environment'
                        }
                    };
                    return await navigator.mediaDevices.getUserMedia(fallback1);
                } catch (fallbackError1) {
                    console.warn('Fallback 1 failed:', fallbackError1);
                    
                    // Fallback 2: Minimal constraints
                    try {
                        const fallback2 = {
                            video: {
                                facingMode: 'environment'
                            }
                        };
                        return await navigator.mediaDevices.getUserMedia(fallback2);
                    } catch (fallbackError2) {
                        console.error('All Android camera constraints failed:', fallbackError2);
                        throw new Error('Camera access denied or unavailable');
                    }
                }
            }
        },

        // Android-specific performance monitoring
        createAndroidPerformanceMonitor: function(videoElement) {
            if (!videoElement) return null;

            let frameCount = 0;
            let lastTime = performance.now();
            let qualityMetrics = {
                fps: 0,
                dropped: 0,
                decoded: 0
            };

            const monitor = {
                update: function() {
                    const now = performance.now();
                    if (now - lastTime >= 1000) {
                        // Get Android-specific video metrics
                        if (videoElement.getVideoPlaybackQuality) {
                            const quality = videoElement.getVideoPlaybackQuality();
                            qualityMetrics.dropped = quality.droppedVideoFrames || 0;
                            qualityMetrics.decoded = quality.totalVideoFrames || 0;
                        }

                        qualityMetrics.fps = Math.round(frameCount * 1000 / (now - lastTime));
                        frameCount = 0;
                        lastTime = now;
                    } else {
                        frameCount++;
                    }
                },
                
                getMetrics: function() {
                    return qualityMetrics;
                },
                
                // Auto-adjust based on performance
                autoOptimize: function() {
                    const metrics = this.getMetrics();
                    
                    // If dropping frames, reduce quality
                    if (metrics.dropped > metrics.decoded * 0.1 && metrics.fps < 10) {
                        // Trigger quality reduction
                        return 'reduce_quality';
                    }
                    
                    return 'maintain';
                }
            };

            return monitor;
        },

        // Initialize all Android optimizations
        initializeAndroidOptimizations: function(videoElement) {
            if (!/Android/i.test(navigator.userAgent)) {
                console.log('Not Android device, skipping Android-specific optimizations');
                return false;
            }

            console.log('Applying Android camera optimizations...');
            
            const videoOptimized = this.optimizeAndroidVideo(videoElement);
            const monitor = this.createAndroidPerformanceMonitor(videoElement);
            
            // Performance monitoring loop
            if (monitor && videoElement) {
                const performanceLoop = () => {
                    monitor.update();
                    const optimization = monitor.autoOptimize();
                    
                    if (optimization === 'reduce_quality') {
                        // Apply quality reduction if needed
                        console.warn('Poor performance detected, reducing quality');
                    }
                    
                    requestAnimationFrame(performanceLoop);
                };
                
                requestAnimationFrame(performanceLoop);
            }

            return {
                videoOptimized,
                monitor,
                constraints: this.getAndroidOptimalConstraints()
            };
        },

        getDefaultConstraints: function() {
            return {
                video: {
                    width: { ideal: 1280, min: 640 },
                    height: { ideal: 720, min: 480 },
                    frameRate: { ideal: 30, max: 30 },
                    facingMode: 'environment'
                }
            };
        }
    };

})();

// Auto-initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        console.log('Android Camera Optimization module loaded');
    });
} else {
    console.log('Android Camera Optimization module loaded');
}
