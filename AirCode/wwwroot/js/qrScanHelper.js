// wwwroot/js/qrScanHelper.js - Based on the working YouTube example
var qrScanner;

window.qrScanHelper = {
    startScan: function (dotNetObject) {
        const videoElement = document.getElementById("qrScanner");

        if (!videoElement) {
            console.error('Video element with id "qrScanner" not found');
            return;
        }

        // Create QR Scanner instance using the UMD library
        qrScanner = new QrScanner(
            videoElement,
            result => {
                console.log('decoded qr code:', result);
                // Call back to Blazor component
                dotNetObject.invokeMethodAsync('OnQrCodeScanned', result.data);
            },
            {
                highlightScanRegion: false, // Turn off built-in highlighting (we have custom overlay)
                highlightCodeOutline: false,
                // FIXED: Better camera settings to prevent zoom
                preferredCamera: 'environment',
                maxScansPerSecond: 8,

                // CRITICAL: Custom scan region calculation for larger area
                calculateScanRegion: function(video) {
                    // Use 80% of the video area for scanning (much larger)
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

        // FIXED: Better camera constraints to prevent zoom
        qrScanner.start()
            .then(async () => {
                console.log('QR Scanner started successfully');

                // Apply better video constraints after start
                const stream = videoElement.srcObject;
                if (stream) {
                    const videoTrack = stream.getVideoTracks()[0];
                    if (videoTrack && videoTrack.applyConstraints) {
                        try {
                            await videoTrack.applyConstraints({
                                // FIXED: Constraints that prevent excessive zoom
                                width: { ideal: 1280, min: 640, max: 1920 },
                                height: { ideal: 720, min: 480, max: 1080 },
                                facingMode: 'environment',
                                // CRITICAL: Zoom control
                                zoom: { ideal: 1.0, min: 1.0, max: 2.0 }
                            });
                            console.log('Applied better camera constraints');
                        } catch (e) {
                            console.log('Could not apply zoom constraints, using defaults:', e.message);
                        }
                    }
                }
            })
            .catch(error => {
                console.error('Failed to start QR scanner:', error);
            });
    },

    stopScan: function () {
        if (qrScanner) {
            qrScanner.stop();
            console.log('QR Scanner stopped');
        }
    },

    // Additional methods for enhanced functionality
    toggleTorch: function(enable) {
        if (qrScanner) {
            if (enable) {
                qrScanner.turnFlashOn();
            } else {
                qrScanner.turnFlashOff();
            }
        }
    },

    switchCamera: function() {
        if (qrScanner) {
            QrScanner.listCameras().then(cameras => {
                if (cameras.length > 1) {
                    // Switch to next available camera
                    const currentCameraId = qrScanner.getCamera()?.id;
                    const nextCamera = cameras.find(cam => cam.id !== currentCameraId) || cameras[0];
                    qrScanner.setCamera(nextCamera.id);
                }
            });
        }
    }
};