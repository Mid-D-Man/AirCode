using AirCode.Services.Storage;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;

namespace AirCode.Services.SupaBase
{
    
    public class SupabaseAuthService : ISupabaseAuthService
    {
        private readonly Supabase.Client _client;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly ILogger<SupabaseAuthService> _logger;

        public SupabaseAuthService(
            Supabase.Client client,
            AuthenticationStateProvider authStateProvider,
            IBlazorAppLocalStorageService localStorage,
            ILogger<SupabaseAuthService> logger)
        {
            _logger = logger;
            _logger.LogInformation("------------------- AUTH SERVICE CONSTRUCTOR -------------------");
            _client = client;
            _authStateProvider = authStateProvider;
            _localStorage = localStorage;
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                _logger.LogInformation("METHOD: LoginAsync - Attempting login for: {Email}", email);
                
                var session = await _client.Auth.SignIn(email, password);
                
                if (session?.User != null)
                {
                    _logger.LogInformation("------------------- User logged in successfully -------------------");
                    _logger.LogInformation("User Email: {Email}", _client.Auth.CurrentUser?.Email);
                    _logger.LogInformation("User ID: {UserId}", _client.Auth.CurrentUser?.Id);
                    
                    // Store session data locally for offline access
                    await StoreSessionDataAsync(session);
                    
                    // Trigger authentication state update
                    await _authStateProvider.GetAuthenticationStateAsync();
                    
                    return true;
                }
                
                _logger.LogWarning("Login failed - No session or user returned");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for email: {Email}", email);
                return false;
            }
        }

        public async Task<bool> RegisterAsync(string email, string password, Dictionary<string, object>? userData = null)
        {
            try
            {
                _logger.LogInformation("METHOD: RegisterAsync - Attempting registration for: {Email}", email);
                
                var options = new SignUpOptions();
                if (userData != null)
                {
                    options.Data = userData;
                }
                
                var session = await _client.Auth.SignUp(email, password, options);
                
                if (session?.User != null)
                {
                    _logger.LogInformation("User registered successfully: {Email}", email);
                    
                    // Store session data locally
                    await StoreSessionDataAsync(session);
                    
                    // Trigger authentication state update
                    await _authStateProvider.GetAuthenticationStateAsync();
                    
                    return true;
                }
                
                _logger.LogWarning("Registration failed - No session or user returned");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for email: {Email}", email);
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                _logger.LogInformation("METHOD: LogoutAsync - Signing out user");
                
                await _client.Auth.SignOut();
                
                // Clear local session data
                await ClearSessionDataAsync();
                
                // Trigger authentication state update
                await _authStateProvider.GetAuthenticationStateAsync();
                
                _logger.LogInformation("User signed out successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                throw;
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                var user = _client.Auth.CurrentUser;
                if (user == null)
                {
                    // Try to restore from local storage if available
                    await RestoreSessionFromStorageAsync();
                    user = _client.Auth.CurrentUser;
                }
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user");
                return null;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                var session = _client.Auth.CurrentSession;
                
                // Check if user exists and session is not expired
                return user != null && session != null && session.ExpiresAt() > DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authentication status");
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            try
            {
                _logger.LogInformation("METHOD: ResetPasswordAsync for email: {Email}", email);
                
                await _client.Auth.ResetPasswordForEmail(email);
                
                _logger.LogInformation("Password reset email sent successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to: {Email}", email);
                return false;
            }
        }

        public async Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates)
        {
            try
            {
                _logger.LogInformation("METHOD: UpdateUserProfileAsync");
                
                var userAttributes = new UserAttributes();
                foreach (var update in updates)
                {
                    userAttributes.Data.Add(update.Key, update.Value);
                }
                
                var updatedUser = await _client.Auth.Update(userAttributes);
                
                if (updatedUser != null)
                {
                    _logger.LogInformation("User profile updated successfully");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return false;
            }
        }

        public async Task<bool> RefreshSessionAsync()
        {
            try
            {
                _logger.LogInformation("METHOD: RefreshSessionAsync");
                
                var session = await _client.Auth.RefreshSession();
                
                if (session != null)
                {
                    await StoreSessionDataAsync(session);
                    _logger.LogInformation("Session refreshed successfully");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing session");
                return false;
            }
        }

        private async Task StoreSessionDataAsync(Session session)
        {
            try
            {
                // Store essential session data for offline access and PWA functionality
                var sessionData = new
                {
                    AccessToken = session.AccessToken,
                    RefreshToken = session.RefreshToken,
                    ExpiresAt = session.ExpiresAt(),
                    UserId = session.User?.Id,
                    UserEmail = session.User?.Email,
                    UserRole = session.User?.UserMetadata?.ContainsKey("role") == true ? 
                              session.User.UserMetadata["role"]?.ToString() : null
                };
                
                await _localStorage.SetItemAsync("supabase_session", sessionData);
                _logger.LogDebug("Session data stored locally");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing session data locally");
            }
        }

        private async Task ClearSessionDataAsync()
        {
            try
            {
                await _localStorage.RemoveItemAsync("supabase_session");
                _logger.LogDebug("Local session data cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing local session data");
            }
        }

        private async Task RestoreSessionFromStorageAsync()
        {
            try
            {
                var sessionData = await _localStorage.GetItemAsync<dynamic>("supabase_session");
                if (sessionData != null && sessionData.ExpiresAt > DateTime.UtcNow)
                {
                    // Attempt to restore session using refresh token
                    await RefreshSessionAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring session from storage");
            }
        }
    }
}