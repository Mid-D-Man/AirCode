export function setSvgContent(elementId, svgContent) {
    document.getElementById(elementId).innerHTML = svgContent;
}

// wwwroot/js/qrCodeModule.js
export function initializeScanner(dotnetRef) {
    const codeReader = new ZXing.BrowserMultiFormatReader();

    codeReader.listVideoInputDevices()
        .then(devices => {
            const selectedDevice = devices[0].deviceId;
            codeReader.decodeFromVideoDevice(selectedDevice, 'qr-video', (result, err) => {
                if (result) {
                    dotnetRef.invokeMethodAsync('HandleQrCode', result.text);
                }
            });
        })
        .catch(err => console.error(err));

    return codeReader;
}

export function stopScanner(codeReader) {
    if (codeReader) {
        codeReader.reset();
    }
}
