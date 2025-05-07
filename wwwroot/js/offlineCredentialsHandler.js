// File: wwwroot/js/offlineCredentialsHandler.js
// Handles offline credentials for AirCode app

window.offlineCredentialsHandler = {
    // Storage keys
    STORAGE_KEYS: {
        CREDENTIALS: "AirCode_offline_credentials",
        AUTH_KEY: "aircode_auth_key",
        AUTH_IV: "aircode_auth_iv"
    },

    // Collect basic device information that's consistent across platforms
    collectDeviceInfo: function() {
        return {
            userAgent: navigator.userAgent,
            language: navigator.language,
            platform: navigator.platform,
            screenWidth: window.screen.width,
            screenHeight: window.screen.height,
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone
        };
    },

    // Create a device fingerprint from collected info
    createDeviceFingerprint: async function() {
        const deviceInfo = this.collectDeviceInfo();
        const deviceInfoStr = JSON.stringify(deviceInfo);

        // Create a hash of the device info using our crypto handler
        return await window.cryptographyHandler.hashData(deviceInfoStr);
    },

    // Store credentials securely with provided key and IV
    storeCredentials: async function(userId, role, key, iv, expirationDays = 14) {
        try {
            // Generate a device fingerprint
            const deviceFingerprint = await this.createDeviceFingerprint();

            // Calculate expiration date
            const expirationDate = new Date();
            expirationDate.setDate(expirationDate.getDate() + expirationDays);

            // Create credentials object
            const credentials = {
                userId: userId,
                role: role,
                deviceFingerprint: deviceFingerprint,
                issuedAt: new Date().toISOString(),
                expiresAt: expirationDate.toISOString()
            };

            // Store the key and IV for later decryption
            localStorage.setItem(this.STORAGE_KEYS.AUTH_KEY, key);
            localStorage.setItem(this.STORAGE_KEYS.AUTH_IV, iv);

            // Create a signature for the credentials using the device fingerprint
            const credentialsStr = JSON.stringify(credentials);
            const signature = await window.cryptographyHandler.signData(
                credentialsStr,
                deviceFingerprint
            );

            // Add signature to credentials
            const signedCredentials = {
                ...credentials,
                signature: signature
            };

            // Encrypt the signed credentials
            const encryptedCredentials = await window.cryptographyHandler.encryptData(
                JSON.stringify(signedCredentials),
                key,
                iv
            );

            // Store in local storage
            localStorage.setItem(this.STORAGE_KEYS.CREDENTIALS, encryptedCredentials);

            return true;
        } catch (error) {
            console.error("Failed to store offline credentials:", error);
            return false;
        }
    },

    // Retrieve and validate credentials
    getCredentials: async function() {
        try {
            // Get encrypted credentials from storage
            const encryptedCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS);
            const key = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY);
            const iv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV);

            if (!encryptedCredentials || !key || !iv) {
                return null;
            }

            // Decrypt credentials
            const decryptedStr = await window.cryptographyHandler.decryptData(
                encryptedCredentials,
                key,
                iv
            );

            const signedCredentials = JSON.parse(decryptedStr);

            // Verify credentials haven't expired
            const expiresAt = new Date(signedCredentials.expiresAt);
            if (expiresAt < new Date()) {
                console.warn("Offline credentials have expired");
                return null;
            }

            // Get current device fingerprint
            const currentDeviceFingerprint = await this.createDeviceFingerprint();

            // Verify device fingerprint
            if (currentDeviceFingerprint !== signedCredentials.deviceFingerprint) {
                console.warn("Device fingerprint mismatch");
                return null;
            }

            // Verify signature
            const { signature, ...credentialsWithoutSignature } = signedCredentials;
            const credentialsStr = JSON.stringify(credentialsWithoutSignature);

            const isValid = await window.cryptographyHandler.verifyHmac(
                credentialsStr,
                signature,
                currentDeviceFingerprint
            );

            if (!isValid) {
                console.warn("Invalid signature on offline credentials");
                return null;
            }

            // Return as JSON string instead of object
            return JSON.stringify({
                userId: signedCredentials.userId,
                role: signedCredentials.role,
                issuedAt: signedCredentials.issuedAt,
                expiresAt: signedCredentials.expiresAt
            });
        } catch (error) {
            console.error("Failed to retrieve offline credentials:", error);
            return null;
        }
    },

    // Get user's role from stored credentials
    getUserRole: async function() {
        try {
            const credentials = await this.getCredentials();
            if (!credentials) return null;

            const parsedCredentials = JSON.parse(credentials);
            return parsedCredentials.role;
        } catch (error) {
            console.error("Failed to get user role:", error);
            return null;
        }
    },

    // Get user's ID from stored credentials
    getUserId: async function() {
        try {
            const credentials = await this.getCredentials();
            if (!credentials) return null;

            const parsedCredentials = JSON.parse(credentials);
            return parsedCredentials.userId;
        } catch (error) {
            console.error("Failed to get user ID:", error);
            return null;
        }
    },

    // Clear stored credentials
    clearCredentials: function() {
        localStorage.removeItem(this.STORAGE_KEYS.CREDENTIALS);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_KEY);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_IV);
        return true;
    }
};