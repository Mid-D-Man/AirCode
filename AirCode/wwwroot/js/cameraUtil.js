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
    }

    async initialize(videoElementId, canvasElementId, onScanCallback, options = {}) {
        try {
            this.onScanCallback = onScanCallback;
            this.scanDelay = options.scanDelay || 300;

            // Get DOM elements
            this.videoElement = document.getElementById(videoElementId);
            this.canvas = document.getElementById(canvasElementId) || this.createCanvas();
            this.context = this.canvas.getContext('2d');

            if (!this.videoElement) {
                throw new Error(`Video element with id '${videoElementId}' not found`);
            }

            // Configure video constraints
            const constraints = {
                video: {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    facingMode: options.preferredCamera || 'environment'
                }
            };

            // Request camera access with proper error handling
            this.stream = await navigator.mediaDevices.getUserMedia(constraints);

            // Set up video element
            this.videoElement.srcObject = this.stream;
            this.videoElement.setAttribute('playsinline', true);
            this.videoElement.muted = true;

            // Wait for video to be ready
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

                // Timeout fallback
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

    createCanvas() {
        const canvas = document.createElement('canvas');
        canvas.style.display = 'none';
        document.body.appendChild(canvas);
        return canvas;
    }

    startScanning() {
        if (this.isScanning || !this.videoElement || !this.context) {
            return false;
        }

        this.isScanning = true;
        this.scanFrame();
        return true;
    }

    stopScanning() {
        this.isScanning = false;
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
    }

    scanFrame() {
        if (!this.isScanning || !this.videoElement || this.videoElement.readyState !== 4) {
            return;
        }

        const now = Date.now();
        if (now - this.lastScanTime < this.scanDelay) {
            this.animationId = requestAnimationFrame(() => this.scanFrame());
            return;
        }

        try {
            // Set canvas size to match video
            const videoWidth = this.videoElement.videoWidth;
            const videoHeight = this.videoElement.videoHeight;

            if (videoWidth === 0 || videoHeight === 0) {
                this.animationId = requestAnimationFrame(() => this.scanFrame());
                return;
            }

            this.canvas.width = videoWidth;
            this.canvas.height = videoHeight;

            // Draw video frame to canvas
            this.context.drawImage(this.videoElement, 0, 0, videoWidth, videoHeight);

            // Get image data
            const imageData = this.context.getImageData(0, 0, videoWidth, videoHeight);

            // Scan for QR code using jsQR
            if (window.jsQR) {
                const code = jsQR(imageData.data, imageData.width, imageData.height);

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

        // Continue scanning
        this.animationId = requestAnimationFrame(() => this.scanFrame());
    }

    async switchCamera() {
        if (!this.stream) return false;

        try {
            const currentTrack = this.stream.getVideoTracks()[0];
            const currentFacingMode = currentTrack.getSettings().facingMode;

            await this.cleanup();

            const newFacingMode = currentFacingMode === 'environment' ? 'user' : 'environment';

            const constraints = {
                video: {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    facingMode: newFacingMode
                }
            };

            this.stream = await navigator.mediaDevices.getUserMedia(constraints);
            this.videoElement.srcObject = this.stream;

            await this.videoElement.play();
            return true;

        } catch (error) {
            console.error('Camera switch failed:', error);
            return false;
        }
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
        }
    }

    // Static method to check camera availability
    static async checkCameraAvailability() {
        try {
            const devices = await navigator.mediaDevices.enumerateDevices();
            const cameras = devices.filter(device => device.kind === 'videoinput');

            return {
                available: cameras.length > 0,
                count: cameras.length,
                devices: cameras.map(cam => ({
                    id: cam.deviceId,
                    label: cam.label || `Camera ${cam.deviceId.substring(0, 8)}...`
                }))
            };
        } catch (error) {
            console.error('Camera availability check failed:', error);
            return { available: false, count: 0, devices: [] };
        }
    }

    // Static method for permission checking
    static async checkCameraPermission() {
        try {
            const permission = await navigator.permissions.query({ name: 'camera' });
            return permission.state; // 'granted', 'denied', or 'prompt'
        } catch (error) {
            console.warn('Permission API not supported:', error);
            return 'unknown';
        }
    }
}

// Export for module use
if (typeof module !== 'undefined' && module.exports) {
    module.exports = CameraUtil;
}

// Global assignment for direct script inclusion
window.CameraUtil = CameraUtil;