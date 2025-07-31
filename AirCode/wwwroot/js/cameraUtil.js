class CameraUtil {
    constructor() {
        this.stream = null;
        this.videoElement = null;
        this.canvas = null;
        this.context = null;
        this.animationId = null;
        this.isScanning = false;
        this.onScanCallback = null;
        this.lastScanTime = 0;
        this.scanDelay = 300;
        // Performance optimizations
        this.offscreenCanvas = null;
        this.offscreenContext = null;
        this.imageDataCache = null;
        this.frameSkipCounter = 0;
        this.frameSkipInterval = 2; // Process every 3rd frame
    }

    async initialize(videoElementId, canvasElementId, onScanCallback, options = {}) {
        try {
            this.onScanCallback = onScanCallback;
            this.scanDelay = options.scanDelay || 300;
            this.frameSkipInterval = options.frameSkipInterval || 2;

            this.videoElement = document.getElementById(videoElementId);
            this.canvas = document.getElementById(canvasElementId) || this.createCanvas();
            this.context = this.canvas.getContext('2d');

            // Create offscreen canvas for better performance
            if (typeof OffscreenCanvas !== 'undefined') {
                this.offscreenCanvas = new OffscreenCanvas(640, 480);
                this.offscreenContext = this.offscreenCanvas.getContext('2d');
            }

            if (!this.videoElement) {
                throw new Error(`Video element with id '${videoElementId}' not found`);
            }

            const constraints = {
                video: {
                    width: { ideal: 1280, min: 640 },
                    height: { ideal: 720, min: 480 },
                    facingMode: options.preferredCamera || 'environment',
                    frameRate: { ideal: 30, max: 30 } // Limit frame rate
                }
            };

            this.stream = await navigator.mediaDevices.getUserMedia(constraints);
            this.videoElement.srcObject = this.stream;
            this.videoElement.setAttribute('playsinline', true);
            this.videoElement.muted = true;

            // Enable GPU acceleration
            if (window.enableGPUAcceleration) {
                window.enableGPUAcceleration(this.videoElement);
                window.enableGPUAcceleration(this.canvas);
            }

            return new Promise((resolve, reject) => {
                this.videoElement.onloadedmetadata = () => {
                    this.videoElement.play()
                        .then(() => {
                            console.log('Camera initialized successfully');
                            resolve();
                        })
                        .catch(reject);
                };

                this.videoElement.onerror = (error) => {
                    reject(new Error('Video element error: ' + error.message));
                };

                setTimeout(() => {
                    if (this.videoElement.readyState === 0) {
                        reject(new Error('Camera initialization timeout'));
                    }
                }, 5000);
            });

        } catch (error) {
            console.error('Camera initialization failed:', error);
            await this.cleanup();
            throw error;
        }
    }

    scanFrame() {
        if (!this.isScanning || !this.videoElement || this.videoElement.readyState !== 4) {
            return;
        }

        // Frame skipping for performance
        this.frameSkipCounter++;
        if (this.frameSkipCounter < this.frameSkipInterval) {
            this.animationId = requestAnimationFrame(() => this.scanFrame());
            return;
        }
        this.frameSkipCounter = 0;

        const now = Date.now();
        if (now - this.lastScanTime < this.scanDelay) {
            this.animationId = requestAnimationFrame(() => this.scanFrame());
            return;
        }

        try {
            const videoWidth = this.videoElement.videoWidth;
            const videoHeight = this.videoElement.videoHeight;

            if (videoWidth === 0 || videoHeight === 0) {
                this.animationId = requestAnimationFrame(() => this.scanFrame());
                return;
            }

            // Use smaller canvas for QR scanning to improve performance
            const scanWidth = Math.min(videoWidth, 640);
            const scanHeight = Math.min(videoHeight, 480);

            const targetCanvas = this.offscreenCanvas || this.canvas;
            const targetContext = this.offscreenContext || this.context;

            targetCanvas.width = scanWidth;
            targetCanvas.height = scanHeight;

            // Draw scaled down image for faster processing
            targetContext.drawImage(
                this.videoElement, 
                0, 0, videoWidth, videoHeight,
                0, 0, scanWidth, scanHeight
            );

            // Reuse image data object if possible
            if (!this.imageDataCache || 
                this.imageDataCache.width !== scanWidth || 
                this.imageDataCache.height !== scanHeight) {
                this.imageDataCache = targetContext.getImageData(0, 0, scanWidth, scanHeight);
            } else {
                targetContext.getImageData(0, 0, scanWidth, scanHeight, this.imageDataCache);
            }

            if (window.jsQR) {
                const code = jsQR(this.imageDataCache.data, scanWidth, scanHeight, {
                    inversionAttempts: "dontInvert" // Skip inversion for speed
                });

                if (code && code.data) {
                    this.lastScanTime = now;
                    if (this.onScanCallback) {
                        this.onScanCallback(code.data);
                    }
                }
            }

        } catch (error) {
            console.error('Scan frame error:', error);
        }

        this.animationId = requestAnimationFrame(() => this.scanFrame());
    }

    async cleanup() {
        this.stopScanning();

        if (this.stream) {
            this.stream.getTracks().forEach(track => {
                track.stop();
            });
            this.stream = null;
        }

        if (this.videoElement) {
            this.videoElement.srcObject = null;
            // Disable GPU acceleration
            if (window.disableGPUAcceleration) {
                window.disableGPUAcceleration(this.videoElement);
                window.disableGPUAcceleration(this.canvas);
            }
        }

        // Clean up cached resources
        this.imageDataCache = null;
        this.offscreenCanvas = null;
        this.offscreenContext = null;
    }

    // Rest of the methods remain the same...
            }
