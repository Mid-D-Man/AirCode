// File: wwwroot/js/offlineCredentialsHandler.js
// Enhanced offline credentials handler for AirCode app
// Now includes lecturer ID and matric number support
// Enhanced offline credentials handler for AirCode app
// recent changes made => Simplified device fingerprinting - stable and consistent
// Added automatic expiration checking and cleanup
// File: wwwroot/js/offlineCredentialsHandler.js
// Streamlined offline credentials handler for AirCode app
// Optimized for essential functionality with reduced storage overhead

window.offlineCredentialsHandler = {
    // Simplified storage keys - only store what's essential
    STORAGE_KEYS: {
        CREDENTIALS: "AirCode_offline_credentials",
        AUTH_KEY: "aircode_auth_key",
        AUTH_IV: "aircode_auth_iv",
        DEVICE_GUID: "AirCode_device_guid" // Only persistent identifier needed
    },
//#region Device FingerPrint
// Enhanced cross-browser device fingerprinting
// Combines multiple stable hardware/OS characteristics
    getOrCreateDeviceGuid: function() {
        const storageKey = this.STORAGE_KEYS.DEVICE_GUID;
        let deviceGuid = localStorage.getItem(storageKey);

        if (!deviceGuid) {
            // Generate stable device fingerprint using multiple factors
            const fingerprint = this.generateDeviceFingerprint();

            // Create deterministic GUID from fingerprint
            deviceGuid = this.fingerprintToGuid(fingerprint);

            localStorage.setItem(storageKey, deviceGuid);
            console.log('[OfflineCredentials] Generated device GUID:', deviceGuid);
        }

        return deviceGuid;
    },

// Generate stable device fingerprint using hardware characteristics
    generateDeviceFingerprint: function() {
        const factors = [];

        // Screen characteristics (stable across browsers)
        factors.push(screen.width + 'x' + screen.height);
        factors.push(screen.colorDepth.toString());
        factors.push(screen.pixelDepth.toString());

        // Timezone (stable)
        factors.push(Intl.DateTimeFormat().resolvedOptions().timeZone);

        // Platform info (stable)
        factors.push(navigator.platform || 'unknown');
        factors.push(navigator.hardwareConcurrency || '0');

        // Available fonts (more stable than full list)
        factors.push(this.getStableFontSignature());

        // WebGL renderer (hardware-based, stable)
        factors.push(this.getWebGLRenderer());

        // Audio context sample rate (hardware-dependent)
        factors.push(this.getAudioSignature());

        return factors.join('|');
    },

// Get stable font signature using common fonts
    getStableFontSignature: function() {
        const testFonts = [
            'Arial', 'Times', 'Courier', 'Helvetica', 'Georgia',
            'Verdana', 'Comic Sans MS', 'Trebuchet MS'
        ];

        const available = [];
        const testString = 'mmmmmmmmmmlli';
        const testSize = '72px';

        // Create measurement elements
        const defaultSpan = document.createElement('span');
        defaultSpan.style.position = 'absolute';
        defaultSpan.style.left = '-9999px';
        defaultSpan.style.fontSize = testSize;
        defaultSpan.style.fontFamily = 'monospace';
        defaultSpan.textContent = testString;
        document.body.appendChild(defaultSpan);

        const defaultWidth = defaultSpan.offsetWidth;
        const defaultHeight = defaultSpan.offsetHeight;

        testFonts.forEach(font => {
            const span = document.createElement('span');
            span.style.position = 'absolute';
            span.style.left = '-9999px';
            span.style.fontSize = testSize;
            span.style.fontFamily = font + ', monospace';
            span.textContent = testString;
            document.body.appendChild(span);

            if (span.offsetWidth !== defaultWidth || span.offsetHeight !== defaultHeight) {
                available.push(font);
            }

            document.body.removeChild(span);
        });

        document.body.removeChild(defaultSpan);
        return available.sort().join(',');
    },

// Get WebGL renderer info (hardware-based)
    getWebGLRenderer: function() {
        try {
            const canvas = document.createElement('canvas');
            const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');

            if (!gl) return 'no-webgl';

            const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
            if (debugInfo) {
                const renderer = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL);
                return renderer.substring(0, 50); // Limit length
            }

            return 'webgl-available';
        } catch (e) {
            return 'webgl-error';
        }
    },

