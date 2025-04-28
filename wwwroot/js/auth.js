// Authentication service for Blazor WASM with Auth0
window.AuthenticationService = {
    init: function () {
        console.log("AuthenticationService initialized");
        return true;
    },

    // Get user information
    getUser: function () {
        // Return user data in a format compatible with RemoteUserAccount
        const userData = localStorage.getItem('auth_user_info');
        console.log("AuthenticationService.getUser called");

        // If we have user data stored, return it
        if (userData) {
            try {
                return JSON.parse(userData);
            } catch (e) {
                console.error("Error parsing user data:", e);
                return null;
            }
        }

        return null;
    },

    // Sign in - redirects to Auth0 login
    signIn: function (state) {
        console.log("AuthenticationService.signIn called", state);
        // This should redirect to Auth0 login page - using the state parameter
        // For Auth0 with universal login, we'll build the URL and redirect

        // Get Auth0 settings from meta tags or config
        const domain = document.querySelector('meta[name="auth0:domain"]')?.content;
        const clientId = document.querySelector('meta[name="auth0:client-id"]')?.content;
        const audience = document.querySelector('meta[name="auth0:audience"]')?.content;
        const redirectUri = window.location.origin + "/authentication/login-callback";

        if (!domain || !clientId) {
            console.error("Auth0 configuration not found");
            return { status: "error", errorMessage: "Auth0 configuration not found" };
        }

        // Build Auth0 Universal Login URL
        const authUrl = new URL(`https://${domain}/authorize`);
        authUrl.searchParams.append('client_id', clientId);
        authUrl.searchParams.append('response_type', 'code');
        authUrl.searchParams.append('redirect_uri', redirectUri);
        authUrl.searchParams.append('scope', 'openid profile email');

        if (audience) {
            authUrl.searchParams.append('audience', audience);
        }

        // Add state parameter to maintain state across the redirect
        if (state) {
            localStorage.setItem('auth_state', JSON.stringify(state));
            authUrl.searchParams.append('state', btoa(JSON.stringify(state)));
        }

        // Redirect to Auth0 login
        console.log("Redirecting to Auth0:", authUrl.toString());
        window.location.href = authUrl.toString();

        // This function is expected to return a Promise, but since we're redirecting,
        // we'll never actually return - the browser will navigate away
        return new Promise((resolve) => {
            // This will never resolve due to redirect
        });
    },

    // Complete sign in - process the callback
    completeSignIn: function (url) {
        console.log("AuthenticationService.completeSignIn called", url);
        // Return the state from Auth0 callback
        const urlObj = new URL(url);
        const params = new URLSearchParams(urlObj.search);
        const error = params.get('error');
        const code = params.get('code');

        if (error) {
            return {
                status: "error",
                errorMessage: params.get('error_description') || "Login failed"
            };
        }

        if (code) {
            // In a real implementation, we would exchange the code for tokens
            // But since we're using Auth0's SDK, we'll let it handle that
            return {
                status: "success"
            };
        }

        return {
            status: "error",
            errorMessage: "No authorization code received"
        };
    },

    // Sign out
    signOut: function (state) {
        console.log("AuthenticationService.signOut called", state);
        // Get Auth0 settings
        const domain = document.querySelector('meta[name="auth0:domain"]')?.content;
        const clientId = document.querySelector('meta[name="auth0:client-id"]')?.content;
        const returnUrl = window.location.origin;

        if (!domain || !clientId) {
            console.error("Auth0 configuration not found");
            return { status: "error", errorMessage: "Auth0 configuration not found" };
        }

        // Clear local storage
        localStorage.removeItem('auth_user_info');
        localStorage.removeItem('auth_state');

        // Build Auth0 logout URL
        const logoutUrl = new URL(`https://${domain}/v2/logout`);
        logoutUrl.searchParams.append('client_id', clientId);
        logoutUrl.searchParams.append('returnTo', returnUrl);

        // Redirect to Auth0 logout
        console.log("Redirecting to Auth0 logout:", logoutUrl.toString());
        window.location.href = logoutUrl.toString();

        return new Promise((resolve) => {
            // This will never resolve due to redirect
        });
    },

    // Store user information after login
    setUser: function (userInfo) {
        if (userInfo) {
            localStorage.setItem('auth_user_info', JSON.stringify(userInfo));
            console.log("User info stored in localStorage");
        }
        return true;
    },

    // Clear user data on logout
    clearUser: function () {
        localStorage.removeItem('auth_user_info');
        console.log("User info cleared from localStorage");
        return true;
    }
};