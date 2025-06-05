using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System.Text.Json;
using AirCode.Services.Auth.Offline;
using AirCode.Services.Cryptography;
using Microsoft.AspNetCore.Components;
namespace AirCode.Services.Auth
{
    public interface IAuthService
    {
        /// <summary>
        /// Logs an authentication message for debugging purposes
        /// </summary>
        void LogAuthenticationMessage(string message);
    
        /// <summary>
        /// Logs an authentication message asynchronously
        /// </summary>
        Task LogAuthenticationMessageAsync(string message);
    
        /// <summary>
        /// Retrieves the JWT token for the authenticated user
        /// </summary>
        Task<string> GetJwtTokenAsync();
    
        /// <summary>
        /// Processes a successful login, stores offline credentials, and navigates to the appropriate page
        /// </summary>
        Task ProcessSuccessfulLoginAsync();
    
        /// <summary>
        /// Gets the authenticated user's role from their claims
        /// </summary>
        Task<string> GetUserRoleAsync();
    
        /// <summary>
        /// Gets the authenticated user's ID from their claims
        /// </summary>
        Task<string> GetUserIdAsync();
    
        /// <summary>
        /// Checks if the user is authenticated
        /// </summary>
        Task<bool> IsAuthenticatedAsync();
    }

    

public class AuthService : IAuthService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ICryptographyService _cryptographyService;
    private readonly IOfflineCredentialsService _offlineCredentialsService;

