using AirCode.Services.Cryptography;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace AirCode.Services.Auth;

/// <summary>
/// Implementation of offline credentials service that uses JS interop
/// </summary>
public class OfflineCredentialsService : IOfflineCredentialsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ICryptographyService _cryptoService;
    private readonly AuthenticationStateProvider _authStateProvider;

    // Test constants - DO NOT use in production!
    private const string TEST_KEY = "VGhpcyBpcyBhIHRlc3Qga2V5IGZvciBBaXJDb2RlIHRlc3Rpbmc="; // 32 bytes when decoded
    private const string TEST_IV = "UmFuZG9tSVZmb3JUZXN0"; // 16 bytes when decoded

    public OfflineCredentialsService(
        IJSRuntime jsRuntime, 
        ICryptographyService cryptoService,
        AuthenticationStateProvider authStateProvider)
    {
        _jsRuntime = jsRuntime;
        _cryptoService = cryptoService;
        _authStateProvider = authStateProvider;
    }

    /// <summary>
    /// Stores offline credentials using provided encryption key and IV
    /// </summary>
    public async Task<bool> StoreCredentialsAsync(string userId, string role, string key, string iv, int expirationDays = 14)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.storeCredentials",
                userId, role, key, iv, expirationDays);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to store offline credentials: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Stores offline credentials using test key and IV (for development only)
    /// </summary>
    public async Task<bool> StoreCredentialsWithTestKeyAsync(string userId, string role, int expirationDays = 14)
    {
        try
        {
            Console.WriteLine($"Storing test credentials for user {userId} with role {role}");
            return await StoreCredentialsAsync(userId, role, TEST_KEY, TEST_IV, expirationDays);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to store test credentials: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Creates offline credentials from the current authenticated user using test keys
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

            // Get user ID and role from claims
            string userId = GetUserIdFromClaims(user);
            string role = GetUserRoleFromClaims(user);

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

            // Store credentials using test keys (for development)
            Console.WriteLine($"Creating offline credentials for user {userId} with role {role}");
            var result = await StoreCredentialsWithTestKeyAsync(userId, role, 12); // 12 hours expiration

            if (result)
            {
                Console.WriteLine("Offline credentials created successfully");
            }
            else
            {
                Console.WriteLine("Failed to create offline credentials");
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to create offline credentials: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates and stores offline credentials with proper encryption
    /// </summary>
    public async Task<(bool Success, string Key, string IV)> GenerateAndStoreCredentialsAsync(
        string userId, string role, int expirationDays = 14)
    {
        try
        {
            // Generate new key and IV
            string key = await _cryptoService.GenerateAesKey(256);
            string iv = await _cryptoService.GenerateIv();
            
            // Store credentials
            bool success = await StoreCredentialsAsync(userId, role, key, iv, expirationDays);
            
            return (success, key, iv);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to generate and store credentials: {ex.Message}");
            return (false, null, null);
        }
    }

    /// <summary>
    /// Retrieves user credentials if available and valid
    /// </summary>
    public async Task<OfflineUserCredentials> GetCredentialsAsync()
    {
        try
        {
            var credentialsJson = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getCredentials");

            if (string.IsNullOrEmpty(credentialsJson))
            {
                return null;
            }

            // Parse JSON into credentials object
            var credentials = System.Text.Json.JsonSerializer.Deserialize<OfflineUserCredentials>(
                credentialsJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            
            return credentials;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to retrieve offline credentials: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the user's role from stored credentials
    /// </summary>
    public async Task<string> GetUserRoleAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getUserRole");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to get user role: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the user's ID from stored credentials
    /// </summary>
    public async Task<string> GetUserIdAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getUserId");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to get user ID: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears stored credentials
    /// </summary>
    public async Task<bool> ClearCredentialsAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.clearCredentials");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to clear credentials: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Helper method to extract user ID from claims
    /// </summary>
    private string GetUserIdFromClaims(ClaimsPrincipal user)
    {
        // Try different claim types for user ID
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ??
                         user.FindFirst("sub") ??
                         user.FindFirst("user_id");
        
        return userIdClaim?.Value ?? string.Empty;
    }

    /// <summary>
    /// Helper method to extract user role from claims
    /// </summary>
    private string GetUserRoleFromClaims(ClaimsPrincipal user)
    {
        // Try different claim types for role
        var roleClaim = user.FindFirst(ClaimTypes.Role) ??
                       user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ??
                       user.FindFirst("https://air-code/roles") ??
                       user.FindFirst("roles");
        
        return roleClaim?.Value ?? "user"; // Default to "user" role
    }
}