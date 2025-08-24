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
                highlightScanRegion: true,
                highlightCodeOutline: true,
                // Additional optimizations for mobile
                preferredCamera: 'environment',
                maxScansPerSecond: 5
            }
        );

        // Start scanning
        qrScanner.start()
            .then(() => {
                console.log('QR Scanner started successfully');
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