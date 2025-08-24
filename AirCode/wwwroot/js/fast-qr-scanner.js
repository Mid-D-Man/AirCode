// Ultra-Fast QR Scanner for Blazor PWA
// Uses qr-scanner library (works offline when bundled)
// Add to wwwroot/js/fast-qr-scanner.js

let qrScanner = null;
let video = null;
let blazorRef = null;
let isScanning = false;
let scanPaused = false;

// Import QR Scanner library (you'll need to download and include this)
// For offline PWA, download qr-scanner.min.js and place in wwwroot/js/
// From: https://github.com/nimiq/qr-scanner
import QrScanner from './qr-scanner.min.js';

window.initFastQRScanner = async (dotNetHelper) => {
    blazorRef = dotNetHelper;
    video = document.getElementById('qr-video');

    if (!video) {
        console.error('QR video element not found');
        return;
    }

    try {
        // Set QR Scanner to use the video element
        qrScanner = new QrScanner(
            video,
            result => onQrDetected(result.data),
            {
                // Optimized settings for fast scanning
                returnDetailedScanResult: true,
                highlightScanRegion: false,
                highlightCodeOutline: false,

                // Performance optimizations
                maxScansPerSecond: 10, // Increased scan rate
                calculateScanRegion: calculateOptimalScanRegion,

                // Mobile optimizations
                preferredCamera: 'environment', // Back camera

                // Quality vs Speed balance
                onDecodeError: (error) => {
                    // Silently ignore decode errors for performance
                    // console.log('Decode error:', error);
                }
            }
        );

        // Check for torch capability
        const hasFlash = await QrScanner.hasCamera();
        if (hasFlash) {
            const cameras = await QrScanner.listCameras();
            const backCamera = cameras.find(camera =>
                camera.label.toLowerCase().includes('back') ||
                camera.label.toLowerCase().includes('environment')
            );

            if (backCamera) {
                await blazorRef.invokeMethodAsync('OnTorchCapabilityDetected', true);
            }
        }

        console.log('Fast QR Scanner initialized successfully');
    } catch (error) {
        console.error('Failed to initialize QR scanner:', error);
    }
};

window.startScanning = async () => {
    if (!qrScanner) return;

    try {
        scanPaused = false;
        await qrScanner.start();
        isScanning = true;

        // Optimize video settings for mobile
        const stream = video.srcObject;
        if (stream) {
            const videoTrack = stream.getVideoTracks()[0];
            if (videoTrack && videoTrack.applyConstraints) {
                await videoTrack.applyConstraints({
                    // Mobile-optimized constraints
                    width: { ideal: 1280, max: 1920 },
                    height: { ideal: 720, max: 1080 },
                    frameRate: { ideal: 30, max: 60 },
                    facingMode: 'environment',

                    // Additional mobile optimizations
                    aspectRatio: 16/9,
                    resizeMode: 'crop-and-scale'
                });
            }
        }

        console.log('QR scanning started');
    } catch (error) {
        console.error('Failed to start QR scanning:', error);
    }
};

window.stopScanning = async () => {
    if (!qrScanner) return;

    try {
        qrScanner.stop();
        isScanning = false;
        scanPaused = false;
        console.log('QR scanning stopped');
    } catch (error) {
        console.error('Failed to stop QR scanning:', error);
    }
};

window.pauseScanning = async (duration = 2000) => {
    if (!isScanning) return;

    scanPaused = true;
    setTimeout(() => {
        scanPaused = false;
    }, duration);
};

window.toggleTorch = async (enable) => {
    if (!qrScanner) return;

    try {
        if (enable) {
            await qrScanner.turnFlashOn();
        } else {
            await qrScanner.turnFlashOff();
        }
        console.log(`Torch ${enable ? 'enabled' : 'disabled'}`);
    } catch (error) {
        console.error('Failed to toggle torch:', error);
    }
};

window.switchCamera = async () => {
    if (!qrScanner) return;

    try {
        const cameras = await QrScanner.listCameras();
        if (cameras.length > 1) {
            const currentCamera = qrScanner.getCamera();
            const nextCamera = cameras.find(cam => cam.id !== currentCamera?.id) || cameras[0];
            await qrScanner.setCamera(nextCamera.id);
            console.log('Switched to camera:', nextCamera.label);
        }
    } catch (error) {
        console.error('Failed to switch camera:', error);
    }
};

// Optimized scan region calculation for mobile devices
function calculateOptimalScanRegion(video) {
    const smallestDimension = Math.min(video.videoWidth, video.videoHeight);
    const scanRegionSize = Math.round(0.7 * smallestDimension);

    return {
        x: Math.round((video.videoWidth - scanRegionSize) / 2),
        y: Math.round((video.videoHeight - scanRegionSize) / 2),
        width: scanRegionSize,
        height: scanRegionSize,
    };
}

// Handle QR code detection
async function onQrDetected(qrData) {
    if (!blazorRef || scanPaused || !isScanning) return;

    try {
        // Throttle to prevent duplicate scans
        scanPaused = true;

        // Call back to Blazor
        await blazorRef.invokeMethodAsync('OnQrCodeDetected', qrData);

        // Brief pause before allowing next scan
        setTimeout(() => {
            scanPaused = false;
        }, 1000);

    } catch (error) {
        console.error('Error processing QR code:', error);
        scanPaused = false;
    }
}

// Performance monitoring (optional - for debugging)
window.getQRScannerStats = () => {
    if (!qrScanner || !video) return null;

    return {
        isScanning: isScanning,
        scanPaused: scanPaused,
        videoWidth: video.videoWidth,
        videoHeight: video.videoHeight,
        fps: video.srcObject?.getVideoTracks()[0]?.getSettings()?.frameRate || 0
    };
};

// Cleanup on page unload
window.addEventListener('beforeunload', () => {
    if (qrScanner) {
        qrScanner.destroy();
    }
});