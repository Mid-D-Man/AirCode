// File: wwwroot/js/cryptographyHandler.js handles offline crypto , signin stuff
window.cryptographyHandler = {
    // Helper: Convert an ArrayBuffer to a Base64 string.
    arrayBufferToBase64: function (buffer) {
        let binary = "";
        const bytes = new Uint8Array(buffer);
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    },

    // Helper: Convert a Base64 string to an ArrayBuffer.
    base64ToArrayBuffer: function (base64) {
        const binaryString = window.atob(base64);
        const len = binaryString.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes.buffer;
    },

    // Helper: Convert ArrayBuffer to hexadecimal string.
    arrayBufferToHex: function (buffer) {
        let hexCodes = [];
        const view = new DataView(buffer);
        for (let i = 0; i < view.byteLength; i++) {
            hexCodes.push(view.getUint8(i).toString(16).padStart(2, '0'));
        }
        return hexCodes.join('');
    },

    // Encrypts data using AES-CBC.
    encryptData: async function (data, keyString, ivString) {
        const encoder = new TextEncoder();
        const dataBuffer = encoder.encode(data);
        const keyBuffer = encoder.encode(keyString);
        const ivBuffer = encoder.encode(ivString);

        const cryptoKey = await crypto.subtle.importKey(
            "raw",
            keyBuffer,
            { name: "AES-CBC" },
            false,
            ["encrypt"]
        );

        try {
            const encryptedBuffer = await crypto.subtle.encrypt(
                { name: "AES-CBC", iv: ivBuffer },
                cryptoKey,
                dataBuffer
            );
            return window.cryptographyHandler.arrayBufferToBase64(encryptedBuffer);
        } catch (error) {
            console.error("Encryption failed:", error);
            throw error;
        }
    },

    // Decrypts Base64-encoded data using AES-CBC.
    decryptData: async function (base64Data, keyString, ivString) {
        const encoder = new TextEncoder();
        const keyBuffer = encoder.encode(keyString);
        const ivBuffer = encoder.encode(ivString);

        const cryptoKey = await crypto.subtle.importKey(
            "raw",
            keyBuffer,
            { name: "AES-CBC" },
            false,
            ["decrypt"]
        );

        const encryptedBuffer = window.cryptographyHandler.base64ToArrayBuffer(base64Data);
        try {
            const decryptedBuffer = await crypto.subtle.decrypt(
                { name: "AES-CBC", iv: ivBuffer },
                cryptoKey,
                encryptedBuffer
            );
            const decoder = new TextDecoder();
            return decoder.decode(decryptedBuffer);
        } catch (error) {
            console.error("Decryption failed:", error);
            throw error;
        }
    },

    // Hashes data using the specified algorithm (default is SHA-256)
    hashData: async function (data, algorithm = "SHA-256") {
        const encoder = new TextEncoder();
        const dataBuffer = encoder.encode(data);
        try {
            const hashBuffer = await crypto.subtle.digest(algorithm, dataBuffer);
            return window.cryptographyHandler.arrayBufferToBase64(hashBuffer);
        } catch (error) {
            console.error("Hashing failed:", error);
            throw error;
        }
    },

    // Computes an HMAC signature (using SHA-256 by default) for the given data.
    // Returns a hexadecimal string containing the first 8 bytes (16 hex characters) of the signature.
    signData: async function (data, keyString, algorithm = "SHA-256") {
        const encoder = new TextEncoder();
        const keyBuffer = encoder.encode(keyString);
        const dataBuffer = encoder.encode(data);

        // Import the key material into a CryptoKey object for HMAC.
        const cryptoKey = await crypto.subtle.importKey(
            "raw",
            keyBuffer,
            { name: "HMAC", hash: { name: algorithm } },
            false,
            ["sign"]
        );

        // Compute the HMAC signature.
        const signatureBuffer = await crypto.subtle.sign("HMAC", cryptoKey, dataBuffer);

        // Convert the ArrayBuffer to a hex string.
        const hexSignature = window.cryptographyHandler.arrayBufferToHex(signatureBuffer);

        // Return only the first 8 bytes (16 hex characters) for brevity.
        return hexSignature.substring(0, 16);
    },

    // Verifies an HMAC signature for the given data.
    // Returns true if the provided signature matches the computed one.
    verifyHmac: async function (data, providedSignature, keyString, algorithm = "SHA-256") {
        // Compute the signature using the signData method.
        const computedSignature = await window.cryptographyHandler.signData(data, keyString, algorithm);
        // Compare signatures. For extra security, consider using a constant-time comparison.
        return computedSignature === providedSignature;
    }
};
