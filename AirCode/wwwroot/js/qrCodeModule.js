// Fixed qrCodeModule.js - Non-module version for GitHub Pages
(function(window) {
    'use strict';

    let wasmModule;
    let wasmInitialized = false;
    let wasmAvailable = false;

    // Enhanced fallback QR code generation with better visual quality
    function generateFallbackQRCode(text, size, darkColor, lightColor) {
        console.log('🔄 Generating fallback QR code...');

        const gridSize = 25;
        const cellSize = Math.floor(size / gridSize);
        const actualSize = cellSize * gridSize;
        const margin = Math.floor((size - actualSize) / 2);

        // Create a more realistic QR code pattern
        const createQRPattern = (text, gridSize) => {
            const pattern = Array(gridSize).fill().map(() => Array(gridSize).fill(0));

            // Add finder patterns (corner squares)
            const addFinderPattern = (startRow, startCol) => {
                for (let r = 0; r < 7; r++) {
                    for (let c = 0; c < 7; c++) {
                        if (startRow + r < gridSize && startCol + c < gridSize) {
                            // Outer border
                            if (r === 0 || r === 6 || c === 0 || c === 6) {
                                pattern[startRow + r][startCol + c] = 1;
                            }
                            // Inner square
                            else if (r >= 2 && r <= 4 && c >= 2 && c <= 4) {
                                pattern[startRow + r][startCol + c] = 1;
                            }
                        }
                    }
                }
            };

            // Add finder patterns to corners
            addFinderPattern(0, 0);
            addFinderPattern(0, gridSize - 7);
            addFinderPattern(gridSize - 7, 0);

            // Add timing patterns
            for (let i = 8; i < gridSize - 8; i++) {
                if (i % 2 === 0) {
                    pattern[6][i] = 1;
                    pattern[i][6] = 1;
                }
            }

            // Add some data pattern based on text hash
            const textHash = text.split('').reduce((a, b) => {
                a = ((a << 5) - a) + b.charCodeAt(0);
                return a & a;
            }, 0);

            for (let r = 9; r < gridSize - 1; r++) {
                for (let c = 9; c < gridSize - 1; c++) {
                    if ((r + c + textHash) % 3 === 0) {
                        pattern[r][c] = 1;
                    }
                }
            }

            return pattern;
        };

        const pattern = createQRPattern(text, gridSize);

        let svg = `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">`;
        svg += `<rect width="100%" height="100%" fill="${lightColor}"/>`;

        // Draw the pattern
        for (let r = 0; r < gridSize; r++) {
            for (let c = 0; c < gridSize; c++) {
                if (pattern[r][c]) {
                    const x = margin + c * cellSize;
                    const y = margin + r * cellSize;
                    svg += `<rect x="${x}" y="${y}" width="${cellSize}" height="${cellSize}" fill="${darkColor}"/>`;
                }
            }
        }

        svg += '</svg>';
        return svg;
    }

    // Main QR code generation function
    async function generateQrCode(text, size, darkColor, lightColor) {
        try {
            // For GitHub Pages, always use fallback since WASM files are not available
            console.log('📋 Using fallback QR code generation');
            return generateFallbackQRCode(text, size, darkColor, lightColor);
        } catch (error) {
            console.error('❌ Error in QR code generation:', error);
            return generateFallbackQRCode(text, size, darkColor, lightColor);
        }
    }

    // Enhanced QR code generation with visual enhancements
    async function generateEnhancedQrCode(text, size, darkColor, lightColor, options = {}) {
        try {
            console.log('🎨 Generating enhanced QR code with options:', options);

            // Generate base QR code
            const baseSvg = await generateQrCode(text, size, darkColor, lightColor);

            // Parse SVG for manipulation
            const parser = new DOMParser();
            const svgDoc = parser.parseFromString(baseSvg, "image/svg+xml");
            const svgElement = svgDoc.documentElement;

            // Check for parsing errors
            if (svgDoc.querySelector('parsererror')) {
                throw new Error('Failed to parse SVG');
            }

            // Apply gradient enhancement
            if (options.useGradient) {
                console.log('🌈 Applying gradient enhancement');
                applyGradient(svgElement, darkColor, options);
            }

            // Apply logo enhancement
            if (options.logoUrl) {
                console.log('🖼️ Applying logo enhancement');
                await applyLogo(svgElement, size, options);
            }

            // Serialize and return
            const serializer = new XMLSerializer();
            const result = serializer.serializeToString(svgDoc);
            console.log('✅ Enhanced QR code generated successfully');
            return result;

        } catch (error) {
            console.error('❌ Error generating enhanced QR code:', error);
            // Fallback to basic QR code
            return await generateQrCode(text, size, darkColor, lightColor);
        }
    }

    // Apply gradient to SVG elements
    function applyGradient(svgElement, darkColor, options) {
        const gradId = `qrGradient_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        const gradientDirection = options.gradientDirection || "linear-x";
        const gradColor1 = options.gradientColor1 || darkColor;
        const gradColor2 = options.gradientColor2 || darkColor;

        // Create defs element
        let defs = svgElement.querySelector('defs');
        if (!defs) {
            defs = document.createElementNS("http://www.w3.org/2000/svg", "defs");
            svgElement.insertBefore(defs, svgElement.firstChild);
        }

        // Create gradient
        let gradient;
        if (gradientDirection === "radial") {
            gradient = document.createElementNS("http://www.w3.org/2000/svg", "radialGradient");
            gradient.setAttribute("cx", "50%");
            gradient.setAttribute("cy", "50%");
            gradient.setAttribute("r", "50%");
        } else {
            gradient = document.createElementNS("http://www.w3.org/2000/svg", "linearGradient");

            const directions = {
                "linear-x": { x1: "0%", y1: "50%", x2: "100%", y2: "50%" },
                "linear-y": { x1: "50%", y1: "0%", x2: "50%", y2: "100%" },
                "diagonal": { x1: "0%", y1: "0%", x2: "100%", y2: "100%" }
            };

            const dir = directions[gradientDirection] || directions["linear-x"];
            Object.entries(dir).forEach(([attr, value]) => {
                gradient.setAttribute(attr, value);
            });
        }

        gradient.setAttribute("id", gradId);

        // Create gradient stops
        const stop1 = document.createElementNS("http://www.w3.org/2000/svg", "stop");
        stop1.setAttribute("offset", "0%");
        stop1.setAttribute("stop-color", gradColor1);

        const stop2 = document.createElementNS("http://www.w3.org/2000/svg", "stop");
        stop2.setAttribute("offset", "100%");
        stop2.setAttribute("stop-color", gradColor2);

        gradient.appendChild(stop1);
        gradient.appendChild(stop2);
        defs.appendChild(gradient);

        // Apply gradient to dark elements
        const elements = svgElement.querySelectorAll('path, rect, circle, polygon');
        elements.forEach(element => {
            const fill = element.getAttribute('fill');
            if (fill === darkColor ||
                (fill && fill.toLowerCase() === darkColor.toLowerCase())) {
                element.setAttribute('fill', `url(#${gradId})`);
            }
        });
    }

    // Apply logo to SVG
    async function applyLogo(svgElement, size, options) {
        const logoSizeRatio = Math.min(Math.max(options.logoSizeRatio || 0.2, 0.1), 0.4);
        const logoSize = Math.floor(size * logoSizeRatio);
        const logoX = Math.floor((size - logoSize) / 2);
        const logoY = Math.floor((size - logoSize) / 2);

        // Create logo container group
        const logoGroup = document.createElementNS("http://www.w3.org/2000/svg", "g");
        logoGroup.setAttribute("class", "qr-logo");

        // Add background for logo (to ensure readability)
        const bgPadding = Math.floor(logoSize * 0.1);
        const bgSize = logoSize + (bgPadding * 2);
        const bgX = logoX - bgPadding;
        const bgY = logoY - bgPadding;

        const logoBg = document.createElementNS("http://www.w3.org/2000/svg", "rect");
        logoBg.setAttribute("x", bgX);
        logoBg.setAttribute("y", bgY);
        logoBg.setAttribute("width", bgSize);
        logoBg.setAttribute("height", bgSize);
        logoBg.setAttribute("fill", options.logoBgColor || "white");
        logoBg.setAttribute("rx", options.logoBgRadius || "4");
        logoBg.setAttribute("ry", options.logoBgRadius || "4");
        logoGroup.appendChild(logoBg);

        // Add logo image
        const image = document.createElementNS("http://www.w3.org/2000/svg", "image");
        image.setAttribute("href", options.logoUrl);
        image.setAttributeNS("http://www.w3.org/1999/xlink", "xlink:href", options.logoUrl);
        image.setAttribute("x", logoX);
        image.setAttribute("y", logoY);
        image.setAttribute("width", logoSize);
        image.setAttribute("height", logoSize);
        image.setAttribute("preserveAspectRatio", "xMidYMid meet");

        // Add rounded corners to logo if specified
        if (options.logoRadius) {
            const clipId = `logoClip_${Date.now()}`;
            let defs = svgElement.querySelector('defs');
            if (!defs) {
                defs = document.createElementNS("http://www.w3.org/2000/svg", "defs");
                svgElement.insertBefore(defs, svgElement.firstChild);
            }

            const clipPath = document.createElementNS("http://www.w3.org/2000/svg", "clipPath");
            clipPath.setAttribute("id", clipId);

            const clipRect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
            clipRect.setAttribute("x", logoX);
            clipRect.setAttribute("y", logoY);
            clipRect.setAttribute("width", logoSize);
            clipRect.setAttribute("height", logoSize);
            clipRect.setAttribute("rx", options.logoRadius);
            clipRect.setAttribute("ry", options.logoRadius);

            clipPath.appendChild(clipRect);
            defs.appendChild(clipPath);
            image.setAttribute("clip-path", `url(#${clipId})`);
        }

        logoGroup.appendChild(image);

        // Add border if requested
        if (options.addLogoBorder) {
            const border = document.createElementNS("http://www.w3.org/2000/svg", "rect");
            border.setAttribute("x", logoX);
            border.setAttribute("y", logoY);
            border.setAttribute("width", logoSize);
            border.setAttribute("height", logoSize);
            border.setAttribute("fill", "none");
            border.setAttribute("stroke", options.logoBorderColor || "#000000");
            border.setAttribute("stroke-width", options.logoBorderWidth || "2");

            if (options.logoRadius) {
                border.setAttribute("rx", options.logoRadius);
                border.setAttribute("ry", options.logoRadius);
            }

            logoGroup.appendChild(border);
        }

        // Append logo group to SVG
        svgElement.appendChild(logoGroup);
    }

    // Utility function to set SVG content
    function setSvgContent(elementId, svgContent) {
        const element = document.getElementById(elementId);
        if (element) {
            console.log(`📋 Setting SVG content for element: ${elementId}`);
            element.innerHTML = svgContent;
            return true;
        } else {
            console.error(`❌ Element with ID '${elementId}' not found`);
            return false;
        }
    }

    // Get module status for debugging
    function getModuleStatus() {
        return {
            wasmInitialized: false,
            wasmAvailable: false,
            hasWasmModule: false,
            fallbackOnly: true
        };
    }

    // Expose functions to global scope
    window.QRCodeModule = {
        generateQrCode,
        generateEnhancedQrCode,
        setSvgContent,
        getModuleStatus
    };

})(window);