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
// Enhanced cryptographyHandler.js - Add MD5 support for Gravatar URLs

// Add to existing window.cryptographyHandler object:

window.cryptographyHandler.hashMD5 = function(input) {
    // MD5 implementation for Gravatar hash generation
    // Since WebCrypto doesn't support MD5, we implement it directly

    function md5cycle(x, k) {
        var a = x[0], b = x[1], c = x[2], d = x[3];
        a = ff(a, b, c, d, k[0], 7, -680876936);
        d = ff(d, a, b, c, k[1], 12, -389564586);
        c = ff(c, d, a, b, k[2], 17, 606105819);
        b = ff(b, c, d, a, k[3], 22, -1044525330);
        a = ff(a, b, c, d, k[4], 7, -176418897);
        d = ff(d, a, b, c, k[5], 12, 1200080426);
        c = ff(c, d, a, b, k[6], 17, -1473231341);
        b = ff(b, c, d, a, k[7], 22, -45705983);
        a = ff(a, b, c, d, k[8], 7, 1770035416);
        d = ff(d, a, b, c, k[9], 12, -1958414417);
        c = ff(c, d, a, b, k[10], 17, -42063);
        b = ff(b, c, d, a, k[11], 22, -1990404162);
        a = ff(a, b, c, d, k[12], 7, 1804603682);
        d = ff(d, a, b, c, k[13], 12, -40341101);
        c = ff(c, d, a, b, k[14], 17, -1502002290);
        b = ff(b, c, d, a, k[15], 22, 1236535329);
        a = gg(a, b, c, d, k[1], 5, -165796510);
        d = gg(d, a, b, c, k[6], 9, -1069501632);
        c = gg(c, d, a, b, k[11], 14, 643717713);
        b = gg(b, c, d, a, k[0], 20, -373897302);
        a = gg(a, b, c, d, k[5], 5, -701558691);
        d = gg(d, a, b, c, k[10], 9, 38016083);
        c = gg(c, d, a, b, k[15], 14, -660478335);
        b = gg(b, c, d, a, k[4], 20, -405537848);
        a = gg(a, b, c, d, k[9], 5, 568446438);
        d = gg(d, a, b, c, k[14], 9, -1019803690);
        c = gg(c, d, a, b, k[3], 14, -187363961);
        b = gg(b, c, d, a, k[8], 20, 1163531501);
        a = gg(a, b, c, d, k[13], 5, -1444681467);
        d = gg(d, a, b, c, k[2], 9, -51403784);
        c = gg(c, d, a, b, k[7], 14, 1735328473);
        b = gg(b, c, d, a, k[12], 20, -1926607734);
        a = hh(a, b, c, d, k[5], 4, -378558);
        d = hh(d, a, b, c, k[8], 11, -2022574463);
        c = hh(c, d, a, b, k[11], 16, 1839030562);
        b = hh(b, c, d, a, k[14], 23, -35309556);
        a = hh(a, b, c, d, k[1], 4, -1530992060);
        d = hh(d, a, b, c, k[4], 11, 1272893353);
        c = hh(c, d, a, b, k[7], 16, -155497632);
        b = hh(b, c, d, a, k[10], 23, -1094730640);
        a = hh(a, b, c, d, k[13], 4, 681279174);
        d = hh(d, a, b, c, k[0], 11, -358537222);
        c = hh(c, d, a, b, k[3], 16, -722521979);
        b = hh(b, c, d, a, k[6], 23, 76029189);
        a = hh(a, b, c, d, k[9], 4, -640364487);
        d = hh(d, a, b, c, k[12], 11, -421815835);
        c = hh(c, d, a, b, k[15], 16, 530742520);
        b = hh(b, c, d, a, k[2], 23, -995338651);
        a = ii(a, b, c, d, k[0], 6, -198630844);
        d = ii(d, a, b, c, k[7], 10, 1126891415);
        c = ii(c, d, a, b, k[14], 15, -1416354905);
        b = ii(b, c, d, a, k[5], 21, -57434055);
        a = ii(a, b, c, d, k[12], 6, 1700485571);
        d = ii(d, a, b, c, k[3], 10, -1894986606);
        c = ii(c, d, a, b, k[10], 15, -1051523);
        b = ii(b, c, d, a, k[1], 21, -2054922799);
        a = ii(a, b, c, d, k[8], 6, 1873313359);
        d = ii(d, a, b, c, k[15], 10, -30611744);
        c = ii(c, d, a, b, k[6], 15, -1560198380);
        b = ii(b, c, d, a, k[13], 21, 1309151649);
        a = ii(a, b, c, d, k[4], 6, -145523070);
        d = ii(d, a, b, c, k[11], 10, -1120210379);
        c = ii(c, d, a, b, k[2], 15, 718787259);
        b = ii(b, c, d, a, k[9], 21, -343485551);
        x[0] = add32(a, x[0]);
        x[1] = add32(b, x[1]);
        x[2] = add32(c, x[2]);
        x[3] = add32(d, x[3]);
    }

    function cmn(q, a, b, x, s, t) {
        a = add32(add32(a, q), add32(x, t));
        return add32((a << s) | (a >>> (32 - s)), b);
    }

    function ff(a, b, c, d, x, s, t) {
        return cmn((b & c) | ((~b) & d), a, b, x, s, t);
    }

    function gg(a, b, c, d, x, s, t) {
        return cmn((b & d) | (c & (~d)), a, b, x, s, t);
    }

    function hh(a, b, c, d, x, s, t) {
        return cmn(b ^ c ^ d, a, b, x, s, t);
    }

    function ii(a, b, c, d, x, s, t) {
        return cmn(c ^ (b | (~d)), a, b, x, s, t);
    }

    function md51(s) {
        var n = s.length,
            state = [1732584193, -271733879, -1732584194, 271733878], i;
        for (i = 64; i <= s.length; i += 64) {
            md5cycle(state, md5blk(s.substring(i - 64, i)));
        }
        s = s.substring(i - 64);
        var tail = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        for (i = 0; i < s.length; i++)
            tail[i >> 2] |= s.charCodeAt(i) << ((i % 4) << 3);
        tail[i >> 2] |= 0x80 << ((i % 4) << 3);
        if (i > 55) {
            md5cycle(state, tail);
            for (i = 0; i < 16; i++) tail[i] = 0;
        }
        tail[14] = n * 8;
        md5cycle(state, tail);
        return state;
    }

    function md5blk(s) {
        var md5blks = [], i;
        for (i = 0; i < 64; i += 4) {
            md5blks[i >> 2] = s.charCodeAt(i)
                + (s.charCodeAt(i + 1) << 8)
                + (s.charCodeAt(i + 2) << 16)
                + (s.charCodeAt(i + 3) << 24);
        }
        return md5blks;
    }

    function rhex(n) {
        var hex_chr = '0123456789abcdef'.split('');
        var s = '', j = 0;
        for (var i = 0; i < 4; i++)
            s += hex_chr[(n >> (j + 4)) & 0x0F] + hex_chr[(n >> j) & 0x0F], j += 8;
        return s;
    }

    function hex(x) {
        for (var i = 0; i < x.length; i++)
            x[i] = rhex(x[i]);
        return x.join('');
    }

    function add32(a, b) {
        return (a + b) & 0xFFFFFFFF;
    }

    // Main MD5 function
    if (input === '') return 'd41d8cd98f00b204e9800998ecf8427e';
    return hex(md51(input));
};

// Alternative approach using existing SHA-256 for development/testing
// (Note: This is NOT MD5 - use only for testing when MD5 is not critical)
window.cryptographyHandler.hashSHA256AsHex = async function(input) {
    const encoder = new TextEncoder();
    const data = encoder.encode(input);
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
};
function base64UrlEncode(buffer) {
    var base64 = btoa(String.fromCharCode.apply(null, new Uint8Array(buffer)));
    return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
