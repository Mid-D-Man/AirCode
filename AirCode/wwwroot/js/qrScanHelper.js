// wwwroot/js/qrScanHelper.js - Enhanced for multiple scanner instances
var qrScanners = new Map(); // Store multiple scanner instances

window.qrScanHelper = {
    // Original single scanner methods (backward compatibility)
    startScan: function (dotNetObject) {
        const videoElement = document.getElementById("qrScanner");
        if (!videoElement) {
            console.error('Video element with id "qrScanner" not found');
            return;
        }
        this.startScanWithId("qrScanner", dotNetObject);
    },

    stopScan: function () {
        this.stopScanWithId("qrScanner");
    },

    switchCamera: function() {
        this.switchCameraWithId("qrScanner");
    },

    // Enhanced methods with ID support
    startScanWithId: function (videoId, dotNetObject) {
        const videoElement = document.getElementById(videoId);

        if (!videoElement) {
            console.error(`Video element with id "${videoId}" not found`);
            return;
        }

        // Stop existing scanner if present
        if (qrScanners.has(videoId)) {
            qrScanners.get(videoId).stop();
        }

        // Create QR Scanner instance
        const scanner = new QrScanner(
            videoElement,
            result => {
                console.log('decoded qr code:', result);
                dotNetObject.invokeMethodAsync('OnQrCodeScanned', result.data);
            },
            {
                highlightScanRegion: false,
                highlightCodeOutline: false,
                preferredCamera: 'environment',
                maxScansPerSecond: 8,
                calculateScanRegion: function(video) {
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

        // Store scanner instance
        qrScanners.set(videoId, scanner);

        // Start scanner with enhanced constraints
        scanner.start()
            .then(async () => {
                console.log(`QR Scanner ${videoId} started successfully`);

                const stream = videoElement.srcObject;
                if (stream) {
                    const videoTrack = stream.getVideoTracks()[0];
                    if (videoTrack && videoTrack.applyConstraints) {
                        try {
                            await videoTrack.applyConstraints({
                                // UPDATED: Slight zoom in from -1 to -0.5
                                width: { ideal: 1600, min: 800, max: 1920 },
                                height: { ideal: 900, min: 600, max: 1080 },
                                facingMode: 'environment',
                                // CRITICAL: Zoom control - adjusted for slight zoom in
                                zoom: { ideal: 1.25, min: 1.0, max: 2.0 }
                            });
                            console.log(`Applied enhanced constraints to ${videoId}`);
                        } catch (e) {
                            console.log(`Constraints fallback for ${videoId}:`, e.message);
                        }
                    }
                }
            })
            .catch(error => {
                console.error(`Failed to start QR scanner ${videoId}:`, error);
                qrScanners.delete(videoId);
            });
    },

    stopScanWithId: function (videoId) {
        const scanner = qrScanners.get(videoId);
        if (scanner) {
            scanner.stop();
            qrScanners.delete(videoId);
            console.log(`QR Scanner ${videoId} stopped`);
        }
    },

    switchCameraWithId: function(videoId) {
        const scanner = qrScanners.get(videoId);
        if (scanner) {
            QrScanner.listCameras().then(cameras => {
                if (cameras.length > 1) {
                    const currentCameraId = scanner.getCamera()?.id;
                    const nextCamera = cameras.find(cam => cam.id !== currentCameraId) || cameras[0];
                    scanner.setCamera(nextCamera.id);
                }
            });
        }
    },

    // Cleanup all scanners
    stopAllScanners: function() {
        qrScanners.forEach((scanner, videoId) => {
            scanner.stop();
            console.log(`Stopped scanner: ${videoId}`);
        });
        qrScanners.clear();
    }
};