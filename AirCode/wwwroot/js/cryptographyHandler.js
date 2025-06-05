// File: wwwroot/js/cryptographyHandler.js handles offline crypto and signin stuff
//could add one for online stuff but currently not necessary
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

    // Generate AES key with specific bit length (128, 192, or 256)
    generateAesKey: async function (bitLength = 256) {
        // Validate bit length
        if (![128, 192, 256].includes(bitLength)) {
            throw new Error("Invalid bit length. Must be 128, 192, or 256.");
        }

        // Generate a random key
        const key = await crypto.subtle.generateKey(
            {
                name: "AES-CBC",
                length: bitLength
            },
            true, // extractable
            ["encrypt", "decrypt"]
        );

        // Export the key to raw format
        const rawKey = await crypto.subtle.exportKey("raw", key);
        return window.cryptographyHandler.arrayBufferToBase64(rawKey);
    },

    // Generate random IV for AES encryption
    generateIv: function () {
        const iv = crypto.getRandomValues(new Uint8Array(16));
        return window.cryptographyHandler.arrayBufferToBase64(iv);
    },

    // Encrypts data using AES-CBC with specified key size
    encryptData: async function (data, keyString, ivString) {
        const encoder = new TextEncoder();
        const dataBuffer = encoder.encode(data);
        const keyBuffer = window.cryptographyHandler.base64ToArrayBuffer(keyString);
        const ivBuffer = window.cryptographyHandler.base64ToArrayBuffer(ivString);

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

    // Decrypts Base64-encoded data using AES-CBC
    decryptData: async function (base64Data, keyString, ivString) {
        const keyBuffer = window.cryptographyHandler.base64ToArrayBuffer(keyString);
        const ivBuffer = window.cryptographyHandler.base64ToArrayBuffer(ivString);

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

    // Computes an HMAC signature for the given data
    signData: async function (data, keyString, algorithm = "SHA-256") {
        const encoder = new TextEncoder();
        const keyBuffer = encoder.encode(keyString);
        const dataBuffer = encoder.encode(data);

        // Import the key material into a CryptoKey object for HMAC
        const cryptoKey = await crypto.subtle.importKey(
            "raw",
            keyBuffer,
            { name: "HMAC", hash: { name: algorithm } },
            false,
            ["sign"]
        );

        // Compute the HMAC signature
        const signatureBuffer = await crypto.subtle.sign("HMAC", cryptoKey, dataBuffer);

        // Convert the ArrayBuffer to a hex string
        const hexSignature = window.cryptographyHandler.arrayBufferToHex(signatureBuffer);

        // Return only the first 8 bytes (16 hex characters) for brevity
        return hexSignature.substring(0, 16);
    },

    // Verifies an HMAC signature for the given data
    verifyHmac: async function (data, providedSignature, keyString, algorithm = "SHA-256") {
        // Compute the signature using the signData method
        const computedSignature = await window.cryptographyHandler.signData(data, keyString, algorithm);
        // Compare signatures
        return computedSignature === providedSignature;
    }
};

// Helper function for PKCE OAuth flow
window.generateCodeChallenge = async function(codeVerifier) {
    const encoder = new TextEncoder();
    const data = encoder.encode(codeVerifier);
    const hash = await crypto.subtle.digest('SHA-256', data);

    return base64UrlEncode(hash);
};

function base64UrlEncode(buffer) {
    var base64 = btoa(String.fromCharCode.apply(null, new Uint8Array(buffer)));
    return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
