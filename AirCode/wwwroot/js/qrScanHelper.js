// wwwroot/js/qrScanHelper.js - Fixed implementation
//  QR Scanner with proper error handling and camera switching
class QRScannerManager {
    constructor(videoId) {
        this.videoId = videoId;
        this.video = document.getElementById(videoId);
        this.qrScanner = null;
        this.isScanning = false;
        this.dotNetRef = null;
        this.cameras = [];
        this.currentCameraIndex = 0;
    }

    async init(dotNetObject) {
        this.dotNetRef = dotNetObject;

        if (!this.video) {
            console.error(`Video element ${this.videoId} not found`);
            return false;
        }

        try {
            // Get camera list BEFORE creating scanner
            this.cameras = await QrScanner.listCameras(true);
            console.log(`Found ${this.cameras.length} cameras:`, this.cameras);

            // Find environment camera
            const envCameraIndex = this.cameras.findIndex(cam =>
                cam.label.toLowerCase().includes('back') ||
                cam.label.toLowerCase().includes('environment')
            );
            this.currentCameraIndex = envCameraIndex >= 0 ? envCameraIndex : 0;

            this.qrScanner = new QrScanner(
                this.video,
                result => this.handleScanResult(result),
                {
                    preferredCamera: this.cameras[this.currentCameraIndex]?.id || 'environment',
                    highlightScanRegion: false,
                    highlightCodeOutline: false,
                    returnDetailedScanResult: true,
                    maxScansPerSecond: 5,
                    calculateScanRegion: (video) => {
                        const scanSize = Math.min(video.videoWidth, video.videoHeight) * 0.8;
                        return {
                            x: Math.round((video.videoWidth - scanSize) / 2),
                            y: Math.round((video.videoHeight - scanSize) / 2),
                            width: Math.round(scanSize),
                            height: Math.round(scanSize),
                        };
                    }
                }
            );

            return true;
        } catch (error) {
            console.error('QR Scanner init failed:', error);
            return false;
        }
    }

    async start() {
        if (!this.qrScanner) return false;

        try {
            await this.qrScanner.start();
            this.isScanning = true;

            // Enhanced video constraints
            const stream = this.video.srcObject;
            if (stream) {
                const videoTrack = stream.getVideoTracks()[0];
                if (videoTrack?.applyConstraints) {
                    try {
                        await videoTrack.applyConstraints({
                            width: { ideal: 1920, min: 1280 },
                            height: { ideal: 1080, min: 720 },
                            zoom: { ideal: 1.25, min: 1.0, max: 2.0 }
                        });
                    } catch (constraintError) {
                        console.log('Advanced constraints not supported:', constraintError.message);
                    }
                }
            }

            return true;
        } catch (error) {
            console.error('Scanner start failed:', error);
            this.isScanning = false;
            return false;
        }
    }

    stop() {
        if (this.qrScanner && this.isScanning) {
            try {
                this.qrScanner.stop();
                this.isScanning = false;
            } catch (error) {
                console.error('Scanner stop error:', error);
            }
        }
    }

    handleScanResult(result) {
        try {
            if (this.dotNetRef && result?.data) {
                this.dotNetRef.invokeMethodAsync('OnQrCodeScanned', result.data);
            }
        } catch (error) {
            console.error('Scan result handling error:', error);
        }
    }

    async switchCamera() {
        if (!this.qrScanner || this.cameras.length <= 1) {
            console.log('Camera switch not possible:', this.cameras.length);
            return false;
        }

        try {
            // Stop current scanning
            const wasScanning = this.isScanning;
            if (wasScanning) {
                this.qrScanner.stop();
                this.isScanning = false;
            }

            // Switch to next camera
            this.currentCameraIndex = (this.currentCameraIndex + 1) % this.cameras.length;
            const nextCamera = this.cameras[this.currentCameraIndex];

            console.log(`Switching to camera: ${nextCamera.label}`);

            // Set new camera
            await this.qrScanner.setCamera(nextCamera.id);

            // Resume scanning if it was active
            if (wasScanning) {
                await this.qrScanner.start();
                this.isScanning = true;
            }

            return true;
        } catch (error) {
            console.error('Camera switch failed:', error);

            // Attempt to recover by restarting with original camera
            try {
                if (this.isScanning) {
                    await this.qrScanner.start();
                }
            } catch (recoveryError) {
                console.error('Failed to recover from camera switch error:', recoveryError);
            }
            return false;
        }
    }

    destroy() {
        try {
            this.stop();
            if (this.qrScanner) {
                this.qrScanner.destroy();
                this.qrScanner = null;
            }
        } catch (error) {
            console.error('Scanner destroy error:', error);
        }
    }
}

// Global scanner management
const scannerInstances = new Map();

window.qrScanHelper = {
    async startScanWithId(videoId, dotNetObject) {
        try {
            let scanner = scannerInstances.get(videoId);

            if (!scanner) {
                scanner = new QRScannerManager(videoId);
                const initSuccess = await scanner.init(dotNetObject);
                if (!initSuccess) {
                    console.error(`Failed to initialize scanner for ${videoId}`);
                    return false;
                }
                scannerInstances.set(videoId, scanner);
            } else {
                scanner.dotNetRef = dotNetObject;
            }

            return await scanner.start();
        } catch (error) {
            console.error(`Start scan error for ${videoId}:`, error);
            return false;
        }
    },

    stopScanWithId(videoId) {
        try {
            const scanner = scannerInstances.get(videoId);
            if (scanner) {
                scanner.stop();
            }
        } catch (error) {
            console.error(`Stop scan error for ${videoId}:`, error);
        }
    },

    async switchCameraWithId(videoId) {
        try {
            const scanner = scannerInstances.get(videoId);
            if (scanner) {
                return await scanner.switchCamera();
            }
            return false;
        } catch (error) {
            console.error(`Camera switch error for ${videoId}:`, error);
            return false;
        }
    },

    cleanup() {
        try {
            scannerInstances.forEach((scanner, videoId) => {
                try {
                    scanner.destroy();
                } catch (error) {
                    console.error(`Cleanup error for ${videoId}:`, error);
                }
            });
            scannerInstances.clear();
        } catch (error) {
            console.error('Global cleanup error:', error);
        }
    },

    // Legacy support
    async startScan(dotNetObject) {
        return this.startScanWithId("qrScanner", dotNetObject);
    },

    stopScan() {
        this.stopScanWithId("qrScanner");
    },

    async switchCamera() {
        return this.switchCameraWithId("qrScanner");
    }
};

// Cleanup on page unload
window.addEventListener('beforeunload', () => {
    window.qrScanHelper.cleanup();
});