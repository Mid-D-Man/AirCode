// auth0Client.js
let auth0 = null;
const AUTH0_TOKEN_KEY = 'auth0_token';
const AUTH0_USER_KEY = 'auth0_user';

window.auth0Client = {
    initialize: async function() {
        // Load Auth0 client script if not already loaded
        if (!window.auth0) {
            await loadScript('https://cdnjs.cloudflare.com/ajax/libs/auth0-spa-js/2.1.2/auth0-spa-js.production.min.js');
        }

        // Create Auth0 client
        auth0 = await createAuth0Client({
            domain: 'dev-msf1vlcbx1a8wtg3.eu.auth0.com', // e.g. 'dev-yourapp.us.auth0.com'
            client_id: 'JGLYuTjBEYGcbl0TCqYbIQh1IlKH0sSD',
            redirect_uri: window.location.origin,
            cacheLocation: 'localstorage',
            audience: 'https://aircode-api/', // Optional API audience
            scope: 'openid profile email'
        });

        // Handle callback from Auth0 redirect
        if (window.location.search.includes('code=')) {
            try {
                await auth0.handleRedirectCallback();
                // Remove query parameters
                window.history.replaceState({}, document.title, '/');

                // Get and store token
                const token = await auth0.getTokenSilently();
                localStorage.setItem(AUTH0_TOKEN_KEY, token);

                // Get and store user
                const user = await auth0.getUser();
                localStorage.setItem(AUTH0_USER_KEY, JSON.stringify(user));
            } catch (err) {
                console.error('Error handling redirect:', err);
            }
        }
    },

    signUp: async function(email, password, username, metadata) {
        try {
            // This is a simplified signup. In a real implementation,
            // you would likely use Auth0's Database Connections API
            // or handle signup on your backend with Auth0 Management API
            const response = await fetch(`https://${auth0.domain}/dbconnections/signup`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    client_id: auth0.client_id,
                    email: email,
                    password: password,
                    connection: 'Username-Password-Authentication',
                    username: username,
                    user_metadata: JSON.parse(metadata)
                })
            });

            if (response.ok) {
                // Auto login after signup
                return await this.login(username, password, false, '');
            }
            return false;
        } catch (err) {
            console.error('Signup error:', err);
            return false;
        }
    },

    login: async function(username, password, isAdmin, adminId) {
        try {
            await auth0.loginWithRedirect({
                username: username,
                password: password,
                // You can include additional parameters if needed
                appState: {
                    isAdmin: isAdmin,
                    adminId: adminId
                }
            });
            return true;
        } catch (err) {
            console.error('Login error:', err);
            return false;
        }
    },

    logout: async function() {
        try {
            await auth0.logout({
                returnTo: window.location.origin
            });

            // Clear local storage
            localStorage.removeItem(AUTH0_TOKEN_KEY);
            localStorage.removeItem(AUTH0_USER_KEY);
            return true;
        } catch (err) {
            console.error('Logout error:', err);
            return false;
        }
    },

    isAuthenticated: async function() {
        try {
            if (!auth0) return false;
            return await auth0.isAuthenticated();
        } catch (err) {
            console.error('Authentication check error:', err);
            return false;
        }
    },

    getUserProfile: async function() {
        try {
            if (!auth0) return null;
            const isAuth = await auth0.isAuthenticated();
            if (!isAuth) return null;

            const user = await auth0.getUser();
            return JSON.stringify(user);
        } catch (err) {
            console.error('Get user profile error:', err);
            return null;
        }
    }
};

// Helper function to load script
function loadScript(url) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = url;
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}