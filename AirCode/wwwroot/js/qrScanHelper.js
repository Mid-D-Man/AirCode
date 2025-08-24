// wwwroot/js/qrScanHelper.js - Fixed implementation
class QRScannerManager {
    constructor(videoId) {
        this.videoId = videoId;
        this.video = document.getElementById(videoId);
        this.qrScanner = null;
        this.isScanning = false;
        this.dotNetRef = null;
    }

    async init(dotNetObject) {
        this.dotNetRef = dotNetObject;

        if (!this.video) {
            console.error(`Video element ${this.videoId} not found`);
            return [];
        }

        const cameras = await QrScanner.listCameras(true);

        this.qrScanner = new QrScanner(
            this.video,
            result => this.handleScanResult(result),
            {
                preferredCamera: 'environment',
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

        return cameras;
    }

    async start() {
        if (!this.qrScanner) return false;

        try {
            await this.qrScanner.start();
            this.isScanning = true;

            // Apply zoom constraints
            const stream = this.video.srcObject;
            if (stream) {
                const videoTrack = stream.getVideoTracks()[0];
                if (videoTrack?.applyConstraints) {
                    try {
                        await videoTrack.applyConstraints({
                            width: { ideal: 1600, min: 800, max: 1920 },
                            height: { ideal: 900, min: 600, max: 1080 },
                            zoom: { ideal: 1.25, min: 1.0, max: 2.0 }
                        });
                    } catch (e) {
                        console.log('Constraints fallback:', e.message);
                    }
                }
            }

            return true;
        } catch (error) {
            console.error('Scanner start failed:', error);
            return false;
        }
    }

    stop() {
        if (this.qrScanner && this.isScanning) {
            this.qrScanner.stop();
            this.isScanning = false;
        }
    }

    handleScanResult(result) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnQrCodeScanned', result.data);
        }
    }

    async switchCamera() {
        if (!this.qrScanner) return;

        const cameras = await QrScanner.listCameras();
        if (cameras.length > 1) {
            const currentId = this.qrScanner.getCamera()?.id;
            const nextCamera = cameras.find(cam => cam.id !== currentId) || cameras[0];
            await this.qrScanner.setCamera(nextCamera.id);
        }
    }
}

// Global scanner instances
const scannerInstances = new Map();

window.qrScanHelper = {
    // Backward compatibility
    async startScan(dotNetObject) {
        return this.startScanWithId("qrScanner", dotNetObject);
    },

    stopScan() {
        this.stopScanWithId("qrScanner");
    },

    switchCamera() {
        this.switchCameraWithId("qrScanner");
    },

    // Multi-instance support
    async startScanWithId(videoId, dotNetObject) {
        let scanner = scannerInstances.get(videoId);

        if (!scanner) {
            scanner = new QRScannerManager(videoId);
            await scanner.init(dotNetObject);
            scannerInstances.set(videoId, scanner);
        } else {
            scanner.dotNetRef = dotNetObject;
        }

        return await scanner.start();
    },

    stopScanWithId(videoId) {
        const scanner = scannerInstances.get(videoId);
        if (scanner) {
            scanner.stop();
        }
    },

    async switchCameraWithId(videoId) {
        const scanner = scannerInstances.get(videoId);
        if (scanner) {
            await scanner.switchCamera();
        }
    },

    cleanup() {
        scannerInstances.forEach(scanner => scanner.stop());
        scannerInstances.clear();
    }
};