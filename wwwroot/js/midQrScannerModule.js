import * as wasm from './pkg/your_crate_name.js';

export class QRScannerComponent {
    constructor(element) {
        this.element = element;
        this.wasmModule = null;
        this.scanCallback = null;
    }

    async init() {
        this.wasmModule = await wasm.default();
        return this;
    }

    setupScanFromSVG(callbackFn) {
        this.scanCallback = callbackFn;
    }

    scanSVG(svgString) {
        if (!this.wasmModule) {
            return "WASM module not initialized";
        }

        try {
            const result = this.wasmModule.scan_qr_from_svg(svgString);
            if (this.scanCallback) {
                this.scanCallback(result);
            }
            return result;
        } catch (error) {
            return `Error: ${error.message}`;
        }
    }

    // Add any additional functionality here
}