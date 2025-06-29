// Key/IV Validation for AirCode Offline Credentials
// Run this in browser console to validate your constants

const TEST_KEY = "VGhpcyBpcyBhIHRlc3Qga2V5IGZvciBBaXJDb2RlIHRlc3Rpbmc=";
const TEST_IV = "UmFuZG9tSVZmb3JUZXN0";

function validateKeyIV() {
    console.log("=== AirCode Key/IV Validation ===");

    try {
        // Validate TEST_KEY
        const keyBuffer = atob(TEST_KEY);
        const keyBytes = new Uint8Array(keyBuffer.length);
        for (let i = 0; i < keyBuffer.length; i++) {
            keyBytes[i] = keyBuffer.charCodeAt(i);
        }

        console.log("✓ TEST_KEY decoded successfully");
        console.log(`  - Length: ${keyBytes.length} bytes (${keyBytes.length * 8} bits)`);
        console.log(`  - Valid for AES: ${[16, 24, 32].includes(keyBytes.length) ? 'YES' : 'NO'}`);

        if (keyBytes.length !== 32) {
            console.warn("⚠️  TEST_KEY should be 32 bytes (256 bits) for AES-256");
        }

    } catch (error) {
        console.error("❌ TEST_KEY validation failed:", error.message);
        return false;
    }

    try {
        // Validate TEST_IV
        const ivBuffer = atob(TEST_IV);
        const ivBytes = new Uint8Array(ivBuffer.length);
        for (let i = 0; i < ivBuffer.length; i++) {
            ivBytes[i] = ivBuffer.charCodeAt(i);
        }

        console.log("✓ TEST_IV decoded successfully");
        console.log(`  - Length: ${ivBytes.length} bytes`);
        console.log(`  - Valid for AES-CBC: ${ivBytes.length === 16 ? 'YES' : 'NO'}`);

        if (ivBytes.length !== 16) {
            console.error("❌ TEST_IV must be exactly 16 bytes for AES-CBC");
            return false;
        }

    } catch (error) {
        console.error("❌ TEST_IV validation failed:", error.message);
        return false;
    }

    return true;
}

// Generate correct test constants
function generateCorrectTestConstants() {
    console.log("\n=== Generating Correct Test Constants ===");

    // Generate 32-byte key (256-bit)
    const keyBytes = new Uint8Array(32);
    crypto.getRandomValues(keyBytes);
    const keyBase64 = btoa(String.fromCharCode(...keyBytes));

    // Generate 16-byte IV
    const ivBytes = new Uint8Array(16);
    crypto.getRandomValues(ivBytes);
    const ivBase64 = btoa(String.fromCharCode(...ivBytes));

    console.log("New TEST_KEY (32 bytes):", keyBase64);
    console.log("New TEST_IV (16 bytes):", ivBase64);

    return { keyBase64, ivBase64 };
}

// Test encryption/decryption with current constants
async function testEncryptionFlow() {
    console.log("\n=== Testing Encryption Flow ===");

    try {
        const testData = "Hello, AirCode!";

        // Use your cryptographyHandler if available
        if (window.cryptographyHandler) {
            const encrypted = await window.cryptographyHandler.encryptData(testData, TEST_KEY, TEST_IV);
            console.log("✓ Encryption successful");

            const decrypted = await window.cryptographyHandler.decryptData(encrypted, TEST_KEY, TEST_IV);
            console.log("✓ Decryption successful:", decrypted === testData ? "MATCH" : "MISMATCH");

            return true;
        } else {
            console.warn("⚠️  cryptographyHandler not available - cannot test encryption");
            return false;
        }

    } catch (error) {
        console.error("❌ Encryption test failed:", error.message);
        return false;
    }
}

// Run all validations
async function runFullValidation() {
    const isValid = validateKeyIV();

    if (!isValid) {
        const newConstants = generateCorrectTestConstants();
        console.log("\n📝 Update your C# constants:");
        console.log(`internal static string TEST_KEY = "${newConstants.keyBase64}";`);
        console.log(`internal static string TEST_IV = "${newConstants.ivBase64}";`);
    }

    await testEncryptionFlow();
}

// Execute validation
runFullValidation();