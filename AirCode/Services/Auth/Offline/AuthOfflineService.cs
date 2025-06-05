namespace AirCode.Services.Auth.Offline;

using AirCode.Services.Auth.Offline;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

/// <summary>
/// Service to handle offline authentication in the AirCode app
/// </summary>
public class AuthOfflineService : IAuthOfflineService
{
    private readonly IOfflineCredentialsService _offlineCredentialsService;
    private readonly AuthenticationStateProvider _authStateProvider;

    public AuthOfflineService(
        IOfflineCredentialsService offlineCredentialsService, 
        AuthenticationStateProvider authStateProvider)
    {
        _offlineCredentialsService = offlineCredentialsService;
        _authStateProvider = authStateProvider;
    }

    /// <summary>
    /// Authenticates a user using offline credentials
    /// </summary>
    /// <returns>True if authentication was successful</returns>
    public async Task<bool> AuthenticateOfflineAsync()
    {
        try
        {
            // Get offline credentials
            var credentials = await _offlineCredentialsService.GetCredentialsAsync();
            
            if (credentials == null)
            {
                Console.WriteLine("No valid offline credentials found");
                return false;
            }

            // Create claim identity based on offline credentials
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, credentials.UserId),
                new Claim(ClaimTypes.Role, credentials.Role),
                new Claim("offline_mode", "true")
            };

            // Cast to our custom auth state provider to notify of authentication state change
            if (_authStateProvider is CustomAuthStateProvider customProvider)
            {
                await customProvider.UpdateAuthenticationStateAsync(
                    new ClaimsPrincipal(new ClaimsIdentity(claims, "offline_auth")));
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to authenticate offline: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates offline credentials from the current authenticated user
    /// </summary>
    public async Task<bool> CreateOfflineCredentialsFromCurrentUserAsync()
    {
        try
        {
            // Get current authentication state
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (!user.Identity.IsAuthenticated)
            {
                Console.WriteLine("User is not authenticated. Cannot create offline credentials.");
                return false;
            }

            // Get user ID and role
            string userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string role = user.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                Console.WriteLine("Missing user ID or role in claims.");
                return false;
            }

            // Don't create offline credentials for SuperiorAdmin role
            if (role.Equals("superioradmin", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("SuperiorAdmin role does not support offline mode.");
                return false;
            }

            // Generate and store credentials
            var result = await _offlineCredentialsService.StoreCredentialsWithTestKeyAsync(userId, role);
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to create offline credentials: {ex.Message}");
            return false;
        }
    }
}