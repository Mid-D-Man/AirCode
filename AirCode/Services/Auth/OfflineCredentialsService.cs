using AirCode.Services.Cryptography;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<OfflineCredentialsService> _logger;

    // Test constants - DO NOT use in production!
    private const string TEST_KEY = "VGhpcyBpcyBhIHRlc3Qga2V5IGZvciBBaXJDb2RlIHRlc3Rpbmc="; // 32 bytes when decoded
    private const string TEST_IV = "UmFuZG9tSVZmb3JUZXN0"; // 16 bytes when decoded

    public OfflineCredentialsService(
        IJSRuntime jsRuntime, 
        ICryptographyService cryptoService,
        AuthenticationStateProvider authStateProvider,
        ILogger<OfflineCredentialsService> logger)
    {
        _jsRuntime = jsRuntime;
        _cryptoService = cryptoService;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Stores offline credentials using provided encryption key and IV
    /// </summary>
    public async Task<bool> StoreCredentialsAsync(string userId, string role, string key, string iv, int expirationDays = 14)
    {
        try
        {
            _logger.LogDebug("Storing offline credentials for user {UserId} with role {Role}", userId, role);
            
            // Convert days to hours for JS module compatibility
            int expirationHours = expirationDays * 24;
            
            var result = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.storeCredentials",
                userId, role, key, iv, expirationHours);
                
            if (result)
            {
                _logger.LogInformation("Successfully stored offline credentials for user {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("Failed to store offline credentials for user {UserId}", userId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store offline credentials for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Stores offline credentials with additional role-specific data using provided encryption key and IV
    /// </summary>
    public async Task<bool> StoreCredentialsWithAdditionalDataAsync(
        string userId, 
        string role, 
        string key, 
        string iv, 
        int expirationDays = 14,
        string lecturerId = null,
        string matricNumber = null)
    {
        try
        {
            _logger.LogDebug("Storing offline credentials with additional data for user {UserId}", userId);
            
            // Build additional data object based on role
            var additionalData = new Dictionary<string, object>();
            
            if (role.Equals("lectureradmin", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(lecturerId))
            {
                additionalData["lecturerId"] = lecturerId;
                _logger.LogDebug("Added lecturer ID for user {UserId}", userId);
            }
            
            if ((role.Equals("student", StringComparison.OrdinalIgnoreCase) || 
                 role.Equals("courserepadmin", StringComparison.OrdinalIgnoreCase)) && 
                !string.IsNullOrEmpty(matricNumber))
            {
                additionalData["matricNumber"] = matricNumber;
                _logger.LogDebug("Added matric number for user {UserId}", userId);
            }
            
            int expirationHours = expirationDays * 24;
            
            var result = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.storeCredentials",
                userId, role, key, iv, expirationHours, additionalData);
                
            if (result)
            {
                _logger.LogInformation("Successfully stored offline credentials with additional data for user {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("Failed to store offline credentials with additional data for user {UserId}", userId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store offline credentials with additional data for user {UserId}", userId);
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
            _logger.LogDebug("Storing test credentials for user {UserId} with role {Role}", userId, role);
            _logger.LogDebug("Using TEST_KEY length: {KeyLength}, TEST_IV length: {IvLength}", 
                Convert.FromBase64String(TEST_KEY).Length, 
                Convert.FromBase64String(TEST_IV).Length);
        
            var result = await StoreCredentialsAsync(userId, role, TEST_KEY, TEST_IV, expirationDays);
        
            _logger.LogDebug("Test credential storage result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store test credentials for user {UserId}", userId);
            throw;
        }
    }
    
    /// <summary>
    /// Creates offline credentials from the current authenticated user using test keys
    /// </summary>
    public async Task<bool> CreateOfflineCredentialsFromCurrentUserAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (!user.Identity.IsAuthenticated)
            {
                _logger.LogWarning("User is not authenticated. Cannot create offline credentials");
                return false;
            }

            string userId = GetUserIdFromClaims(user);
            string role = GetUserRoleFromClaims(user);
            string lecturerId = GetLecturerIdFromClaims(user);
            string matricNumber = GetMatricNumberFromClaims(user);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                _logger.LogWarning("Missing user ID or role in claims for offline credential creation");
                return false;
            }

            if (role.Equals("superioradmin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SuperiorAdmin role does not support offline mode");
                return false;
            }

            _logger.LogInformation("Creating offline credentials for user {UserId} with role {Role}", userId, role);
            
            // Store credentials with additional data using test keys (for development)
            var result = await StoreCredentialsWithAdditionalDataAsync(
                userId, role, TEST_KEY, TEST_IV, 12, lecturerId, matricNumber);

            if (result)
            {
                _logger.LogInformation("Offline credentials created successfully for user {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("Failed to create offline credentials for user {UserId}", userId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create offline credentials from current user");
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
            _logger.LogDebug("Generating and storing credentials for user {UserId}", userId);
            
            string key = await _cryptoService.GenerateAesKey(256);
            string iv = await _cryptoService.GenerateIv();
            
            bool success = await StoreCredentialsAsync(userId, role, key, iv, expirationDays);
            
            if (success)
            {
                _logger.LogInformation("Successfully generated and stored credentials for user {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("Failed to generate and store credentials for user {UserId}", userId);
            }
            
            return (success, key, iv);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and store credentials for user {UserId}", userId);
            return (false, null, null);
        }
    }

    /// <summary>
    /// Retrieves user credentials if available and valid (automatically cleans expired credentials)
    /// </summary>
    public async Task<OfflineUserCredentials> GetCredentialsAsync()
    {
        try
        {
            bool areValid = await AreCredentialsValidAsync();
            if (!areValid)
            {
                _logger.LogDebug("No valid credentials available");
                return null;
            }

            var credentialsJson = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getCredentials");

            if (string.IsNullOrEmpty(credentialsJson))
            {
                _logger.LogDebug("No credentials returned from JavaScript handler");
                return null;
            }

            var credentials = System.Text.Json.JsonSerializer.Deserialize<OfflineUserCredentials>(
                credentialsJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            
            _logger.LogDebug("Successfully retrieved credentials for user {UserId}", credentials?.UserId);
            return credentials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve offline credentials");
            await ClearCredentialsAsync();
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
            if (!await AreCredentialsValidAsync())
            {
                _logger.LogDebug("Invalid credentials, cannot retrieve user role");
                return null;
            }

            var role = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getUserRole");
                
            _logger.LogDebug("Retrieved user role: {Role}", role);
            return role;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user role");
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
            if (!await AreCredentialsValidAsync())
            {
                _logger.LogDebug("Invalid credentials, cannot retrieve user ID");
                return null;
            }

            var userId = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getUserId");
                
            _logger.LogDebug("Retrieved user ID: {UserId}", userId);
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user ID");
            return null;
        }
    }

    /// <summary>
    /// Gets the lecturer ID from stored credentials (LecturerAdmin role only)
    /// </summary>
    public async Task<string> GetLecturerIdAsync()
    {
        try
        {
            if (!await AreCredentialsValidAsync())
            {
                _logger.LogDebug("Invalid credentials, cannot retrieve lecturer ID");
                return null;
            }

            var lecturerId = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getLecturerId");
                
            _logger.LogDebug("Retrieved lecturer ID: {LecturerId}", lecturerId);
            return lecturerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get lecturer ID");
            return null;
        }
    }

    /// <summary>
    /// Gets the matric number from stored credentials (Student/CourseRepAdmin roles only)
    /// </summary>
    public async Task<string> GetMatricNumberAsync()
    {
        try
        {
            if (!await AreCredentialsValidAsync())
            {
                _logger.LogDebug("Invalid credentials, cannot retrieve matric number");
                return null;
            }

            var matricNumber = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getMatricNumber");
                
            _logger.LogDebug("Retrieved matric number: {MatricNumber}", matricNumber);
            return matricNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get matric number");
            return null;
        }
    }

    /// <summary>
    /// Gets the device GUID from stored credentials
    /// </summary>
    public async Task<string> GetDeviceGuidAsync()
    {
        try
        {
            var deviceGuid = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getDeviceGuid");
                
            _logger.LogDebug("Retrieved device GUID: {DeviceGuid}", deviceGuid);
            return deviceGuid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device GUID");
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
            var result = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.clearCredentials");
                
            if (result)
            {
                _logger.LogInformation("Successfully cleared offline credentials");
            }
            else
            {
                _logger.LogWarning("Failed to clear offline credentials");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear credentials");
            return false;
        }
    }

    /// <summary>
    /// Checks if offline credentials exist and are still valid (not expired)
    /// </summary>
    public async Task<bool> AreCredentialsValidAsync()
    {
        try
        {
            bool isAuthenticated = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.isAuthenticated");
            
            _logger.LogDebug("Credential validity check result: {IsValid}", isAuthenticated);
            return isAuthenticated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check credential validity");
            return false;
        }
    }

    /// <summary>
    /// Checks and cleans up expired credentials
    /// Returns true if credentials were expired and cleaned up, false if they were valid or didn't exist
    /// </summary>
    public async Task<bool> CheckAndCleanExpiredCredentialsAsync()
    {
        try
        {
            // Use areCredentialsValid which handles cleanup automatically
            bool areValid = await AreCredentialsValidAsync();
            bool wereExpiredAndCleaned = !areValid;
            
            if (wereExpiredAndCleaned)
            {
                _logger.LogInformation("Expired credentials were cleaned up");
            }
            
            return wereExpiredAndCleaned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check and clean expired credentials");
            return false;
        }
    }

    /// <summary>
    /// Gets comprehensive credential status for debugging
    /// </summary>
    public async Task<object> GetCredentialStatusAsync()
    {
        try
        {
            var status = await _jsRuntime.InvokeAsync<object>(
                "offlineCredentialsHandler.getCredentialStatus");
                
            _logger.LogDebug("Retrieved credential status: {@Status}", status);
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get credential status");
            return null;
        }
    }

    /// <summary>
    /// Helper method to extract user ID from claims
    /// </summary>
    private string GetUserIdFromClaims(ClaimsPrincipal user)
    {
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
        var roleClaim = user.FindFirst(ClaimTypes.Role) ??
                       user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ??
                       user.FindFirst("https://air-code/roles") ??
                       user.FindFirst("roles");
        
        return roleClaim?.Value ?? "user";
    }

    /// <summary>
    /// Helper method to extract lecturer ID from claims
    /// </summary>
    private string GetLecturerIdFromClaims(ClaimsPrincipal user)
    {
        var lecturerIdClaim = user.FindFirst("lecturer_id") ??
                             user.FindFirst("https://air-code/lecturer_id");
        
        return lecturerIdClaim?.Value;
    }

    /// <summary>
    /// Helper method to extract matric number from claims
    /// </summary>
    private string GetMatricNumberFromClaims(ClaimsPrincipal user)
    {
        var matricNumberClaim = user.FindFirst("matric_number") ??
                               user.FindFirst("https://air-code/matric_number");
        
        return matricNumberClaim?.Value;
    }
}