// Get audio context characteristics
    getAudioSignature: function() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return 'no-audio';

            const ctx = new AudioContext();
            const signature = ctx.sampleRate.toString();
            ctx.close();

            return signature;
        } catch (e) {
            return 'audio-error';
        }
    },

// Convert fingerprint to consistent GUID format
    fingerprintToGuid: function(fingerprint) {
        // Simple hash function to convert fingerprint to hex
        let hash = 0;
        for (let i = 0; i < fingerprint.length; i++) {
            const char = fingerprint.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash; // Convert to 32-bit integer
        }

        // Convert to positive hex
        const hex = (Math.abs(hash)).toString(16).padStart(8, '0');

        // Create GUID format: 8-4-4-4-12
        const guid = [
            hex.substring(0, 8),
            hex.substring(0, 4),
            '4' + hex.substring(1, 4), // Version 4 indicator
            ((parseInt(hex.substring(0, 1), 16) & 0x3) | 0x8).toString(16) + hex.substring(1, 4),
            hex.padEnd(12, '0').substring(0, 12)
        ].join('-');

        return guid;
    },
    // Simplified device fingerprint using only GUID
    createDeviceFingerprint: function() {
        return this.getOrCreateDeviceGuid();
    },

    //#endregion
    // Check if credentials exist and are valid (not expired)
    areCredentialsValid: async function() {
        try {
            const encryptedCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS);
            const key = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY);
            const iv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV);

            if (!encryptedCredentials || !key || !iv) {
                return false;
            }

            // Decrypt and check expiration
            const decryptedStr = await window.cryptographyHandler.decryptData(
                encryptedCredentials, key, iv
            );
            const credentials = JSON.parse(decryptedStr);

            // Check expiration
            const expiresAt = new Date(credentials.expiresAt);
            const now = new Date();

            if (expiresAt < now) {
                console.log('[OfflineCredentials] Credentials expired, cleaning up');
                this.clearCredentials();
                return false;
            }

            return true;

        } catch (error) {
            console.error('[OfflineCredentials] Validation error:', error);
            this.clearCredentials();
            return false;
        }
    },

    // Simplified credential storage
    storeCredentials: async function(userId, role, key, iv, expirationHours = 12, additionalData = {}) {
        try {
            console.log('[OfflineCredentials] Storing credentials for:', userId);

            // Input validation
            if (!userId || !role || !key || !iv) {
                throw new Error('Missing required parameters');
            }

            // Create expiration timestamp
            const expirationDate = new Date();
            expirationDate.setHours(expirationDate.getHours() + expirationHours);

            // Build minimal credential object
            const credentials = {
                userId: userId,
                role: role.toLowerCase(),
                deviceGuid: this.getOrCreateDeviceGuid(),
                issuedAt: new Date().toISOString(),
                expiresAt: expirationDate.toISOString()
            };

            // Add role-specific data only when provided
            if (role.toLowerCase() === 'lectureradmin' && additionalData.lecturerId) {
                credentials.lecturerId = additionalData.lecturerId;
            }
            if (['student', 'courserepadmin'].includes(role.toLowerCase()) && additionalData.matricNumber) {
                credentials.matricNumber = additionalData.matricNumber;
            }

            // Create signature for integrity
            const credentialsStr = JSON.stringify(credentials);
            const signature = await window.cryptographyHandler.signData(
                credentialsStr,
                credentials.deviceGuid
            );

            const signedCredentials = { ...credentials, signature };

            // Encrypt credentials
            const encryptedCredentials = await window.cryptographyHandler.encryptData(
                JSON.stringify(signedCredentials),
                key,
                iv
            );

            // Store encrypted data
            localStorage.setItem(this.STORAGE_KEYS.CREDENTIALS, encryptedCredentials);
            localStorage.setItem(this.STORAGE_KEYS.AUTH_KEY, key);
            localStorage.setItem(this.STORAGE_KEYS.AUTH_IV, iv);

            console.log('[OfflineCredentials] Credentials stored successfully');
            return true;

        } catch (error) {
            console.error('[OfflineCredentials] Storage failed:', error);
            this.clearCredentials();
            return false;
        }
    },

    // Retrieve and validate credentials
    getCredentials: async function() {
        try {
            // Check validity first (handles expiration cleanup)
            if (!await this.areCredentialsValid()) {
                return null;
            }

            const encryptedCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS);
            const key = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY);
            const iv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV);

            // Decrypt credentials
            const decryptedStr = await window.cryptographyHandler.decryptData(
                encryptedCredentials, key, iv
            );
            const signedCredentials = JSON.parse(decryptedStr);

            // Verify signature integrity
            const { signature, ...credentialsWithoutSignature } = signedCredentials;
            const credentialsStr = JSON.stringify(credentialsWithoutSignature);

            const isValidSignature = await window.cryptographyHandler.verifyHmac(
                credentialsStr,
                signature,
                signedCredentials.deviceGuid
            );

            if (!isValidSignature) {
                console.warn('[OfflineCredentials] Invalid signature');
                this.clearCredentials();
                return null;
            }

            return JSON.stringify(credentialsWithoutSignature);

        } catch (error) {
            console.error('[OfflineCredentials] Retrieval failed:', error);
            this.clearCredentials();
            return null;
        }
    },

    // Simplified getter methods
    getUserRole: async function() {
        const credentials = await this.getCredentials();
        return credentials ? JSON.parse(credentials).role : null;
    },

    getUserId: async function() {
        const credentials = await this.getCredentials();
        return credentials ? JSON.parse(credentials).userId : null;
    },

    getLecturerId: async function() {
        const credentials = await this.getCredentials();
        if (!credentials) return null;

        const parsed = JSON.parse(credentials);
        return parsed.role === 'lectureradmin' ? parsed.lecturerId || null : null;
    },

    getMatricNumber: async function() {
        const credentials = await this.getCredentials();
        if (!credentials) return null;

        const parsed = JSON.parse(credentials);
        const allowedRoles = ['student', 'courserepadmin'];
        return allowedRoles.includes(parsed.role) ? parsed.matricNumber || null : null;
    },

    getDeviceGuid: function() {
        return this.getOrCreateDeviceGuid();
    },

    // Authentication status check
    isAuthenticated: async function() {
        return await this.areCredentialsValid();
    },

    // Clear all credential data
    clearCredentials: function() {
        console.log('[OfflineCredentials] Clearing credentials');

        localStorage.removeItem(this.STORAGE_KEYS.CREDENTIALS);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_KEY);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_IV);
        // Note: DEVICE_GUID persists across sessions

        return true;
    },

    // Debug status information
    getCredentialStatus: async function() {
        try {
            const hasCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS) !== null;
            const hasKey = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY) !== null;
            const hasIv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV) !== null;
            const deviceGuid = this.getDeviceGuid();

            let credentialInfo = null;
            let isValid = false;

            if (hasCredentials && hasKey && hasIv) {
                isValid = await this.areCredentialsValid();
                if (isValid) {
                    const credentials = await this.getCredentials();
                    if (credentials) {
                        const parsed = JSON.parse(credentials);
                        credentialInfo = {
                            userId: parsed.userId,
                            role: parsed.role,
                            expiresAt: parsed.expiresAt,
                            lecturerId: parsed.lecturerId || null,
                            matricNumber: parsed.matricNumber || null
                        };
                    }
                }
            }

            return {
                hasCredentials,
                hasKey,
                hasIv,
                isValid,
                deviceGuid,
                credentialInfo
            };

        } catch (error) {
            console.error('[OfflineCredentials] Status check failed:', error);
            return {
                hasCredentials: false,
                hasKey: false,
                hasIv: false,
                isValid: false,
                deviceGuid: this.getDeviceGuid(),
                error: error.message
            };
        }
    }
};