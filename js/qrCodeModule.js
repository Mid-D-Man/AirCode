import init, { generate_enhanced_qr_code, generate_qr_code } from '/wasm/qr_code_generator.js';
let wasmModule;

// Update your initWasm function in qrCodeModule.js
let wasmInitialized = false;

export async function initWasm() {
    if (!wasmInitialized) {
        try {
            await init();
            wasmInitialized = true;
            console.log("WASM module initialized successfully");
        } catch (error) {
            console.error("Failed to initialize WASM module:", error);
            throw error;
        }
    }
    return wasmInitialized;
}

export async function generateQrCode(text, size, darkColor, lightColor) {
    await initWasm();
    try {
        return generate_qr_code(text, size, darkColor, lightColor);
    } catch (error) {
        console.error("Error generating QR code:", error);
        throw error;
    }
}

export async function generateEnhancedQrCode(text, size, darkColor, lightColor, options = {}) {
    try {
        await initWasm();

        // Generate base QR code
        const baseSvg = generate_qr_code(text, size, darkColor, lightColor);

        // Parse the SVG to a DOM object to manipulate it properly
        const parser = new DOMParser();
        const svgDoc = parser.parseFromString(baseSvg, "image/svg+xml");
        const svgElement = svgDoc.documentElement;

        // If we want to use gradient
        if (options.useGradient) {
            const gradId = `qrGradient_${Math.random().toString(36).substring(2, 9)}`;
            const gradientDirection = options.gradientDirection || "linear-x";
            const gradColor1 = options.gradientColor1 || darkColor;
            const gradColor2 = options.gradientColor2 || darkColor;

            // Create defs element
            const defs = document.createElementNS("http://www.w3.org/2000/svg", "defs");

            // Create gradient element
            let gradient;
            if (gradientDirection === "radial") {
                gradient = document.createElementNS("http://www.w3.org/2000/svg", "radialGradient");
                gradient.setAttribute("cx", "50%");
                gradient.setAttribute("cy", "50%");
                gradient.setAttribute("r", "50%");
            } else {
                gradient = document.createElementNS("http://www.w3.org/2000/svg", "linearGradient");

                if (gradientDirection === "linear-x") {
                    gradient.setAttribute("x1", "0%");
                    gradient.setAttribute("y1", "50%");
                    gradient.setAttribute("x2", "100%");
                    gradient.setAttribute("y2", "50%");
                } else if (gradientDirection === "linear-y") {
                    gradient.setAttribute("x1", "50%");
                    gradient.setAttribute("y1", "0%");
                    gradient.setAttribute("x2", "50%");
                    gradient.setAttribute("y2", "100%");
                } else if (gradientDirection === "diagonal") {
                    gradient.setAttribute("x1", "0%");
                    gradient.setAttribute("y1", "0%");
                    gradient.setAttribute("x2", "100%");
                    gradient.setAttribute("y2", "100%");
                }
            }

            gradient.setAttribute("id", gradId);

            // Create stops
            const stop1 = document.createElementNS("http://www.w3.org/2000/svg", "stop");
            stop1.setAttribute("offset", "0%");
            stop1.setAttribute("stop-color", gradColor1);

            const stop2 = document.createElementNS("http://www.w3.org/2000/svg", "stop");
            stop2.setAttribute("offset", "100%");
            stop2.setAttribute("stop-color", gradColor2);

            // Add stops to gradient
            gradient.appendChild(stop1);
            gradient.appendChild(stop2);

            // Add gradient to defs
            defs.appendChild(gradient);

            // Add defs to SVG
            svgElement.insertBefore(defs, svgElement.firstChild);

            // Replace all dark color fills with gradient
            const paths = svgElement.querySelectorAll('path, rect');
            paths.forEach(path => {
                if (path.getAttribute('fill') === darkColor) {
                    path.setAttribute('fill', `url(#${gradId})`);
                }
            });
        }

        // If we want to add a logo
        // If we want to add a logo
        if (options.logoUrl) {
            const logoSizeRatio = options.logoSizeRatio || 0.25;
            const logoSize = Math.floor(size * logoSizeRatio);
            const logoX = Math.floor((size - logoSize) / 2);
            const logoY = Math.floor((size - logoSize) / 2);

            // Create image element
            const image = document.createElementNS("http://www.w3.org/2000/svg", "image");
            image.setAttribute("href", options.logoUrl);
            image.setAttribute("x", logoX);
            image.setAttribute("y", logoY);
            image.setAttribute("width", logoSize);
            image.setAttribute("height", logoSize);
            image.setAttribute("preserveAspectRatio", "xMidYMid slice");

            // Add image to SVG
            svgElement.appendChild(image);

            // Add border around logo if requested
            if (options.addLogoBorder) {
                const rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
                rect.setAttribute("x", logoX);
                rect.setAttribute("y", logoY);
                rect.setAttribute("width", logoSize);
                rect.setAttribute("height", logoSize);
                rect.setAttribute("fill", "none");
                rect.setAttribute("stroke", options.logoBorderColor || "white");
                rect.setAttribute("stroke-width", options.logoBorderWidth || "2");

                // Add border radius if specified
                if (options.logoBorderRadius) {
                    rect.setAttribute("rx", options.logoBorderRadius);
                    rect.setAttribute("ry", options.logoBorderRadius);
                }

                // Add border to SVG
                svgElement.appendChild(rect);
            }
        }

        // Serialize SVG back to string
        const serializer = new XMLSerializer();
        return serializer.serializeToString(svgDoc);
    } catch (error) {
        console.error("Error generating enhanced QR code:", error);
        throw error;
    }
}
export function setSvgContent(elementId, svgContent) {
    const element = document.getElementById(elementId);
    if (element) {
        console.log("SVG content:", svgContent); // Debug line
        element.innerHTML = svgContent;
    } else {
        console.error(`Element with ID '${elementId}' not found`);
    }
}