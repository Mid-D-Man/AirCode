// File: wwwroot/js/offlineCredentialsHandler.js
// Enhanced offline credentials handler for AirCode app
// Now includes lecturer ID and matric number support
// Enhanced offline credentials handler for AirCode app
// recent changes made => Simplified device fingerprinting - stable and consistent
// Added automatic expiration checking and cleanup

window.offlineCredentialsHandler = {
    // Storage keys
    STORAGE_KEYS: {
        CREDENTIALS: "AirCode_offline_credentials",
        AUTH_KEY: "aircode_auth_key",
        AUTH_IV: "aircode_auth_iv",
        USER_SESSION: "AirCode_user_session",
        DEVICE_ID: "AirCode_device_id",
        DEVICE_GUID: "AirCode_device_guid" // Persistent GUID
    },

    // Generate a persistent GUID for this device (only created once)
    //not sure how this works but it does
    getOrCreateDeviceGuid: function() {
        let deviceGuid = localStorage.getItem(this.STORAGE_KEYS.DEVICE_GUID);

        if (!deviceGuid) {
            // Generate a new GUID
            deviceGuid = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                const r = Math.random() * 16 | 0;
                const v = c === 'x' ? r : (r & 0x3 | 0x8);
                return v.toString(16);
            });

            localStorage.setItem(this.STORAGE_KEYS.DEVICE_GUID, deviceGuid);
            console.log('[OfflineCredentials] Generated new device GUID:', deviceGuid);
        }

        return deviceGuid;
    },

    // Collect minimal, stable device information for fingerprinting
    collectDeviceInfo: function() {
        try {
            // Create canvas fingerprint for hardware consistency
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            ctx.textBaseline = 'top';
            ctx.font = '14px Arial';
            ctx.fillText('AirCode Device', 2, 2);

            return {
                userAgent: navigator.userAgent || 'unknown',
                language: navigator.language || 'en',
                screenResolution: `${screen.width}x${screen.height}`,
                colorDepth: screen.colorDepth || 24,
                timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
                canvas: canvas.toDataURL(),
                hardwareConcurrency: navigator.hardwareConcurrency || 4,
                deviceGuid: this.getOrCreateDeviceGuid() // Persistent GUID
            };
        } catch (error) {
            console.warn('Error collecting device info:', error);
            // Fallback minimal info
            return {
                userAgent: 'unknown',
                language: 'en',
                screenResolution: '1920x1080',
                colorDepth: 24,
                timezone: 'UTC',
                canvas: 'fallback',
                hardwareConcurrency: 4,
                deviceGuid: this.getOrCreateDeviceGuid()
            };
        }
    },

    // Create a stable device fingerprint hash
    createDeviceFingerprint: async function() {
        const deviceInfo = this.collectDeviceInfo();

        // Create deterministic string (excluding time-dependent data)
        const deterministicInfo = {
            userAgent: deviceInfo.userAgent,
            language: deviceInfo.language,
            screenResolution: deviceInfo.screenResolution,
            colorDepth: deviceInfo.colorDepth,
            timezone: deviceInfo.timezone,
            canvas: deviceInfo.canvas,
            hardwareConcurrency: deviceInfo.hardwareConcurrency,
            deviceGuid: deviceInfo.deviceGuid
        };

        const deviceInfoStr = JSON.stringify(deterministicInfo);

        // Create stable hash
        let hash = 0;
        for (let i = 0; i < deviceInfoStr.length; i++) {
            const char = deviceInfoStr.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash; // Convert to 32-bit integer
        }

        // Create fingerprint ID using GUID prefix for uniqueness
        const deviceGuid = deviceInfo.deviceGuid;
        const guidPrefix = deviceGuid.substring(0, 8); // First 8 chars of GUID
        const fingerprintId = `device_${guidPrefix}_${Math.abs(hash).toString(16)}`;

        // Store device ID in localStorage
        localStorage.setItem(this.STORAGE_KEYS.DEVICE_ID, fingerprintId);

        return fingerprintId;
    },

    // Check if credentials exist and are not expired
    // Returns false if expired or don't exist, automatically cleans up expired credentials
    areCredentialsValid: async function() {
        try {
            console.log('[OfflineCredentials] Checking credential validity...');

            // Get encrypted credentials from storage
            const encryptedCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS);
            const key = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY);
            const iv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV);

            if (!encryptedCredentials || !key || !iv) {
                console.log('[OfflineCredentials] No credentials found');
                return false;
            }

            try {
                // Decrypt credentials to check expiration
                const decryptedStr = await window.cryptographyHandler.decryptData(
                    encryptedCredentials,
                    key,
                    iv
                );

                const signedCredentials = JSON.parse(decryptedStr);

                // Check if credentials have expired
                const expiresAt = new Date(signedCredentials.expiresAt);
                const now = new Date();

                if (expiresAt < now) {
                    console.log('[OfflineCredentials] Credentials have expired, cleaning up...');
                    this.clearCredentials();
                    return false;
                }

                // Additional validation: check device fingerprint
                const currentDeviceFingerprint = await this.createDeviceFingerprint();
                if (signedCredentials.deviceFingerprint &&
                    signedCredentials.deviceFingerprint !== currentDeviceFingerprint) {
                    console.warn('[OfflineCredentials] Device fingerprint mismatch, clearing credentials');
                    this.clearCredentials();
                    return false;
                }

                console.log('[OfflineCredentials] Credentials are valid');
                return true;

            } catch (decryptionError) {
                console.error('[OfflineCredentials] Failed to decrypt credentials:', decryptionError);
                this.clearCredentials();
                return false;
            }

        } catch (error) {
            console.error('[OfflineCredentials] Error checking credential validity:', error);
            return false;
        }
    },

    // Check for expired credentials and clean them up
    // Returns true if credentials were expired and cleaned up, false otherwise
    checkAndCleanExpiredCredentials: async function() {
        try {
            console.log('[OfflineCredentials] Checking for expired credentials...');

            // Get encrypted credentials from storage
            const encryptedCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS);
            const key = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY);
            const iv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV);

            if (!encryptedCredentials || !key || !iv) {
                console.log('[OfflineCredentials] No credentials to check');
                return false;
            }

            try {
                // Decrypt credentials to check expiration
                const decryptedStr = await window.cryptographyHandler.decryptData(
                    encryptedCredentials,
                    key,
                    iv
                );

                const signedCredentials = JSON.parse(decryptedStr);

                // Check if credentials have expired
                const expiresAt = new Date(signedCredentials.expiresAt);
                const now = new Date();

                if (expiresAt < now) {
                    console.log('[OfflineCredentials] Found expired credentials, cleaning up...');
                    this.clearCredentials();
                    return true; // Credentials were expired and cleaned up
                }

                console.log('[OfflineCredentials] Credentials are still valid');
                return false; // Credentials are still valid

            } catch (decryptionError) {
                console.error('[OfflineCredentials] Failed to decrypt credentials during expiration check:', decryptionError);
                console.log('[OfflineCredentials] Clearing potentially corrupted credentials...');
                this.clearCredentials();
                return true; // Credentials were corrupted and cleaned up
            }

        } catch (error) {
            console.error('[OfflineCredentials] Error during expiration check:', error);
            return false;
        }
    },

    // Enhanced credential storage with lecturer ID and matric number support
    // Enhanced credential storage with comprehensive error handling and validation
