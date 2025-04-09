/// auth0Client.js
let auth0 = null;
const AUTH0_DOMAIN = 'dev-msf1vlcbx1a8wtg3.eu.auth0.com';
const AUTH0_CLIENT_ID = 'JGLYuTjBEYGcbl0TCqYbIQh1IlKH0sSD';
const AUTH0_TOKEN_KEY = 'auth0_token';
const AUTH0_USER_KEY = 'auth0_user';

window.auth0Client = {
    initialize: async function() {
        try {
            // Load Auth0 client script if not already loaded
            if (typeof createAuth0Client === 'undefined') {
                await loadScript('https://cdn.auth0.com/js/auth0-spa-js/2.0/auth0-spa-js.production.min.js');

                // Wait a moment to ensure script is fully loaded
                await new Promise(resolve => setTimeout(resolve, 100));
            }

            if (typeof createAuth0Client === 'undefined') {
                console.error('Auth0 client library not loaded properly');
                return;
            }

            // Create Auth0 client
            auth0 = await createAuth0Client({
                domain: AUTH0_DOMAIN,
                clientId: AUTH0_CLIENT_ID, // Note: clientId, not client_id
                authorizationParams: {
                    redirect_uri: window.location.origin,
                    audience: 'https://aircode-api/',
                    scope: 'openid profile email'
                },
                cacheLocation: 'localstorage'
            });

            console.log("Auth0 client initialized successfully");

            // Handle callback from Auth0 redirect
            if (window.location.search.includes('code=') && window.location.search.includes('state=')) {
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
        } catch (err) {
            console.error('Error initializing Auth0:', err);
        }
    },

    signUp: async function(email, password, username, metadata) {
        try {
            if (!auth0) {
                console.error('Auth0 client not initialized');
                return false;
            }

            // Parse metadata if it's a string
            const metadataObj = typeof metadata === 'string' ? JSON.parse(metadata) : metadata;

            // Using Auth0's Database API
            const response = await fetch(`https://${AUTH0_DOMAIN}/dbconnections/signup`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    client_id: AUTH0_CLIENT_ID,
                    email: email,
                    password: password,
                    connection: 'Username-Password-Authentication',
                    username: username,
                    user_metadata: metadataObj
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                console.error('Auth0 signup error:', errorData);
                return false;
            }

            console.log('Auth0 signup successful');

            // Auto login after signup
            return await this.login(username, password, false, '');
        } catch (err) {
            console.error('Signup error:', err);
            return false;
        }
    },

    login: async function(username, password, isAdmin, adminId) {
        try {
            if (!auth0) {
                console.error('Auth0 client not initialized');
                return false;
            }

            // Use resource owner password flow instead of redirect
            const tokenResponse = await fetch(`https://${AUTH0_DOMAIN}/oauth/token`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    grant_type: 'password',
                    username: username,
                    password: password,
                    client_id: AUTH0_CLIENT_ID,
                    audience: 'https://aircode-api/',
                    scope: 'openid profile email'
                })
            });

            if (!tokenResponse.ok) {
                console.error('Auth0 login failed');
                return false;
            }

            const tokenData = await tokenResponse.json();
            localStorage.setItem(AUTH0_TOKEN_KEY, tokenData.access_token);

            // Get user profile
            const userInfoResponse = await fetch(`https://${AUTH0_DOMAIN}/userinfo`, {
                headers: {
                    Authorization: `Bearer ${tokenData.access_token}`
                }
            });

            if (!userInfoResponse.ok) {
                console.error('Failed to get user info');
                return false;
            }

            const userInfo = await userInfoResponse.json();
            localStorage.setItem(AUTH0_USER_KEY, JSON.stringify(userInfo));

            return true;
        } catch (err) {
            console.error('Login error:', err);
            return false;
        }
    },

    logout: async function() {
        try {
            if (!auth0) {
                console.error('Auth0 client not initialized');
                return false;
            }

            await auth0.logout({
                logoutParams: {
                    returnTo: window.location.origin
                }
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
        script.onload = () => {
            console.log(`Script loaded: ${url}`);
            resolve();
        };
        script.onerror = (err) => {
            console.error(`Script load error: ${url}`, err);
            // Try alternative CDN if main one fails
            if (url.includes('cdn.auth0.com')) {
                console.log('Trying alternative CDN...');
                const altScript = document.createElement('script');
                altScript.src = 'https://cdnjs.cloudflare.com/ajax/libs/auth0-spa-js/2.0.0/auth0-spa-js.production.min.js';
                altScript.onload = resolve;
                altScript.onerror = reject;
                document.head.appendChild(altScript);
            } else {
                reject(err);
            }
        };
        document.head.appendChild(script);
    });
}