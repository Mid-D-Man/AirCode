let videoElem = null;
let canvasElem = null;
let stream = null;
let scanning = false;
let animationFrame;

export async function startZXingScanner(dotNetHelper) {
    videoElem = document.getElementById("zxing-video");
    canvasElem = document.getElementById("zxing-canvas");

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        console.error("Camera API not supported.");
        return;
    }

    try {
        stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: "environment" }
        });
        videoElem.srcObject = stream;
        videoElem.setAttribute("playsinline", true);
        await videoElem.play();
        scanning = true;
        tick(dotNetHelper);
    } catch (err) {
        console.error("Error accessing camera:", err);
    }
}

function tick(dotNetHelper) {
    if (!scanning) return;
    if (videoElem.readyState === videoElem.HAVE_ENOUGH_DATA) {
        // Set canvas dimensions to match the video.
        canvasElem.width = videoElem.videoWidth;
        canvasElem.height = videoElem.videoHeight;
        let context = canvasElem.getContext("2d");
        context.drawImage(videoElem, 0, 0, canvasElem.width, canvasElem.height);

        // Capture image data from the canvas.
        let imageData = context.getImageData(0, 0, canvasElem.width, canvasElem.height);

        try {
            // Create a ZXing BrowserQRCodeReader instance.
            const codeReader = new ZXing.BrowserQRCodeReader();
            // decodeFromImageData returns a promise
            codeReader.decodeFromImageData(imageData)
                .then(result => {
                    // When a QR code is successfully decoded, notify Blazor.
                    dotNetHelper.invokeMethodAsync("OnQRCodeDecoded", result.text);
                    // Optionally, you can stop scanning after a result is detected
                    // stopZXingScanner();
                })
                .catch(err => {
                    // No QR code found in this frame; continue scanning.
                    // (Optionally log err for debugging)
                });
        } catch (ex) {
            console.error("Error during ZXing scanning", ex);
        }
    }
    // Schedule the next frame
    animationFrame = requestAnimationFrame(() => tick(dotNetHelper));
}

export function stopZXingScanner() {
    scanning = false;
    cancelAnimationFrame(animationFrame);
    if (stream) {
        stream.getTracks().forEach(track => track.stop());
    }
}