// Addresses silent failure issues and provides detailed debugging capabilities
    storeCredentials: async function(userId, role, key, iv, expirationHours = 12, additionalData = {}) {
        console.log('[OfflineCredentials] Starting credential storage process...');
        console.log(`[DEBUG] Parameters: userId=${userId}, role=${role}, expirationHours=${expirationHours}`);

        try {
            // Phase 1: Dependency Validation
            if (!window.cryptographyHandler) {
                throw new Error('Cryptography handler not available - ensure cryptographyHandler.js is loaded');
            }

            const requiredMethods = ['encryptData', 'signData'];
            for (const method of requiredMethods) {
                if (typeof window.cryptographyHandler[method] !== 'function') {
                    throw new Error(`Missing cryptography method: ${method}`);
                }
            }
            console.log('[DEBUG] Cryptography handler validation passed');

            // Phase 2: Input Validation
            if (!userId || !role || !key || !iv) {
                throw new Error('Missing required parameters: userId, role, key, or iv');
            }

            // Phase 3: Key/IV Format Validation and Conversion
            let cryptoKey, cryptoIV;
            try {
                // Validate base64 format
                const base64Pattern = /^[A-Za-z0-9+/]*={0,2}$/;
                if (!base64Pattern.test(key)) {
                    throw new Error('Invalid base64 key format');
                }
                if (!base64Pattern.test(iv)) {
                    throw new Error('Invalid base64 IV format');
                }

                // Decode and validate lengths
                const keyBytes = new Uint8Array(atob(key).split('').map(c => c.charCodeAt(0)));
                const ivBytes = new Uint8Array(atob(iv).split('').map(c => c.charCodeAt(0)));

                console.log(`[DEBUG] Decoded lengths - Key: ${keyBytes.length} bytes, IV: ${ivBytes.length} bytes`);

                if (keyBytes.length !== 32) {
                    throw new Error(`Invalid key length: ${keyBytes.length} bytes, expected 32 bytes for AES-256`);
                }
                if (ivBytes.length !== 16) {
                    throw new Error(`Invalid IV length: ${ivBytes.length} bytes, expected 16 bytes for AES`);
                }

                cryptoKey = keyBytes;
                cryptoIV = ivBytes;
                console.log('[DEBUG] Key/IV validation and conversion completed');

            } catch (decodeError) {
                console.error('[ERROR] Key/IV processing failed:', decodeError);
                throw new Error(`Key/IV validation failed: ${decodeError.message}`);
            }

            // Phase 4: Device Fingerprint Generation
            let deviceFingerprint;
            try {
                deviceFingerprint = await this.createDeviceFingerprint();
                console.log(`[DEBUG] Device fingerprint created: ${deviceFingerprint}`);
            } catch (fingerprintError) {
                console.error('[ERROR] Device fingerprint generation failed:', fingerprintError);
                throw new Error(`Device fingerprint generation failed: ${fingerprintError.message}`);
            }

            // Phase 5: Credential Object Construction
            const expirationDate = new Date();
            expirationDate.setHours(expirationDate.getHours() + expirationHours);

            const credentials = {
                userId: userId,
                role: role.toLowerCase(),
                deviceFingerprint: deviceFingerprint,
                issuedAt: new Date().toISOString(),
                expiresAt: expirationDate.toISOString(),
                loginTimestamp: Math.floor(Date.now() / 1000)
            };

            // Phase 6: Role-Specific Data Integration
            switch (role.toLowerCase()) {
                case 'lectureradmin':
                    if (additionalData.lecturerId) {
                        credentials.lecturerId = additionalData.lecturerId;
                        console.log(`[DEBUG] Added lecturer ID: ${additionalData.lecturerId}`);
                    }
                    break;

                case 'student':
                case 'courserepadmin':
                    if (additionalData.matricNumber) {
                        credentials.matricNumber = additionalData.matricNumber;
                        console.log(`[DEBUG] Added matric number: ${additionalData.matricNumber}`);
                    }
                    break;

                default:
                    console.log(`[DEBUG] No additional data required for role: ${role}`);
            }

            // Phase 7: Digital Signature Generation
            let signature;
            try {
                const credentialsStr = JSON.stringify(credentials);
                signature = await window.cryptographyHandler.signData(credentialsStr, deviceFingerprint);
                console.log('[DEBUG] Digital signature generated successfully');
            } catch (signError) {
                console.error('[ERROR] Signature generation failed:', signError);
                throw new Error(`Signature generation failed: ${signError.message}`);
            }

            // Phase 8: Signed Credential Assembly
            const signedCredentials = {
                ...credentials,
                signature: signature
            };

            // Phase 9: Encryption Process
            let encryptedCredentials;
            try {
                const credentialsPayload = JSON.stringify(signedCredentials);
                console.log(`[DEBUG] Payload size: ${credentialsPayload.length} characters`);

                encryptedCredentials = await window.cryptographyHandler.encryptData(
                    credentialsPayload,
                    cryptoKey,
                    cryptoIV
                );
                console.log('[DEBUG] Credential encryption completed');
            } catch (encryptError) {
                console.error('[ERROR] Encryption failed:', encryptError);
                throw new Error(`Encryption failed: ${encryptError.message}`);
            }

            // Phase 10: Storage Operations
            try {
                // Store encryption keys for decryption
                localStorage.setItem(this.STORAGE_KEYS.AUTH_KEY, key);
                localStorage.setItem(this.STORAGE_KEYS.AUTH_IV, iv);
                console.log('[DEBUG] Encryption keys stored');

                // Store encrypted credentials
                localStorage.setItem(this.STORAGE_KEYS.CREDENTIALS, encryptedCredentials);
                console.log('[DEBUG] Encrypted credentials stored');

                // Create and store user session data
                const userData = {
                    userId: userId,
                    role: role.toLowerCase(),
                    deviceId: deviceFingerprint,
                    lecturerId: additionalData.lecturerId || '',
                    matricNumber: additionalData.matricNumber || '',
                    loginTimestamp: credentials.loginTimestamp
                };

                localStorage.setItem(this.STORAGE_KEYS.USER_SESSION, JSON.stringify(userData));
                console.log('[DEBUG] User session data stored');

            } catch (storageError) {
                console.error('[ERROR] localStorage operations failed:', storageError);
                throw new Error(`Storage operation failed: ${storageError.message}`);
            }

            // Phase 11: Verification
            try {
                const storedCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS);
                const storedKey = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY);
                const storedIV = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV);

                if (!storedCredentials || !storedKey || !storedIV) {
                    throw new Error('Storage verification failed - items not found in localStorage');
                }

                console.log('[DEBUG] Storage verification successful');
                console.log(`[DEBUG] Stored credential length: ${storedCredentials.length} characters`);

            } catch (verificationError) {
                console.error('[ERROR] Storage verification failed:', verificationError);
                // Cleanup partial storage
                this.clearCredentials();
                throw new Error(`Storage verification failed: ${verificationError.message}`);
            }

            console.log('[SUCCESS] Credentials stored successfully for user:', userId);
            return true;

        } catch (error) {
            console.error('[CRITICAL ERROR] Credential storage process failed:', error);

            // Store detailed error information for debugging
            const errorDetails = {
                error: error.message,
                stack: error.stack,
                timestamp: new Date().toISOString(),
                userId: userId,
                role: role,
                phase: this.getErrorPhase(error.message),
                browserInfo: {
                    userAgent: navigator.userAgent,
                    localStorage: typeof Storage !== 'undefined'
                }
            };

            try {
                localStorage.setItem('AirCode_last_storage_error', JSON.stringify(errorDetails));
            } catch (debugStorageError) {
                console.error('[ERROR] Could not store debug information:', debugStorageError);
            }

            return false;
        }
    },

