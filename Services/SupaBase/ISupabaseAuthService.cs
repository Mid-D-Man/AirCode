namespace AirCode.Services.SupaBase;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;

public interface ISupabaseAuthService
{
    /// <summary>
    /// Authenticate user with email and password
    /// </summary>
    Task<bool> LoginAsync(string email, string password);
        
    /// <summary>
    /// Register new user account
    /// </summary>
    Task<bool> RegisterAsync(string email, string password, Dictionary<string, object>? userData = null);
        
    /// <summary>
    /// Sign out current user
    /// </summary>
    Task LogoutAsync();
        
    /// <summary>
    /// Get current authenticated user
    /// </summary>
    Task<User?> GetCurrentUserAsync();
        
    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
        
    /// <summary>
    /// Reset user password
    /// </summary>
    Task<bool> ResetPasswordAsync(string email);
        
    /// <summary>
    /// Update user profile information
    /// </summary>
    Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates);
        
    /// <summary>
    /// Refresh current session
    /// </summary>
    Task<bool> RefreshSessionAsync();
}
