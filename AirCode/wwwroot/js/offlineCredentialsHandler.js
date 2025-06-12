// File: wwwroot/js/offlineCredentialsHandler.js
// Enhanced offline credentials handler for AirCode app
// Now includes lecturer ID and matric number support

window.offlineCredentialsHandler = {
    // Storage keys
    STORAGE_KEYS: {
        CREDENTIALS: "AirCode_offline_credentials",
        AUTH_KEY: "aircode_auth_key",
        AUTH_IV: "aircode_auth_iv",
        USER_SESSION: "AirCode_user_session", // Match AuthService
        DEVICE_ID: "AirCode_device_id" // Match AuthService
    },

    // Collect comprehensive device information for fingerprinting
    collectDeviceInfo: function() {
        try {
            // Create canvas fingerprint (similar to AuthService GetDeviceIdAsync)
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            ctx.textBaseline = 'top';
            ctx.font = '14px Arial';
            ctx.fillText('Device fingerprint', 2, 2);

            return {
                userAgent: navigator.userAgent,
                language: navigator.language,
                languages: navigator.languages ? navigator.languages.join(',') : '',
                platform: navigator.platform,
                screenWidth: window.screen.width,
                screenHeight: window.screen.height,
                screenResolution: screen.width + 'x' + screen.height,
                colorDepth: screen.colorDepth,
                timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
                timezoneOffset: new Date().getTimezoneOffset(),
                canvas: canvas.toDataURL(),
                cookieEnabled: navigator.cookieEnabled,
                doNotTrack: navigator.doNotTrack,
                hardwareConcurrency: navigator.hardwareConcurrency || 'unknown'
            };
        } catch (error) {
            console.warn('Error collecting device info:', error);
            // Fallback minimal info
            return {
                userAgent: navigator.userAgent || 'unknown',
                language: navigator.language || 'en',
                platform: navigator.platform || 'unknown',
                screenWidth: window.screen.width || 0,
                screenHeight: window.screen.height || 0,
                timezone: 'UTC'
            };
        }
    },

    // Create a device fingerprint hash (similar to AuthService logic)
    createDeviceFingerprint: async function() {
        const deviceInfo = this.collectDeviceInfo();
        const deviceInfoStr = JSON.stringify(deviceInfo);

        // Create hash similar to AuthService method
        let hash = 0;
        for (let i = 0; i < deviceInfoStr.length; i++) {
            const char = deviceInfoStr.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash; // Convert to 32-bit integer
        }

        const fingerprintId = 'device_' + Math.abs(hash).toString(16) + '_' + Date.now().toString(16);

        // Store device ID in localStorage (matching AuthService)
        localStorage.setItem(this.STORAGE_KEYS.DEVICE_ID, fingerprintId);

        return fingerprintId;
    },

    // Enhanced credential storage with lecturer ID and matric number support
    storeCredentials: async function(userId, role, key, iv, expirationHours = 12, additionalData = {}) {
        try {
            console.log('[OfflineCredentials] Storing credentials for user:', userId, 'role:', role);

            // Generate device fingerprint
            const deviceFingerprint = await this.createDeviceFingerprint();

            // Calculate expiration date (hours instead of days to match AuthService)
            const expirationDate = new Date();
            expirationDate.setHours(expirationDate.getHours() + expirationHours);

            // Create base credentials object
            const credentials = {
                userId: userId,
                role: role.toLowerCase(), // Normalize role case
                deviceFingerprint: deviceFingerprint,
                issuedAt: new Date().toISOString(),
                expiresAt: expirationDate.toISOString(),
                loginTimestamp: Math.floor(Date.now() / 1000) // Unix timestamp like AuthService
            };

            // Add role-specific data based on user role
            switch (role.toLowerCase()) {
                case 'lectureradmin':
                    if (additionalData.lecturerId) {
                        credentials.lecturerId = additionalData.lecturerId;
                        console.log('[OfflineCredentials] Added lecturer ID:', additionalData.lecturerId);
                    }
                    break;

                case 'student':
                case 'courseadmin':
                    if (additionalData.matricNumber) {
                        credentials.matricNumber = additionalData.matricNumber;
                        console.log('[OfflineCredentials] Added matric number:', additionalData.matricNumber);
                    }
                    break;

                default:
                    console.log('[OfflineCredentials] No additional data needed for role:', role);
            }

            // Store the key and IV for later decryption
            localStorage.setItem(this.STORAGE_KEYS.AUTH_KEY, key);
            localStorage.setItem(this.STORAGE_KEYS.AUTH_IV, iv);

            // Create user session data (matching AuthService structure)
            const userData = {
                userId: userId,
                role: role.toLowerCase(),
                deviceId: deviceFingerprint,
                lecturerId: additionalData.lecturerId || '',
                matricNumber: additionalData.matricNumber || '',
                loginTimestamp: credentials.loginTimestamp
            };

            localStorage.setItem(this.STORAGE_KEYS.USER_SESSION, JSON.stringify(userData));

            // Create signature for credentials integrity
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

            // Store encrypted credentials
            localStorage.setItem(this.STORAGE_KEYS.CREDENTIALS, encryptedCredentials);

            console.log('[OfflineCredentials] Credentials stored successfully');
            return true;

        } catch (error) {
            console.error('[OfflineCredentials] Failed to store credentials:', error);
            return false;
        }
    },

    // Enhanced credential retrieval with validation
    getCredentials: async function() {
        try {
            // Get encrypted credentials from storage
            const encryptedCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS);
            const key = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY);
            const iv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV);

            if (!encryptedCredentials || !key || !iv) {
                console.warn('[OfflineCredentials] Missing credential components');
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
            const now = new Date();

            if (expiresAt < now) {
                console.warn('[OfflineCredentials] Credentials have expired');
                this.clearCredentials(); // Auto-cleanup expired credentials
                return null;
            }

            // Get current device fingerprint for validation
            const currentDeviceFingerprint = await this.createDeviceFingerprint();

            // Verify device hasn't changed significantly
            if (signedCredentials.deviceFingerprint &&
                !this.isDeviceFingerprintValid(currentDeviceFingerprint, signedCredentials.deviceFingerprint)) {
                console.warn('[OfflineCredentials] Device fingerprint validation failed');
                return null;
            }

            // Verify signature integrity
            const { signature, ...credentialsWithoutSignature } = signedCredentials;
            const credentialsStr = JSON.stringify(credentialsWithoutSignature);

            const isValid = await window.cryptographyHandler.verifyHmac(
                credentialsStr,
                signature,
                signedCredentials.deviceFingerprint
            );

            if (!isValid) {
                console.warn('[OfflineCredentials] Invalid signature on credentials');
                this.clearCredentials(); // Auto-cleanup corrupted credentials
                return null;
            }

            // Return validated credentials
            const validCredentials = {
                userId: signedCredentials.userId,
                role: signedCredentials.role,
                issuedAt: signedCredentials.issuedAt,
                expiresAt: signedCredentials.expiresAt,
                loginTimestamp: signedCredentials.loginTimestamp
            };

            // Add role-specific data if present
            if (signedCredentials.lecturerId) {
                validCredentials.lecturerId = signedCredentials.lecturerId;
            }
            if (signedCredentials.matricNumber) {
                validCredentials.matricNumber = signedCredentials.matricNumber;
            }

            console.log('[OfflineCredentials] Credentials retrieved successfully for user:', validCredentials.userId);
            return JSON.stringify(validCredentials);

        } catch (error) {
            console.error('[OfflineCredentials] Failed to retrieve credentials:', error);
            this.clearCredentials(); // Cleanup on error
            return null;
        }
    },

    // Device fingerprint validation with some tolerance for minor changes
    isDeviceFingerprintValid: function(current, stored) {
        if (current === stored) return true;

        // Extract the hash part from device IDs for comparison
        try {
            const currentHash = current.split('_')[1];
            const storedHash = stored.split('_')[1];
            return currentHash === storedHash;
        } catch (error) {
            console.warn('[OfflineCredentials] Error comparing device fingerprints:', error);
            return false;
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
            console.error('[OfflineCredentials] Failed to get user role:', error);
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
            console.error('[OfflineCredentials] Failed to get user ID:', error);
            return null;
        }
    },

    // Get lecturer ID (for lecturer admin users)
    getLecturerId: async function() {
        try {
            const credentials = await this.getCredentials();
            if (!credentials) return null;

            const parsedCredentials = JSON.parse(credentials);

            // Only return lecturer ID for lecturer admin role
            if (parsedCredentials.role === 'lectureradmin') {
                return parsedCredentials.lecturerId || null;
            }

            return null;
        } catch (error) {
            console.error('[OfflineCredentials] Failed to get lecturer ID:', error);
            return null;
        }
    },

    // Get matric number (for student and course admin users)
    getMatricNumber: async function() {
        try {
            const credentials = await this.getCredentials();
            if (!credentials) return null;

            const parsedCredentials = JSON.parse(credentials);

            // Only return matric number for student or course admin roles
            const allowedRoles = ['student', 'courseadmin'];
            if (allowedRoles.includes(parsedCredentials.role)) {
                return parsedCredentials.matricNumber || null;
            }

            return null;
        } catch (error) {
            console.error('[OfflineCredentials] Failed to get matric number:', error);
            return null;
        }
    },

    // Get device ID
    getDeviceId: async function() {
        try {
            const storedDeviceId = localStorage.getItem(this.STORAGE_KEYS.DEVICE_ID);
            if (storedDeviceId) {
                return storedDeviceId;
            }

            // Generate new device ID if not found
            return await this.createDeviceFingerprint();
        } catch (error) {
            console.error('[OfflineCredentials] Failed to get device ID:', error);
            return 'device_fallback_' + Date.now().toString(16);
        }
    },

    // Get user session data (matching AuthService structure)
    getUserSession: function() {
        try {
            const sessionData = localStorage.getItem(this.STORAGE_KEYS.USER_SESSION);
            return sessionData ? JSON.parse(sessionData) : null;
        } catch (error) {
            console.error('[OfflineCredentials] Failed to get user session:', error);
            return null;
        }
    },

    // Check if credentials are valid and not expired
    isAuthenticated: async function() {
        try {
            const credentials = await this.getCredentials();
            return credentials !== null;
        } catch (error) {
            console.error('[OfflineCredentials] Failed to check authentication status:', error);
            return false;
        }
    },

    // Clear all stored credentials and related data
    clearCredentials: function() {
        console.log('[OfflineCredentials] Clearing all stored credentials');

        // Remove all credential-related items
        localStorage.removeItem(this.STORAGE_KEYS.CREDENTIALS);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_KEY);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_IV);
        localStorage.removeItem(this.STORAGE_KEYS.USER_SESSION);
        // Note: We keep DEVICE_ID as it should persist across sessions

        return true;
    },

    // Debug function to get credential status
    getCredentialStatus: async function() {
        try {
            const hasCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS) !== null;
            const hasKey = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY) !== null;
            const hasIv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV) !== null;
            const hasSession = localStorage.getItem(this.STORAGE_KEYS.USER_SESSION) !== null;

            let credentialInfo = null;
            if (hasCredentials && hasKey && hasIv) {
                const credentials = await this.getCredentials();
                if (credentials) {
                    const parsed = JSON.parse(credentials);
                    credentialInfo = {
                        userId: parsed.userId,
                        role: parsed.role,
                        expiresAt: parsed.expiresAt,
                        lecturerId: parsed.lecturerId || 'N/A',
                        matricNumber: parsed.matricNumber || 'N/A'
                    };
                }
            }

            return {
                hasCredentials,
                hasKey,
                hasIv,
                hasSession,
                isValid: credentialInfo !== null,
                credentialInfo
            };
        } catch (error) {
            console.error('[OfflineCredentials] Failed to get credential status:', error);
            return {
                hasCredentials: false,
                hasKey: false,
                hasIv: false,
                hasSession: false,
                isValid: false,
                error: error.message
            };
        }
    }
};