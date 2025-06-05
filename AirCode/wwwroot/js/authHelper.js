window.authHelper = {
    logAuthMessage: function (message) {
        console.log(`[Authentication] -> ${message}`);
    },

    storeJwtLocally: function (token) {
        // Store JWT in sessionStorage for retrieval elsewhere
        // Note: This is just for development/debugging purposes
        //yahoo ai ai yahoo ai ai  ,, 
        if (token) {
            sessionStorage.setItem('debug_jwt_token', token);
            console.log('[Authentication] -> JWT stored in sessionStorage for debugging');
            return true;
        }
        return false;
    },

    retrieveStoredJwt: function () {
        return sessionStorage.getItem('debug_jwt_token') || null;
    },

    clearStoredJwt: function () {
        sessionStorage.removeItem('debug_jwt_token');
        console.log('[Authentication] -> JWT cleared from sessionStorage');
    },

    // Function to extract tokens from browser storage directly
    extractAuthTokensFromStorage: function() {
        try {
            // Try to find authentication tokens in local storage
            const keys = Object.keys(localStorage);
            for (let i = 0; i < keys.length; i++) {
                const key = keys[i];
                if (key.includes('auth') || key.includes('token') || key.includes('oidc')) {
                    console.log(`[Authentication] -> Found potential token in localStorage: ${key}`);
                    try {
                        const value = localStorage.getItem(key);
                        // If it looks like a JWT (contains periods and is base64-ish)
                        if (value && value.includes('.') && value.length > 50) {
                            console.log(`[Authentication] -> This appears to be a JWT token`);
                            return value;
                        }
                    } catch (e) {
                        console.log(`[Authentication] -> Error reading localStorage key ${key}: ${e}`);
                    }
                }
            }

            // Try session storage as well
            const sessionKeys = Object.keys(sessionStorage);
            for (let i = 0; i < sessionKeys.length; i++) {
                const key = sessionKeys[i];
                if (key.includes('auth') || key.includes('token') || key.includes('oidc')) {
                    console.log(`[Authentication] -> Found potential token in sessionStorage: ${key}`);
                    try {
                        const value = sessionStorage.getItem(key);
                        // If it looks like a JWT (contains periods and is base64-ish)
                        if (value && value.includes('.') && value.length > 50) {
                            console.log(`[Authentication] -> This appears to be a JWT token`);
                            return value;
                        }
                    } catch (e) {
                        console.log(`[Authentication] -> Error reading sessionStorage key ${key}: ${e}`);
                    }
                }
            }

            return null;
        } catch (e) {
            console.log(`[Authentication] -> Error extracting tokens: ${e}`);
            return null;
        }
    }
};