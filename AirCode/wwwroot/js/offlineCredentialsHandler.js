//  offline credentials handler - optimized for reliability
window.offlineCredentialsHandler = (() => {
    'use strict';

    const STORAGE_KEYS = {
        CREDENTIALS: "AirCode_offline_credentials",
        AUTH_KEY: "aircode_auth_key", 
        AUTH_IV: "aircode_auth_iv",
        DEVICE_GUID: "AirCode_device_guid"
    };
 
    // Immediate device GUID initialization
    let deviceGuid = null;
    
    const initializeDeviceGuid = () => {
        if (!deviceGuid) {
            deviceGuid = localStorage.getItem(STORAGE_KEYS.DEVICE_GUID);
            if (!deviceGuid) {
                deviceGuid = `${Date.now().toString(16).slice(-8)}${Math.random().toString(16).slice(2,10)}`;
                localStorage.setItem(STORAGE_KEYS.DEVICE_GUID, deviceGuid);
                console.log('[OfflineCredentials] Device GUID created -> ', deviceGuid);
            }
        }
        return deviceGuid;
    };

    // Initialize immediately
    initializeDeviceGuid();

    const logDebug = (message, data = null) => {
        console.log(`[OfflineCredentials] ${message}`, data || '');
    };

    const logError = (message, error = null) => {
        console.error(`[OfflineCredentials] ${message}`, error || '');
    };

    return {
        // Public methods
        getOrCreateDeviceGuid() {
            return initializeDeviceGuid();
        },

        createDeviceFingerprint() {
            return this.getOrCreateDeviceGuid();
        },

        async areCredentialsValid() {
            try {
                const encryptedCredentials = localStorage.getItem(STORAGE_KEYS.CREDENTIALS);
                const key = localStorage.getItem(STORAGE_KEYS.AUTH_KEY);
                const iv = localStorage.getItem(STORAGE_KEYS.AUTH_IV);

                if (!encryptedCredentials || !key || !iv) {
                    logDebug('Missing credential components');
                    return false;
                }

                // Verify cryptography handler exists
                if (!window.cryptographyHandler?.decryptData) {
                    logError('Cryptography handler not available');
                    return false;
                }

                const decryptedStr = await window.cryptographyHandler.decryptData(
                    encryptedCredentials, key, iv
                );
                
                if (!decryptedStr) {
                    logError('Decryption failed');
                    this.clearCredentials();
                    return false;
                }

                const credentials = JSON.parse(decryptedStr);
                const expiresAt = new Date(credentials.expiresAt);
                const now = new Date();

                if (expiresAt < now) {
                    logDebug('Credentials expired, cleaning up');
                    this.clearCredentials();
                    return false;
                }

                return true;

            } catch (error) {
                logError('Validation error:', error);
                this.clearCredentials();
                return false;
            }
        },

        async storeCredentials(userId, role, key, iv, expirationHours = 12, additionalData = {}) {
            try {
                logDebug(`Storing credentials for: ${userId}, role: ${role}, expiry: ${expirationHours}h`);

                // Input validation
                if (!userId?.trim() || !role?.trim() || !key?.trim() || !iv?.trim()) {
                    throw new Error('Missing required parameters');
                }

                // Verify cryptography handler
                if (!window.cryptographyHandler?.encryptData || !window.cryptographyHandler?.signData) {
                    throw new Error('Cryptography handler not available');
                }

                // Create expiration timestamp
                const expirationDate = new Date();
                expirationDate.setHours(expirationDate.getHours() + expirationHours);

                const currentDeviceGuid = this.getOrCreateDeviceGuid();
                
                // Build credential object
                const credentials = {
                    userId: userId.trim(),
                    role: role.toLowerCase().trim(),
                    deviceGuid: currentDeviceGuid,
                    issuedAt: new Date().toISOString(),
                    expiresAt: expirationDate.toISOString()
                };

                // Add role-specific data
                const normalizedRole = role.toLowerCase().trim();
                if (normalizedRole === 'lectureradmin' && additionalData?.lecturerId?.trim()) {
                    credentials.lecturerId = additionalData.lecturerId.trim();
                    logDebug('Added lecturer ID');
                }
                
                if (['student', 'courserepadmin'].includes(normalizedRole) && additionalData?.matricNumber?.trim()) {
                    credentials.matricNumber = additionalData.matricNumber.trim();
                    logDebug('Added matric number');
                }

                // Create signature
                const credentialsStr = JSON.stringify(credentials);
                const signature = await window.cryptographyHandler.signData(
                    credentialsStr, currentDeviceGuid
                );

                if (!signature) {
                    throw new Error('Failed to create signature');
                }

                const signedCredentials = { ...credentials, signature };

                // Encrypt credentials
                const encryptedCredentials = await window.cryptographyHandler.encryptData(
                    JSON.stringify(signedCredentials), key, iv
                );

                if (!encryptedCredentials) {
                    throw new Error('Encryption failed');
                }

                // Atomic storage operation
                localStorage.setItem(STORAGE_KEYS.CREDENTIALS, encryptedCredentials);
                localStorage.setItem(STORAGE_KEYS.AUTH_KEY, key);
                localStorage.setItem(STORAGE_KEYS.AUTH_IV, iv);

                logDebug('Credentials stored successfully');
                
                // Immediate validation
                const isValid = await this.areCredentialsValid();
                if (!isValid) {
                    throw new Error('Stored credentials failed validation');
                }

                return true;

            } catch (error) {
                logError('Storage failed:', error);
                this.clearCredentials();
                return false;
            }
        },

        async getCredentials() {
            try {
                if (!await this.areCredentialsValid()) {
                    return null;
                }

                const encryptedCredentials = localStorage.getItem(STORAGE_KEYS.CREDENTIALS);
                const key = localStorage.getItem(STORAGE_KEYS.AUTH_KEY);
                const iv = localStorage.getItem(STORAGE_KEYS.AUTH_IV);

                const decryptedStr = await window.cryptographyHandler.decryptData(
                    encryptedCredentials, key, iv
                );
                
                const signedCredentials = JSON.parse(decryptedStr);
                const { signature, ...credentialsWithoutSignature } = signedCredentials;

                // Verify signature integrity
                if (window.cryptographyHandler.verifyHmac) {
                    const credentialsStr = JSON.stringify(credentialsWithoutSignature);
                    const isValidSignature = await window.cryptographyHandler.verifyHmac(
                        credentialsStr, signature, signedCredentials.deviceGuid
                    );

                    if (!isValidSignature) {
                        logError('Invalid signature detected');
                        this.clearCredentials();
                        return null;
                    }
                }

                return JSON.stringify(credentialsWithoutSignature);

            } catch (error) {
                logError('Retrieval failed:', error);
                this.clearCredentials();
                return null;
            }
        },

        async getUserRole() {
            try {
                const credentials = await this.getCredentials();
                return credentials ? JSON.parse(credentials).role : null;
            } catch (error) {
                logError('Get user role failed:', error);
                return null;
            }
        },

        async getUserId() {
            try {
                const credentials = await this.getCredentials();
                return credentials ? JSON.parse(credentials).userId : null;
            } catch (error) {
                logError('Get user ID failed:', error);
                return null;
            }
        },

        async getLecturerId() {
            try {
                const credentials = await this.getCredentials();
                if (!credentials) return null;

                const parsed = JSON.parse(credentials);
                return parsed.role === 'lectureradmin' ? (parsed.lecturerId || null) : null;
            } catch (error) {
                logError('Get lecturer ID failed:', error);
                return null;
            }
        },

        async getMatricNumber() {
            try {
                const credentials = await this.getCredentials();
                if (!credentials) return null;

                const parsed = JSON.parse(credentials);
                const allowedRoles = ['student', 'courserepadmin'];
                return allowedRoles.includes(parsed.role) ? (parsed.matricNumber || null) : null;
            } catch (error) {
                logError('Get matric number failed:', error);
                return null;
            }
        },

        getDeviceGuid() {
            return this.getOrCreateDeviceGuid();
        },

        async isAuthenticated() {
            return await this.areCredentialsValid();
        },

        clearCredentials() {
            logDebug('Clearing credentials');
            
            localStorage.removeItem(STORAGE_KEYS.CREDENTIALS);
            localStorage.removeItem(STORAGE_KEYS.AUTH_KEY);
            localStorage.removeItem(STORAGE_KEYS.AUTH_IV);
            // Device GUID persists

            return true;
        },

        async getCredentialStatus() {
            try {
                const hasCredentials = localStorage.getItem(STORAGE_KEYS.CREDENTIALS) !== null;
                const hasKey = localStorage.getItem(STORAGE_KEYS.AUTH_KEY) !== null;
                const hasIv = localStorage.getItem(STORAGE_KEYS.AUTH_IV) !== null;
                const currentDeviceGuid = this.getDeviceGuid();

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
                    deviceGuid: currentDeviceGuid,
                    credentialInfo,
                    cryptographyHandlerAvailable: !!window.cryptographyHandler,
                    timestamp: new Date().toISOString()
                };

            } catch (error) {
                logError('Status check failed:', error);
                return {
                    hasCredentials: false,
                    hasKey: false,
                    hasIv: false,
                    isValid: false,
                    deviceGuid: this.getDeviceGuid(),
                    error: error.message,
                    timestamp: new Date().toISOString()
                };
            }
        }
    };
})();

// Immediate initialization verification
document.addEventListener('DOMContentLoaded', () => {
    console.log('[OfflineCredentials] Handler initialized, Device GUID:', 
        window.offlineCredentialsHandler.getDeviceGuid());
});