    public AuthService(
        IJSRuntime jsRuntime,
        NavigationManager navigationManager,
        IAccessTokenProvider tokenProvider,
        AuthenticationStateProvider authStateProvider,
        ICryptographyService cryptographyService,
        IOfflineCredentialsService offlineCredentialsService)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
        _tokenProvider = tokenProvider;
        _authStateProvider = authStateProvider;
        _cryptographyService = cryptographyService;
        _offlineCredentialsService = offlineCredentialsService;
    }

    public void LogAuthenticationMessage(string message)
    {
        _jsRuntime.InvokeVoidAsync("console.log", $"[Auth] {message}");
    }

    public async Task LogAuthenticationMessageAsync(string message)
    {
        await _jsRuntime.InvokeVoidAsync("console.log", $"[Auth] {message}");
    }

    public async Task<string> GetJwtTokenAsync()
    {
        try
        {
            var tokenResult = await _tokenProvider.RequestAccessToken();
            if (tokenResult.TryGetToken(out var token))
            {
                return token.Value;
            }
            return "Token not found";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User.Identity.IsAuthenticated;
    }

    public async Task<string> GetUserRoleAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        
        if (!user.Identity.IsAuthenticated)
            return string.Empty;

        // Check for role claim in standard location
        var roleClaim = user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        if (roleClaim != null)
            return roleClaim.Value;
            
        // Check for Auth0 custom role
        roleClaim = user.FindFirst("https://air-code/roles");
        if (roleClaim != null)
            return roleClaim.Value;
            
        // Default role
        return "user";
    }

    public async Task<string> GetUserIdAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        
        if (!user.Identity.IsAuthenticated)
            return string.Empty;
            
        // Get user ID from sub claim
        var subClaim = user.FindFirst("sub");
        return subClaim?.Value ?? string.Empty;
    }

    public async Task ProcessSuccessfulLoginAsync()
    {
        try
        {
            // Request the token
            var result = await _tokenProvider.RequestAccessToken();
            await LogAuthenticationMessageAsync($"Access token status: {result.Status}");

            if (result.TryGetToken(out var token))
            {
                await LogAuthenticationMessageAsync($"JWT retrieved successfully. Length: {token.Value.Length} chars");
        
                // Store token for debugging
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_debug_token", token.Value);
                
                // Extract user information
                string userId = await GetUserIdAsync();
                string userRole = await GetUserRoleAsync();
                
                if (string.IsNullOrEmpty(userId))
                {
                    // Try to extract from token directly if claims aren't populated yet
                    (userId, userRole) = await ExtractUserInfoFromToken(token.Value);
                }
                
                await LogAuthenticationMessageAsync($"User ID: {userId}, Role: {userRole}");
                
                // Store offline credentials if we have a valid user ID
                if (!string.IsNullOrEmpty(userId))
                {
                    // Default to "user" role if none was found
                    userRole = string.IsNullOrEmpty(userRole) ? "user" : userRole;
                    
                    // Only store offline credentials for non-superior admin roles
                    if (userRole.ToLower() != "superioradmin")
                    {
                        var key = await _cryptographyService.GenerateAesKey();
                        var iv = await _cryptographyService.GenerateIv();
                        int expiration = 12; // hours
                        
                        await LogAuthenticationMessageAsync($"Storing offline credentials for user {userId} with role {userRole}");
                        var credentialsResult = await _offlineCredentialsService.StoreCredentialsAsync(
                            userId, userRole, key, iv, expiration);
                        
                        if (credentialsResult)
                        {
                            await LogAuthenticationMessageAsync("Offline credentials stored successfully");
                        }
                        else
                        {
                            await LogAuthenticationMessageAsync("Failed to store offline credentials");
                        }
                    }
                }

                // Navigate based on role
                await NavigateByRole(userRole);
            }
            else
            {
                await LogAuthenticationMessageAsync("Failed to retrieve JWT token");
                _navigationManager.NavigateTo("/auth-info", forceLoad: false);
            }
        }
        catch (Exception ex)
        {
            await LogAuthenticationMessageAsync($"Error in ProcessSuccessfulLoginAsync: {ex.Message}");
            _navigationManager.NavigateTo("/auth-info", forceLoad: false);
        }
    }
    
    private async Task<(string userId, string userRole)> ExtractUserInfoFromToken(string token)
    {
        string userId = string.Empty;
        string userRole = string.Empty;

        try
        {
            // Parse the JWT to extract user info
            var tokenParts = token.Split('.');
            if (tokenParts.Length > 1)
            {
                var payload = tokenParts[1];
                // Ensure proper padding for Base64Url decoding
                var paddedPayload = payload;
                switch (payload.Length % 4)
                {
                    case 2: paddedPayload += "=="; break;
                    case 3: paddedPayload += "="; break;
                }
                paddedPayload = paddedPayload.Replace('-', '+').Replace('_', '/');
                
                var decodedBytes = Convert.FromBase64String(paddedPayload);
                var jsonString = System.Text.Encoding.UTF8.GetString(decodedBytes);
                
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                
                // Extract user ID (sub claim)
                if (jsonElement.TryGetProperty("sub", out var subProperty))
                {
                    userId = subProperty.GetString();
                }
                
                // Try multiple possible role claim names
                var roleClaims = new[] {
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
                    "https://air-code/roles",
                    "roles"
                };
                
                foreach (var roleClaim in roleClaims)
                {
                    if (jsonElement.TryGetProperty(roleClaim, out var roleProperty))
                    {
                        if (roleProperty.ValueKind == JsonValueKind.Array)
                        {
                            userRole = roleProperty.EnumerateArray().FirstOrDefault().GetString();
                        }
                        else if (roleProperty.ValueKind == JsonValueKind.String)
                        {
                            userRole = roleProperty.GetString();
                        }
                        
                        if (!string.IsNullOrEmpty(userRole))
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await LogAuthenticationMessageAsync($"Error extracting info from token: {ex.Message}");
        }
        
        return (userId, userRole);
    }
    
    private async Task NavigateByRole(string userRole)
    {
        await LogAuthenticationMessageAsync($"Navigating based on role: {userRole}");
        
        switch (userRole?.ToLower())
        {
            case "superioradmin":
                await LogAuthenticationMessageAsync("Redirecting to Superior Admin Dashboard");
                _navigationManager.NavigateTo("/Admin/SuperiorDashboard", forceLoad: false);
                break;
            case "lectureradmin":
                await LogAuthenticationMessageAsync("Redirecting to Lecturer Admin Dashboard");
                _navigationManager.NavigateTo("/Admin/SuperiorDashboard", forceLoad: false);
                break;
            case "courseadmin":
                await LogAuthenticationMessageAsync("Redirecting to Course Admin Dashboard");
                _navigationManager.NavigateTo("/Admin/SuperiorDashboard", forceLoad: false);
                break;
            case "student":
                await LogAuthenticationMessageAsync("Redirecting to Student Dashboard");
                _navigationManager.NavigateTo("/Client/Dashboard", forceLoad: false);
                break;
            default:
                // If we can't determine the role, go to auth-info for debugging
                await LogAuthenticationMessageAsync($"Unknown role '{userRole}', redirecting to auth-info");
                _navigationManager.NavigateTo("/auth-info", forceLoad: false);
                break;
        }
    }
}
}