// Helper method to identify error phase for debugging
    getErrorPhase: function(errorMessage) {
        const phaseKeywords = [
            { phase: 'dependency_validation', keywords: ['cryptography handler', 'not available', 'missing'] },
            { phase: 'input_validation', keywords: ['missing required parameters'] },
            { phase: 'key_iv_processing', keywords: ['base64', 'invalid key', 'invalid iv', 'validation failed'] },
            { phase: 'device_fingerprint', keywords: ['device fingerprint', 'fingerprint generation'] },
            { phase: 'signature_generation', keywords: ['signature generation', 'signData'] },
            { phase: 'encryption', keywords: ['encryption failed', 'encryptData'] },
            { phase: 'storage_operations', keywords: ['localStorage', 'storage operation'] },
            { phase: 'verification', keywords: ['storage verification', 'verification failed'] }
        ];

        const lowerMessage = errorMessage.toLowerCase();
        for (const phaseInfo of phaseKeywords) {
            if (phaseInfo.keywords.some(keyword => lowerMessage.includes(keyword))) {
                return phaseInfo.phase;
            }
        }

        return 'unknown';
    },

    // Enhanced credential retrieval with validation
    getCredentials: async function() {
        try {
            // First check if credentials are valid (this will clean up expired ones)
            const areValid = await this.areCredentialsValid();
            if (!areValid) {
                return null;
            }

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
                this.clearCredentials();
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
            this.clearCredentials();
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

    // Get the persistent device GUID
    getDeviceGuid: function() {
        return this.getOrCreateDeviceGuid();
    },

    // Get user session data
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
    // This method matches what the C# service expects
    isAuthenticated: async function() {
        try {
            return await this.areCredentialsValid();
        } catch (error) {
            console.error('[OfflineCredentials] Failed to check authentication status:', error);
            return false;
        }
    },

    // Clear all stored credentials and related data
    clearCredentials: function() {
        console.log('[OfflineCredentials] Clearing all stored credentials');

        // Remove credential-related items
        localStorage.removeItem(this.STORAGE_KEYS.CREDENTIALS);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_KEY);
        localStorage.removeItem(this.STORAGE_KEYS.AUTH_IV);
        localStorage.removeItem(this.STORAGE_KEYS.USER_SESSION);
        // Note: We keep DEVICE_ID and DEVICE_GUID as they should persist

        return true;
    },

    // Debug function to get credential status
    getCredentialStatus: async function() {
        try {
            const hasCredentials = localStorage.getItem(this.STORAGE_KEYS.CREDENTIALS) !== null;
            const hasKey = localStorage.getItem(this.STORAGE_KEYS.AUTH_KEY) !== null;
            const hasIv = localStorage.getItem(this.STORAGE_KEYS.AUTH_IV) !== null;
            const hasSession = localStorage.getItem(this.STORAGE_KEYS.USER_SESSION) !== null;
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
                            lecturerId: parsed.lecturerId || 'N/A',
                            matricNumber: parsed.matricNumber || 'N/A'
                        };
                    }
                }
            }

            return {
                hasCredentials,
                hasKey,
                hasIv,
                hasSession,
                isValid,
                deviceGuid,
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
                deviceGuid: this.getDeviceGuid(),
                error: error.message
            };
        }
    }
};