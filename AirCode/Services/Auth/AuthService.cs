using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System.Text.Json;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AirCode.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigationManager;
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IOfflineCredentialsService _offlineCredentialsService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            IAccessTokenProvider tokenProvider,
            AuthenticationStateProvider authStateProvider,
            IOfflineCredentialsService offlineCredentialsService,
            ILogger<AuthService> logger)
        {
            _jsRuntime = jsRuntime;
            _navigationManager = navigationManager;
            _tokenProvider = tokenProvider;
            _authStateProvider = authStateProvider;
            _offlineCredentialsService = offlineCredentialsService;
            _logger = logger;
        }

        public void LogAuthenticationMessage(string message)
        {
            _logger.LogInformation("[Auth] {Message}", message);
            MID_HelperFunctions.DebugMessage($"[Auth] {message}", DebugClass.Log);
        }

        public async Task LogAuthenticationMessageAsync(string message)
        {
            _logger.LogInformation("[Auth] {Message}", message);
            await _jsRuntime.InvokeVoidAsync("console.log", $"[Auth] {message}");
        }

        public async Task<string> GetJwtTokenAsync()
        {
            try
            {
                var tokenResult = await _tokenProvider.RequestAccessToken();
                if (tokenResult.TryGetToken(out var token))
                {
                    _logger.LogDebug("JWT token retrieved successfully");
                    return token.Value;
                }
                
                _logger.LogWarning("JWT token not found in token result");
                return "Token not found";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving JWT token");
                return $"Error: {ex.Message}";
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var isAuthenticated = authState.User.Identity.IsAuthenticated;
                
                _logger.LogDebug("Authentication status: {IsAuthenticated}", isAuthenticated);
                return isAuthenticated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authentication status");
                return false;
            }
        }

        public async Task<string> GetUserPictureAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
        
                if (!user.Identity.IsAuthenticated)
                {
                    _logger.LogDebug("User not authenticated, returning empty picture URL");
                    return string.Empty;
                }

                // Check for Auth0 picture claim
                var pictureClaim = user.FindFirst("picture");
                if (pictureClaim != null && !string.IsNullOrEmpty(pictureClaim.Value))
                {
                    if (await IsImageUrlAccessibleAsync(pictureClaim.Value))
                    {
                        _logger.LogDebug("Using Auth0 profile picture");
                        return pictureClaim.Value;
                    }
                }

                // Check for Gravatar using email
                var emailClaim = user.FindFirst("email") ?? user.FindFirst(ClaimTypes.Email);
                if (emailClaim != null && !string.IsNullOrEmpty(emailClaim.Value))
                {
                    var gravatarUrl = GenerateGravatarUrl(emailClaim.Value);
                    if (await IsImageUrlAccessibleAsync(gravatarUrl))
                    {
                        _logger.LogDebug("Using Gravatar profile picture");
                        return gravatarUrl;
                    }
                }

                _logger.LogDebug("No accessible profile picture found");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user picture");
                return string.Empty;
            }
        }

        private async Task<bool> IsImageUrlAccessibleAsync(string imageUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3);
        
                var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, imageUrl));
                var isAccessible = response.IsSuccessStatusCode && 
                                 response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") == true;
                
                _logger.LogDebug("Image URL {ImageUrl} accessibility: {IsAccessible}", imageUrl, isAccessible);
                return isAccessible;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Image URL {ImageUrl} not accessible", imageUrl);
                return false;
            }
        }

        private string GenerateGravatarUrl(string email, int size = 80)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLower()));
            var hashString = Convert.ToHexString(hash).ToLower();
            return $"https://www.gravatar.com/avatar/{hashString}?s={size}&d=404";
        }

        public async Task<string> GetUserRoleAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                
                if (!user.Identity.IsAuthenticated)
                {
                    _logger.LogDebug("User not authenticated, returning empty role");
                    return string.Empty;
                }

                // Check for role claim in standard location
                var roleClaim = user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
                if (roleClaim != null)
                {
                    _logger.LogDebug("Found role claim: {Role}", roleClaim.Value);
                    return roleClaim.Value;
                }
                    
                // Check for Auth0 custom role
                roleClaim = user.FindFirst("https://air-code/roles");
                if (roleClaim != null)
                {
                    _logger.LogDebug("Found Auth0 custom role: {Role}", roleClaim.Value);
                    return roleClaim.Value;
                }
                    
                _logger.LogDebug("No role claim found, returning default");
                return "default";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user role");
                return "default";
            }
        }

        public async Task<string> GetUserIdAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                
                if (!user.Identity.IsAuthenticated)
                {
                    _logger.LogDebug("User not authenticated, returning empty user ID");
                    return string.Empty;
                }
                    
                // Get user ID from sub claim
                var subClaim = user.FindFirst("sub");
                var userId = subClaim?.Value ?? string.Empty;
                
                _logger.LogDebug("Retrieved user ID: {UserId}", userId);
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user ID");
                return string.Empty;
            }
        }

        public async Task<string> GetLecturerIdAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                
                if (!user.Identity.IsAuthenticated)
                {
                    _logger.LogDebug("User not authenticated, returning empty lecturer ID");
                    return string.Empty;
                }

                // First check if user has lecturer admin role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "lectureradmin")
                {
                    _logger.LogDebug("User role {Role} is not lecturer admin", userRole);
                    return string.Empty;
                }

                // Check for lecturer ID in custom claims
                var lecturerIdClaims = new[] {
                    "https://air-code/lecturerId",
                    "lecturerId",
                    "lecturer_id"
                };

                foreach (var claimName in lecturerIdClaims)
                {
                    var lecturerIdClaim = user.FindFirst(claimName);
                    if (lecturerIdClaim != null && !string.IsNullOrEmpty(lecturerIdClaim.Value))
                    {
                        _logger.LogDebug("Found lecturer ID in claim {ClaimName}: {LecturerId}", claimName, lecturerIdClaim.Value);
                        return lecturerIdClaim.Value;
                    }
                }

                _logger.LogDebug("No lecturer ID found in claims");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving lecturer ID");
                return string.Empty;
            }
        }

        public async Task<string> GetMatricNumberAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                
                if (!user.Identity.IsAuthenticated)
                {
                    _logger.LogDebug("User not authenticated, returning empty matric number");
                    return string.Empty;
                }

                // Check if user has student or course admin role
                var userRole = await GetUserRoleAsync();
                var allowedRoles = new[] { "student", "courserepadmin" };
                if (!allowedRoles.Contains(userRole?.ToLower()))
                {
                    _logger.LogDebug("User role {Role} is not allowed for matric number", userRole);
                    return string.Empty;
                }

                // Check for matric number in custom claims
                var matricClaims = new[] {
                    "https://air-code/matricNumber",
                    "matricNumber",
                    "matric_number",
                    "studentId"
                };

                foreach (var claimName in matricClaims)
                {
                    var matricClaim = user.FindFirst(claimName);
                    if (matricClaim != null && !string.IsNullOrEmpty(matricClaim.Value))
                    {
                        _logger.LogDebug("Found matric number in claim {ClaimName}: {MatricNumber}", claimName, matricClaim.Value);
                        return matricClaim.Value;
                    }
                }

                _logger.LogDebug("No matric number found in claims");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving matric number");
                return string.Empty;
            }
        }

        public async Task<string> GetDeviceIdAsync()
        {
            try
            {
                // Use the offline credentials service to get device GUID
                var deviceGuid = await _offlineCredentialsService.GetDeviceGuidAsync();
                
                if (!string.IsNullOrEmpty(deviceGuid))
                {
                    _logger.LogDebug("Retrieved device GUID from offline credentials: {DeviceGuid}", deviceGuid);
                    return $"device_{deviceGuid.Substring(0, 8)}";
                }

                // Fallback to JavaScript-based device ID generation
                var deviceId = await _jsRuntime.InvokeAsync<string>("eval", @"
                    (function() {
                        const deviceGuid = window.offlineCredentialsHandler.getOrCreateDeviceGuid();
                        return 'device_' + deviceGuid.substring(0, 8);
                    })()
                ");

                _logger.LogDebug("Generated fallback device ID: {DeviceId}", deviceId);
                return deviceId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating device ID");
                
                // Final fallback: create a simple GUID-based ID
                var fallbackGuid = Guid.NewGuid().ToString("N")[..16];
                var fallbackId = $"device_fallback_{fallbackGuid}";
                
                _logger.LogWarning("Using fallback device ID: {FallbackId}", fallbackId);
                return fallbackId;
            }
        }

        public async Task ProcessSuccessfulLoginAsync()
        {
            try
            {
                _logger.LogInformation("Processing successful login");

                // Request the token
                var result = await _tokenProvider.RequestAccessToken();
                _logger.LogDebug("Access token status: {Status}", result.Status);

                if (!result.TryGetToken(out var token))
                {
                    _logger.LogError("Failed to retrieve JWT token");
                    _navigationManager.NavigateTo("UnknownError", forceLoad: false);
                    return;
                }

                _logger.LogInformation("JWT retrieved successfully. Length: {Length} chars", token.Value.Length);
        
                // Store token for debugging in development
                #if DEBUG
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_debug_token", token.Value);
                #endif
                
                // Extract user information from claims first
                var userId = await GetUserIdAsync();
                var userRole = await GetUserRoleAsync();
                var deviceId = await GetDeviceIdAsync();
                
                string lecturerId = string.Empty;
                string matricNumber = string.Empty;

                // If claims are empty, try extracting from token directly
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
                {
                    _logger.LogDebug("Claims not populated, extracting from token");
                    var tokenInfo = await ExtractUserInfoFromTokenAsync(token.Value);
                    userId = tokenInfo.userId;
                    userRole = tokenInfo.userRole;
                    lecturerId = tokenInfo.lecturerId;
                    matricNumber = tokenInfo.matricNumber;
                }
                else
                {
                    // Get role-specific IDs from claims
                    if (userRole?.ToLower() == "lectureradmin")
                    {
                        lecturerId = await GetLecturerIdAsync();
                        if (string.IsNullOrEmpty(lecturerId))
                        {
                            var tokenInfo = await ExtractUserInfoFromTokenAsync(token.Value);
                            lecturerId = tokenInfo.lecturerId;
                        }
                    }
                    else if (new[] { "student", "courserepadmin" }.Contains(userRole?.ToLower()))
                    {
                        matricNumber = await GetMatricNumberAsync();
                        if (string.IsNullOrEmpty(matricNumber))
                        {
                            var tokenInfo = await ExtractUserInfoFromTokenAsync(token.Value);
                            matricNumber = tokenInfo.matricNumber;
                        }
                    }
                }

                _logger.LogInformation("User login details - ID: {UserId}, Role: {Role}, Device: {DeviceId}{LecturerInfo}{MatricInfo}", 
                    userId, userRole, deviceId,
                    string.IsNullOrEmpty(lecturerId) ? "" : $", Lecturer ID: {lecturerId}",
                    string.IsNullOrEmpty(matricNumber) ? "" : $", Matric: {matricNumber}");

                // Validate required data
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("User ID is required but not found");
                    _navigationManager.NavigateTo("UnknownError", forceLoad: false);
                    return;
                }

                // Set default role if empty
                userRole = string.IsNullOrEmpty(userRole) ? "user" : userRole;

                // Only create offline credentials for non-SuperiorAdmin users
                if (userRole.ToLower() != "superioradmin")
                {
                    _logger.LogInformation("Creating offline credentials for user {UserId} with role {Role}", userId, userRole);
                    
                    // Store user session data
                    var userData = new
                    {
                        userId = userId,
                        role = userRole,
                        deviceId = deviceId,
                        lecturerId = lecturerId,
                        matricNumber = matricNumber,
                        loginTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                        "AirCode_user_session", 
                        JsonSerializer.Serialize(userData));
                    
                    // Create offline credentials using the service
                    var credentialsResult = await _offlineCredentialsService.CreateOfflineCredentialsFromCurrentUserAsync();
                    
                    if (credentialsResult)
                    {
                        _logger.LogInformation("Offline credentials created successfully for user {UserId}", userId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create offline credentials for user {UserId}", userId);
                    }
                }
                else
                {
                    _logger.LogInformation("Skipping offline credentials creation for SuperiorAdmin user {UserId}", userId);
                }

                _logger.LogInformation("Login processing completed successfully for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessSuccessfulLoginAsync");
                _navigationManager.NavigateTo("UnknownError", forceLoad: false);
            }
        }
        
        private async Task<(string userId, string userRole, string lecturerId, string matricNumber)> ExtractUserInfoFromTokenAsync(string token)
        {
            string userId = string.Empty;
            string userRole = string.Empty;
            string lecturerId = string.Empty;
            string matricNumber = string.Empty;

            try
            {
                _logger.LogDebug("Extracting user info from JWT token");

                var tokenParts = token.Split('.');
                if (tokenParts.Length <= 1)
                {
                    _logger.LogWarning("Invalid JWT token format");
                    return (userId, userRole, lecturerId, matricNumber);
                }

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
                    _logger.LogDebug("Extracted user ID from token: {UserId}", userId);
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
                            _logger.LogDebug("Extracted role from token: {Role}", userRole);
                            break;
                        }
                    }
                }

                // Extract lecturer ID if user is a lecturer admin
                if (userRole?.ToLower() == "lectureradmin")
                {
                    var lecturerIdClaims = new[] {
                        "https://air-code/lecturerId",
                        "lecturerId",
                        "lecturer_id"
                    };

                    foreach (var lecturerIdClaim in lecturerIdClaims)
                    {
                        if (jsonElement.TryGetProperty(lecturerIdClaim, out var lecturerIdProperty))
                        {
                            lecturerId = lecturerIdProperty.GetString();
                            if (!string.IsNullOrEmpty(lecturerId))
                            {
                                _logger.LogDebug("Extracted lecturer ID from token: {LecturerId}", lecturerId);
                                break;
                            }
                        }
                    }
                }

                // Extract matric number if user is student or course admin
                if (new[] { "student", "courserepadmin" }.Contains(userRole?.ToLower()))
                {
                    var matricClaims = new[] {
                        "https://air-code/matricNumber",
                        "matricNumber",
                        "matric_number",
                        "studentId"
                    };

                    foreach (var matricClaim in matricClaims)
                    {
                        if (jsonElement.TryGetProperty(matricClaim, out var matricProperty))
                        {
                            matricNumber = matricProperty.GetString();
                            if (!string.IsNullOrEmpty(matricNumber))
                            {
                                _logger.LogDebug("Extracted matric number from token: {MatricNumber}", matricNumber);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting info from JWT token");
            }
            
            return (userId, userRole, lecturerId, matricNumber);
        }
    }
}