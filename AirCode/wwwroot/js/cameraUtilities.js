// wwwroot/js/cameraUtilities.js
// Consolidated Camera and Scanner Utilities - Focus on Quality over Performance

(function() {
    'use strict';

    // Main Camera Utilities
    window.CameraUtilities = {
        
        // Get high-quality constraints - prioritize clarity
        getHighQualityConstraints: function() {
            return {
                video: {
                    // High resolution for better QR scanning
                    width: { ideal: 1920, min: 1280 },
                    height: { ideal: 1080, min: 720 },
                    frameRate: { ideal: 30 },
                    facingMode: 'environment',
                    
                    // Focus on image quality
                    focusMode: 'continuous',
                    exposureMode: 'continuous',
                    whiteBalanceMode: 'continuous',
                    zoom: true // Enable zoom if supported
                }
            };
        },

        // Android-specific high-quality constraints
        getAndroidQualityConstraints: function() {
            const isAndroid = /Android/i.test(navigator.userAgent);
            
            if (!isAndroid) {
                return this.getHighQualityConstraints();
            }

            // Android optimized for quality, not performance
            return {
                video: {
                    width: { ideal: 1920, min: 1280 },
                    height: { ideal: 1080, min: 720 },
                    frameRate: { ideal: 30 },
                    facingMode: 'environment',
                    
                    // Android-specific quality settings
                    focusMode: 'continuous',
                    exposureMode: 'continuous',
                    whiteBalanceMode: 'continuous',
                    torch: false,
                    zoom: true
                }
            };
        },

        // Enhanced getUserMedia with quality fallbacks
        getQualityCameraStream: async function() {
            const isAndroid = /Android/i.test(navigator.userAgent);
            const primaryConstraints = isAndroid ? 
                this.getAndroidQualityConstraints() : 
                this.getHighQualityConstraints();
            
            try {
                console.log('Attempting high-quality camera stream...');
                return await navigator.mediaDevices.getUserMedia(primaryConstraints);
            } catch (error) {
                console.warn('High-quality constraints failed, trying medium quality:', error);
                
                // Medium quality fallback
                try {
                    const mediumConstraints = {
                        video: {
                            width: { ideal: 1280, min: 854 },
                            height: { ideal: 720, min: 480 },
                            frameRate: { ideal: 30 },
                            facingMode: 'environment',
                            focusMode: 'continuous'
                        }
                    };
                    return await navigator.mediaDevices.getUserMedia(mediumConstraints);
                } catch (fallbackError) {
                    console.warn('Medium quality failed, trying basic:', fallbackError);
                    
                    // Basic fallback
                    const basicConstraints = {
                        video: {
                            facingMode: 'environment'
                        }
                    };
                    return await navigator.mediaDevices.getUserMedia(basicConstraints);
                }
            }
        },

        // Optimize video element for quality (not performance)
        optimizeVideoForQuality: function(videoElement) {
            if (!videoElement) {
                console.error('No video element provided');
                return false;
            }

            try {
                // Essential video attributes
                videoElement.setAttribute('playsinline', 'true');
                videoElement.setAttribute('webkit-playsinline', 'true');
                videoElement.muted = true;
                videoElement.autoplay = true;

                // Quality-focused CSS
                Object.assign(videoElement.style, {
                    // Center the video properly
                    position: 'absolute',
                    top: '50%',
                    left: '50%',
                    transform: 'translate(-50%, -50%)',
                    
                    // Fill container while maintaining aspect ratio
                    minWidth: '100%',
                    minHeight: '100%',
                    width: 'auto',
                    height: 'auto',
                    objectFit: 'cover',
                    
                    // Image quality settings - prioritize sharpness
                    imageRendering: 'crisp-edges',
                    filter: 'none', // Remove any blur
                    
                    // Basic GPU acceleration without overdoing it
                    willChange: 'transform',
                    backfaceVisibility: 'hidden'
                });

                console.log('Video element optimized for quality');
                return true;
            } catch (error) {
                console.error('Video optimization failed:', error);
                return false;
            }
        },

        // Zoom functionality
        applyZoom: function(videoElement, zoomLevel) {
            if (!videoElement) {
                console.error('No video element for zoom');
                return false;
            }

            try {
                const clampedZoom = Math.max(1.0, Math.min(3.0, zoomLevel));
                
                videoElement.style.transform = `translate(-50%, -50%) scale(${clampedZoom})`;
                
                console.log(`Zoom applied: ${clampedZoom}x`);
                return true;
            } catch (error) {
                console.error('Zoom application failed:', error);
                return false;
            }
        },

        // Check if device supports zoom
        supportsZoom: async function() {
            try {
                const stream = await navigator.mediaDevices.getUserMedia({ video: true });
                const track = stream.getVideoTracks()[0];
                const capabilities = track.getCapabilities();
                
                // Clean up
                track.stop();
                
                return !!(capabilities.zoom || capabilities.focusDistance);
            } catch (error) {
                console.warn('Zoom support check failed:', error);
                return true; // Assume it works and let CSS handle it
            }
        },

        // Initialize camera with quality focus
        initializeQualityCamera: async function(videoElement) {
            if (!videoElement) {
                throw new Error('Video element is required');
            }

            try {
                console.log('Initializing quality camera...');
                
                // Get high-quality stream
                const stream = await this.getQualityCameraStream();
                
                // Set up video element
                videoElement.srcObject = stream;
                this.optimizeVideoForQuality(videoElement);
                
                // Wait for video to be ready
                return new Promise((resolve, reject) => {
                    const timeout = setTimeout(() => {
                        reject(new Error('Camera initialization timeout'));
                    }, 10000);

                    videoElement.onloadedmetadata = () => {
                        clearTimeout(timeout);
                        
                        videoElement.play()
                            .then(() => {
                                console.log('Quality camera initialized successfully');
                                console.log(`Video dimensions: ${videoElement.videoWidth}x${videoElement.videoHeight}`);
                                resolve({
                                    stream: stream,
                                    width: videoElement.videoWidth,
                                    height: videoElement.videoHeight,
                                    supportsZoom: this.supportsZoom()
                                });
                            })
                            .catch(reject);
                    };

                    videoElement.onerror = (error) => {
                        clearTimeout(timeout);
                        reject(new Error('Video element error: ' + error.message));
                    };
                });

            } catch (error) {
                console.error('Camera initialization failed:', error);
                throw error;
            }
        },

        // Clean up camera resources
        cleanup: function(videoElement, stream) {
            try {
                if (stream) {
                    stream.getTracks().forEach(track => {
                        track.stop();
                    });
                }

                if (videoElement) {
                    videoElement.srcObject = null;
                    videoElement.load(); // Reset video element
                }

                console.log('Camera cleanup completed');
            } catch (error) {
                console.error('Cleanup failed:', error);
            }
        },

        // Performance monitoring (simplified)
        createSimplePerformanceMonitor: function(videoElement) {
            let frameCount = 0;
            let lastTime = Date.now();
            let currentFPS = 0;

            return {
                update: function() {
                    frameCount++;
                    const now = Date.now();
                    
                    if (now - lastTime >= 1000) {
                        currentFPS = Math.round(frameCount * 1000 / (now - lastTime));
                        frameCount = 0;
                        lastTime = now;
                    }
                },
                
                getFPS: function() {
                    return currentFPS;
                },
                
                getVideoInfo: function() {
                    if (!videoElement) return null;
                    
                    return {
                        width: videoElement.videoWidth,
                        height: videoElement.videoHeight,
                        fps: currentFPS
                    };
                }
            };
        }
    };

    // Reactor Blazor QR Code Scanner Integration
    window.ReactorQRScannerEnhanced = {
        
        // Override the default getUserMedia for quality
        enhanceReactorScanner: function() {
            // Check if Reactor QR Scanner is available
            if (window.ReactorQRScanner) {
                console.log('Enhancing Reactor QR Scanner for quality...');
                
                // Store original getUserMedia
                const originalGetUserMedia = window.ReactorQRScanner.getUserMedia;
                
                // Override with our quality-focused version
                window.ReactorQRScanner.getUserMedia = async function(constraints) {
                    console.log('Using enhanced camera constraints for Reactor Scanner');
                    
                    try {
                        // Use our quality constraints instead
                        return await window.CameraUtilities.getQualityCameraStream();
                    } catch (error) {
                        console.warn('Enhanced constraints failed, falling back to original:', error);
                        // Fall back to original if ours fail
                        if (originalGetUserMedia) {
                            return await originalGetUserMedia.call(this, constraints);
                        }
                        throw error;
                    }
                };
                
                return true;
            } else {
                console.warn('Reactor QR Scanner not found - enhancement skipped');
                return false;
            }
        },

        // Apply quality optimizations to Reactor scanner video
        optimizeReactorVideo: function() {
            // Wait a bit for the video element to be created
            setTimeout(() => {
                const video = document.querySelector('.video-container video') || 
                            document.querySelector('video');
                
                if (video) {
                    console.log('Optimizing Reactor scanner video for quality...');
                    window.CameraUtilities.optimizeVideoForQuality(video);
                } else {
                    console.warn('Reactor scanner video element not found');
                }
            }, 500);
        }
    };

    // Auto-initialization
    function initialize() {
        console.log('Camera Utilities loaded - prioritizing quality over performance');
        
        // Enhance Reactor QR Scanner if available
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                setTimeout(() => {
                    window.ReactorQRScannerEnhanced.enhanceReactorScanner();
                    window.ReactorQRScannerEnhanced.optimizeReactorVideo();
                }, 1000);
            });
        } else {
            setTimeout(() => {
                window.ReactorQRScannerEnhanced.enhanceReactorScanner();
                window.ReactorQRScannerEnhanced.optimizeReactorVideo();
            }, 1000);
        }
    }

    // Initialize when script loads
    initialize();

})();
