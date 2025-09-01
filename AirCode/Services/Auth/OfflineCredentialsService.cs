using AirCode.Services.Cryptography;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using AirCode.Domain.Entities;

namespace AirCode.Services.Auth;

public class OfflineCredentialsService : IOfflineCredentialsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ICryptographyService _cryptoService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<OfflineCredentialsService> _logger;

    // Test constants - DO NOT use in production! use config service or fetch from edge functions (dont forget)
    internal static string TEST_KEY = "+58K1jECYmF6GpPovgv3kmUMljv/EvY3G1NPwWqRCj8=";
    internal static string TEST_IV = "3emqU/f2fW6KG4rqanUG+Q==";

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

    public async Task<bool> StoreCredentialsAsync(string userId, string role, string key, string iv, int expirationDays = 14)
    {
        try
        {
            _logger.LogDebug("Storing offline credentials for user {UserId} with role {Role}", userId, role);
            
            // Wait for JS interop to be ready
            await EnsureJsReadyAsync();
            
            int expirationHours = expirationDays * 24;
            
            var result = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.storeCredentials",
                userId, role, key, iv, expirationHours);
                
            _logger.LogInformation("Credential storage result for {UserId}: {Result}", userId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store offline credentials for user {UserId}", userId);
            return false;
        }
    }

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
            _logger.LogDebug("Storing credentials with additional data for user {UserId}", userId);
            
            await EnsureJsReadyAsync();
            
            var additionalData = new Dictionary<string, object>();
            
            if (role.Equals("lectureradmin", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(lecturerId))
            {
                additionalData["lecturerId"] = lecturerId;
            }
            
            if ((role.Equals("student", StringComparison.OrdinalIgnoreCase) || 
                 role.Equals("courserepadmin", StringComparison.OrdinalIgnoreCase)) && 
                !string.IsNullOrEmpty(matricNumber))
            {
                additionalData["matricNumber"] = matricNumber;
            }
            
            int expirationHours = expirationDays * 24;
            
            var result = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.storeCredentials",
                userId, role, key, iv, expirationHours, additionalData);
                
            _logger.LogInformation("Enhanced credential storage for {UserId}: {Result}", userId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store enhanced credentials for user {UserId}", userId);
            return false;
        }
    }
    
    public async Task<bool> StoreCredentialsWithTestKeyAsync(string userId, string role, int expirationDays = 14)
    {
        try
        {
            _logger.LogDebug("Storing test credentials for user {UserId}", userId);
            await EnsureJsReadyAsync();
            
            return await StoreCredentialsAsync(userId, role, TEST_KEY, TEST_IV, expirationDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store test credentials for user {UserId}", userId);
            return false;
        }
    }
    
    public async Task<bool> CreateOfflineCredentialsFromCurrentUserAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (!user.Identity.IsAuthenticated)
            {
                _logger.LogWarning("User not authenticated for offline credential creation");
                return false;
            }

            string userId = GetUserIdFromClaims(user);
            string role = GetUserRoleFromClaims(user);
            string lecturerId = GetLecturerIdFromClaims(user);
            string matricNumber = GetMatricNumberFromClaims(user);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                _logger.LogWarning("Missing user claims for offline credentials");
                return false;
            }

            if (role.Equals("superioradmin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SuperiorAdmin role not supported for offline mode");
                return false;
            }

            _logger.LogInformation("Creating offline credentials for user {UserId}", userId);
            
            await EnsureJsReadyAsync();
            
            return await StoreCredentialsWithAdditionalDataAsync(
                userId, role, TEST_KEY, TEST_IV, 12, lecturerId, matricNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create offline credentials from current user");
            return false;
        }
    }

    public async Task<(bool Success, string Key, string IV)> GenerateAndStoreCredentialsAsync(
        string userId, string role, int expirationDays = 14)
    {
        try
        {
            _logger.LogDebug("Generating credentials for user {UserId}", userId);
            
            string key = await _cryptoService.GenerateAesKey(256);
            string iv = await _cryptoService.GenerateIv();
            
            bool success = await StoreCredentialsAsync(userId, role, key, iv, expirationDays);
            
            return (success, key, iv);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate credentials for user {UserId}", userId);
            return (false, null, null);
        }
    }

    public async Task<OfflineUserCredentials> GetCredentialsAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            bool areValid = await AreCredentialsValidAsync();
            if (!areValid) return null;

            var credentialsJson = await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getCredentials");

            if (string.IsNullOrEmpty(credentialsJson)) return null;

            return System.Text.Json.JsonSerializer.Deserialize<OfflineUserCredentials>(
                credentialsJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve offline credentials");
            await ClearCredentialsAsync();
            return null;
        }
    }

    public async Task<string> GetUserRoleAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            if (!await AreCredentialsValidAsync()) return null;

            return await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getUserRole");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user role");
            return null;
        }
    }

    public async Task<string> GetUserIdAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            if (!await AreCredentialsValidAsync()) return null;

            return await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getUserId");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user ID");
            return null;
        }
    }

    public async Task<string> GetLecturerIdAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            if (!await AreCredentialsValidAsync()) return null;

            return await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getLecturerId");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get lecturer ID");
            return null;
        }
    }

    public async Task<string> GetMatricNumberAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            if (!await AreCredentialsValidAsync()) return null;

            return await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getMatricNumber");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get matric number");
            return null;
        }
    }

    public async Task<string> GetDeviceGuidAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            return await _jsRuntime.InvokeAsync<string>(
                "offlineCredentialsHandler.getDeviceGuid");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device GUID");
            return null;
        }
    }

    public async Task<bool> ClearCredentialsAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            var result = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.clearCredentials");
                
            _logger.LogInformation("Credential clear result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear credentials");
            return false;
        }
    }

    public async Task<bool> AreCredentialsValidAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            bool isAuthenticated = await _jsRuntime.InvokeAsync<bool>(
                "offlineCredentialsHandler.isAuthenticated");
            
            return isAuthenticated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check credential validity");
            return false;
        }
    }

    public async Task<bool> CheckAndCleanExpiredCredentialsAsync()
    {
        try
        {
            bool areValid = await AreCredentialsValidAsync();
            return !areValid; // Returns true if expired and cleaned
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check expired credentials");
            return false;
        }
    }

    public async Task<object> GetCredentialStatusAsync()
    {
        try
        {
            await EnsureJsReadyAsync();
            
            return await _jsRuntime.InvokeAsync<object>(
                "offlineCredentialsHandler.getCredentialStatus");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get credential status");
            return null;
        }
    }

    public async Task<bool> StoreCredentialsWithDetailedLoggingAsync(
        string userId, 
        string role, 
        int expirationDays = 14,
        string lecturerId = null,
        string matricNumber = null)
    {
        try
        {
            _logger.LogInformation("=== Enhanced Credential Storage ===");
            _logger.LogInformation("User: {UserId}, Role: {Role}, Expiry: {Days} days", 
                userId, role, expirationDays);
            
            await EnsureJsReadyAsync();
            
            // Diagnostic check
            var diagnostics = await DiagnoseCredentialStorageAsync(userId, role);
            if (diagnostics.ContainsKey("error"))
            {
                _logger.LogError("Pre-flight validation failed: {Error}", diagnostics["error"]);
                return false;
            }
            
            var result = await StoreCredentialsWithAdditionalDataAsync(
                userId, role, TEST_KEY, TEST_IV, expirationDays, lecturerId, matricNumber);
            
            if (result)
            {
                _logger.LogInformation("✓ Enhanced credential storage successful");
                
                // Verify immediately
                var verificationResult = await AreCredentialsValidAsync();
                _logger.LogInformation("Storage verification: {Result}", verificationResult);
            }
            else
            {
                _logger.LogError("✗ Enhanced credential storage failed");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enhanced credential storage exception");
            return false;
        }
    }

    // Critical helper methods
    private async Task EnsureJsReadyAsync(int maxAttempts = 5, int delayMs = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var isReady = await _jsRuntime.InvokeAsync<bool>(
                    "eval", "typeof window.offlineCredentialsHandler !== 'undefined'");
                    
                if (isReady) return;
                
                await Task.Delay(delayMs);
            }
            catch
            {
                if (i == maxAttempts - 1) 
                    throw new InvalidOperationException("JavaScript handler not available");
                    
                await Task.Delay(delayMs);
            }
        }
        
        throw new InvalidOperationException("JavaScript handler initialization timeout");
    }

    private async Task<Dictionary<string, object>> DiagnoseCredentialStorageAsync(string userId, string role)
    {
        var diagnostics = new Dictionary<string, object>();
        
        try
        {
            _logger.LogInformation("=== Diagnosing Credential Storage ===");
            
            // Check JS handlers
            var jsHandlerAvailable = await _jsRuntime.InvokeAsync<bool>(
                "eval", "typeof window.offlineCredentialsHandler !== 'undefined'");
            diagnostics["js_handler_available"] = jsHandlerAvailable;
            
            var cryptoHandlerAvailable = await _jsRuntime.InvokeAsync<bool>(
                "eval", "typeof window.cryptographyHandler !== 'undefined'");
            diagnostics["crypto_handler_available"] = cryptoHandlerAvailable;
            
            // Check localStorage
            var localStorageAvailable = await _jsRuntime.InvokeAsync<bool>(
                "eval", "typeof Storage !== 'undefined'");
            diagnostics["local_storage_available"] = localStorageAvailable;
            
            // Check device GUID creation
            if (jsHandlerAvailable)
            {
                var deviceGuid = await _jsRuntime.InvokeAsync<string>(
                    "offlineCredentialsHandler.getDeviceGuid");
                diagnostics["device_guid_created"] = !string.IsNullOrEmpty(deviceGuid);
                diagnostics["device_guid"] = deviceGuid;
            }
            
            _logger.LogInformation("Diagnostics: {@Diagnostics}", diagnostics);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Diagnostic failed");
            diagnostics["exception"] = ex.Message;
        }
        
        return diagnostics;
    }

    // Claim extraction helpers
    private string GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ??
                         user.FindFirst("sub") ??
                         user.FindFirst("user_id");
        return userIdClaim?.Value ?? string.Empty;
    }

    private string GetUserRoleFromClaims(ClaimsPrincipal user)
    {
        var roleClaim = user.FindFirst(ClaimTypes.Role) ??
                       user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ??
                       user.FindFirst("https://air-code/roles") ??
                       user.FindFirst("roles");
        return roleClaim?.Value ?? "user";
    }

    private string GetLecturerIdFromClaims(ClaimsPrincipal user)
    {
        var lecturerIdClaim = user.FindFirst("lecturer_id") ??
                             user.FindFirst("https://air-code/lecturer_id");
        return lecturerIdClaim?.Value;
    }

    private string GetMatricNumberFromClaims(ClaimsPrincipal user)
    {
        var matricNumberClaim = user.FindFirst("matric_number") ??
                               user.FindFirst("https://air-code/matric_number");
        return matricNumberClaim?.Value;
    }
}
