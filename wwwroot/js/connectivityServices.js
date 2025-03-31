// wwwroot/js/connectivityServices.js

// Connectivity checker service
console.log("connectivityServices.js loaded");

window.connectivityChecker = {
    dotNetReference: null,
    isOnline: navigator.onLine,
    checkIntervalId: null,
    // Add a method to get the online status
    getOnlineStatus: function() {
        console.log("connectivityChecker.getOnlineStatus called, isOnline:", this.isOnline);
        return this.isOnline;
    },

    init: function (dotNetRef) {
        console.log("connectivityChecker.init called with dotNetRef:", dotNetRef);
        this.dotNetReference = dotNetRef;

        // Add event listeners for online/offline events
        window.addEventListener('online', this.handleOnlineEvent.bind(this));
        window.addEventListener('offline', this.handleOfflineEvent.bind(this));

        // Setup periodic check every 30 seconds
        this.checkIntervalId = setInterval(this.checkConnectivity.bind(this), 30000);

        return true;
    },


    handleOnlineEvent: function () {
        console.log("handleOnlineEvent called.");
        if (!this.isOnline) {
            this.isOnline = true;
            this.notifyDotNet(true);
        }
    },

    handleOfflineEvent: function () {
        console.log("handleOfflineEvent called.");
        if (this.isOnline) {
            this.isOnline = false;
            this.notifyDotNet(false);
        }
    },

    checkConnectivity: function () {
        const currentStatus = navigator.onLine;
        if (this.isOnline !== currentStatus) {
            console.log("checkConnectivity: status changed from", this.isOnline, "to", currentStatus);
            this.isOnline = currentStatus;
            this.notifyDotNet(currentStatus);
        }
    },

    notifyDotNet: function (isOnline) {
        console.log("notifyDotNet called with isOnline:", isOnline);
        if (this.dotNetReference) {
            this.dotNetReference.invokeMethodAsync('OnConnectivityChanged', isOnline)
                .then(() => { console.log("DotNet notified successfully."); })
                .catch(err => { console.error("Error notifying DotNet:", err); });
        } else {
            console.warn("dotNetReference is not set, cannot notify DotNet.");
        }
    },


    dispose: function () {
        console.log("Disposing connectivityChecker...");
        window.removeEventListener('online', this.handleOnlineEvent);
        window.removeEventListener('offline', this.handleOfflineEvent);

        if (this.checkIntervalId) {
            clearInterval(this.checkIntervalId);
            this.checkIntervalId = null;
        }

        this.dotNetReference = null;
    }

};

// Offline manager for showing prompts and managing offline state
window.offlineManager = {
    showOfflinePrompt: function () {
        return new Promise(resolve => {
            if (!confirm("You appear to be offline. Would you like to continue in offline mode?")) {
                resolve(false);
                return;
            }
            resolve(true);
        });
    },

    showOnlinePrompt: function () {
        return new Promise(resolve => {
            if (!confirm("You're back online! Would you like to switch to online mode?")) {
                resolve(false);
                return;
            }
            resolve(true);
        });
    },

    showNoCredentialsMessage: function () {
        alert("Offline mode requires that you have previously logged in on this device. Please connect to the internet and login at least once.");
    },

    hasStoredCredentials: function () {
        const credentials = localStorage.getItem('AirCode_credentials');
        return credentials !== null && credentials !== undefined;
    }
};

// Credential manager for securely storing and retrieving credentials
window.credentialManager = {
    storeCredentials: function (username, password, isAdmin, adminId) {
        // In a real application, you should use a more secure method
        // like the Credential Management API or IndexedDB with encryption
        try {
            // Create a credential object
            const credentials = {
                username: username,
                // Store a hashed version of the password, never the plain password
                // This is a simple hash for demonstration, use a proper hashing in production
                passwordHash: this.simpleHash(password),
                isAdmin: isAdmin,
                adminId: adminId,
                deviceId: this.getDeviceFingerprint(),
                timestamp: new Date().getTime()
            };

            // Store encrypted credentials
            localStorage.setItem('AirCode_credentials', this.encryptData(JSON.stringify(credentials)));
            return true;
        } catch (error) {
            console.error("Error storing credentials:", error);
            return false;
        }
    },

    getStoredCredentials: function () {
        try {
            const encryptedCredentials = localStorage.getItem('AirCode_credentials');
            if (!encryptedCredentials) return null;

            const credentialsJson = this.decryptData(encryptedCredentials);
            return JSON.parse(credentialsJson);
        } catch (error) {
            console.error("Error retrieving stored credentials:", error);
            return null;
        }
    },

    clearCredentials: function () {
        localStorage.removeItem('AirCode_credentials');
    },

    // Simple hash function (for demonstration only)
    // In production, use a proper cryptographic hash function
    simpleHash: function (input) {
        let hash = 0;
        if (input.length === 0) return hash;

        for (let i = 0; i < input.length; i++) {
            const char = input.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash; // Convert to 32bit integer
        }

        return hash.toString(16);
    },

    // Device fingerprinting (simplified)
    getDeviceFingerprint: function () {
        const screenPrint = `${screen.height}x${screen.width}x${screen.colorDepth}`;
        const userAgent = navigator.userAgent;
        const timeZoneOffset = new Date().getTimezoneOffset();
        const language = navigator.language;

        return this.simpleHash(`${screenPrint}-${userAgent}-${timeZoneOffset}-${language}`);
    },

    // Simple encryption/decryption for demonstration
    // In production, use a proper encryption library
    encryptData: function (data) {
        // This is a placeholder for a real encryption implementation
        // For production, use the Web Crypto API or a library like CryptoJS
        return btoa(data); // Base64 encoding is NOT encryption
    },

    decryptData: function (encryptedData) {
        // This is a placeholder for a real decryption implementation
        return atob(encryptedData); // Base64 decoding is NOT decryption
    }
};
