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
// Device fingerprinting and credential management
window.credentialManager = {
    // Store credentials securely
    storeCredentials: function (username, password, isAdmin, adminId) {
        try {
            // We'll use the C# side for actual encryption, but we can add 
            // additional fingerprinting here
            const fingerprint = this.getDeviceFingerprint();

            // Return success
            return true;
        } catch (error) {
            console.error("Error storing credentials", error);
            return false;
        }
    },

    // Generate a simple fingerprint based on browser and device data
    getDeviceFingerprint: function () {
        const nav = window.navigator;
        const screen = window.screen;

        // Collect various device properties
        const fingerprint = {
            userAgent: nav.userAgent,
            language: nav.language,
            platform: nav.platform,
            cores: nav.hardwareConcurrency || 'unknown',
            screenWidth: screen.width,
            screenHeight: screen.height,
            colorDepth: screen.colorDepth,
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
            touchPoints: nav.maxTouchPoints || 0
        };

        // Convert to string and hash (simple algorithm for demo)
        const fingerprintStr = JSON.stringify(fingerprint);
        return this.simpleHash(fingerprintStr);
    },

    clearCredentials: function () {
        localStorage.removeItem('AirCode_credentials');
    },
    // Simple hash function for demonstration
    simpleHash: function (str) {
        let hash = 0;
        for (let i = 0; i < str.length; i++) {
            const char = str.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash; // Convert to 32bit integer
        }
        return hash.toString(36); // Convert to base-36 for readability
    }
};
