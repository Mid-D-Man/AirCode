// wwwroot/js/connectivityServices.js
//only for connetivity check
// Connectivity checker service
console.log("connectivityServices.js loaded");

window.connectivityChecker = {
    dotNetReference: null,
    isOnline: navigator.onLine,
    checkIntervalId: null,
    // Add a method to get the online status .
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

