using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System.Text.Json;
using AirCode.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components;

namespace AirCode.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigationManager;
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IOfflineCredentialsService _offlineCredentialsService;

        public AuthService(
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            IAccessTokenProvider tokenProvider,
            AuthenticationStateProvider authStateProvider,
            IOfflineCredentialsService offlineCredentialsService)
        {
            _jsRuntime = jsRuntime;
            _navigationManager = navigationManager;
            _tokenProvider = tokenProvider;
            _authStateProvider = authStateProvider;
            _offlineCredentialsService = offlineCredentialsService;
        }

        public void LogAuthenticationMessage(string message)
        {
            MID_HelperFunctions.DebugMessage($"[Auth] {message}", DebugClass.Log);
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
// Add this method to your existing AuthService.cs

        public async Task<string> GetUserPictureAsync()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
    
            if (!user.Identity.IsAuthenticated)
                return string.Empty;

            // Check for Auth0 picture claim
            var pictureClaim = user.FindFirst("picture");
            if (pictureClaim != null && !string.IsNullOrEmpty(pictureClaim.Value))
            {
                // Validate the URL to ensure it's accessible
                if (await IsImageUrlAccessibleAsync(pictureClaim.Value))
                {
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
                    return gravatarUrl;
                }
            }

            return string.Empty; // Will fall back to icon service
        }

        private async Task<bool> IsImageUrlAccessibleAsync(string imageUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3);
        
                var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, imageUrl));
                return response.IsSuccessStatusCode && 
                       response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") == true;
            }
            catch
            {
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
                
            // return default role if no role found
            return "default";
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

        public async Task<string> GetLecturerIdAsync()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            
            if (!user.Identity.IsAuthenticated)
                return string.Empty;

            // First check if user has lecturer admin role
            var userRole = await GetUserRoleAsync();
            if (userRole?.ToLower() != "lectureradmin")
                return string.Empty;

            // Check for lecturer ID in custom claim
            var lecturerIdClaim = user.FindFirst("https://air-code/lecturerId");
            if (lecturerIdClaim != null)
                return lecturerIdClaim.Value;

            // Check alternative claim names
            lecturerIdClaim = user.FindFirst("lecturerId");
            if (lecturerIdClaim != null)
                return lecturerIdClaim.Value;

            lecturerIdClaim = user.FindFirst("lecturer_id");
            if (lecturerIdClaim != null)
                return lecturerIdClaim.Value;

            return string.Empty;
        }

        public async Task<string> GetMatricNumberAsync()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            
            if (!user.Identity.IsAuthenticated)
                return string.Empty;

            // Check if user has student or course admin role
            var userRole = await GetUserRoleAsync();
            var allowedRoles = new[] { "student", "courserepadmin" };
            if (!allowedRoles.Contains(userRole?.ToLower()))
                return string.Empty;

            // Check for matric number in custom claims
            var matricClaim = user.FindFirst("https://air-code/matricNumber");
            if (matricClaim != null)
                return matricClaim.Value;

            // Check alternative claim names
            matricClaim = user.FindFirst("matricNumber");
            if (matricClaim != null)
                return matricClaim.Value;

            matricClaim = user.FindFirst("matric_number");
            if (matricClaim != null)
                return matricClaim.Value;

            matricClaim = user.FindFirst("studentId");
            if (matricClaim != null)
                return matricClaim.Value;

            return string.Empty;
        }

        public async Task<string> GetDeviceIdAsync()
        {
            try
            {
                // Try to get existing device ID from localStorage
                var existingDeviceId = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "AirCode_device_id");
                
                if (!string.IsNullOrEmpty(existingDeviceId))
                {
                    return existingDeviceId;
                }

                // Generate new device ID using simplified JavaScript fingerprinting
                var deviceId = await _jsRuntime.InvokeAsync<string>("eval", @"
                    (function() {
                        // Get or create persistent GUID
                        function getOrCreateDeviceGuid() {
                            let deviceGuid = localStorage.getItem('AirCode_device_guid');
                            
                            if (!deviceGuid) {
                                // Generate a new GUID
                                deviceGuid = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                                    const r = Math.random() * 16 | 0;
                                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                                    return v.toString(16);
                                });
                                
                                localStorage.setItem('AirCode_device_guid', deviceGuid);
                            }
                            
                            return deviceGuid;
                        }

                        // Create stable device fingerprint
                        const canvas = document.createElement('canvas');
                        const ctx = canvas.getContext('2d');
                        ctx.textBaseline = 'top';
                        ctx.font = '14px Arial';
                        ctx.fillText('AirCode Device', 2, 2);
                        
                        const deviceGuid = getOrCreateDeviceGuid();
                        
                        // Collect minimal, stable device info
                        const fingerprint = {
                            userAgent: navigator.userAgent || 'unknown',
                            language: navigator.language || 'en',
                            screenResolution: screen.width + 'x' + screen.height,
                            colorDepth: screen.colorDepth || 24,
                            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
                            canvas: canvas.toDataURL(),
                            hardwareConcurrency: navigator.hardwareConcurrency || 4,
                            deviceGuid: deviceGuid
                        };
                        
                        // Create deterministic hash from fingerprint
                        const fingerprintStr = JSON.stringify(fingerprint);
                        let hash = 0;
                        for (let i = 0; i < fingerprintStr.length; i++) {
                            const char = fingerprintStr.charCodeAt(i);
                            hash = ((hash << 5) - hash) + char;
                            hash = hash & hash; // Convert to 32-bit integer
                        }
                        
                        // Create stable device ID using GUID prefix for uniqueness
                        const guidPrefix = deviceGuid.substring(0, 8);
                        return 'device_' + guidPrefix + '_' + Math.abs(hash).toString(16);
                    })()
                ");

                // Store the generated device ID
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "AirCode_device_id", deviceId);
                
                return deviceId;
            }
            catch (Exception ex)
            {
                await LogAuthenticationMessageAsync($"Error generating device ID: {ex.Message}");
                
                // Fallback: create a simple GUID-based ID
                var fallbackGuid = Guid.NewGuid().ToString("N")[..16]; // First 16 chars of GUID
                var fallbackId = $"device_fallback_{fallbackGuid}";
                
                try
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "AirCode_device_id", fallbackId);
                }
                catch
                {
                    // If even localStorage fails, return a temporary ID
                }
                
                return fallbackId;
            }
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
            
                    // Store token for debugging (remove in production)
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_debug_token", token.Value);
                    
                    // Extract user information
                    string userId = await GetUserIdAsync();
                    string userRole = await GetUserRoleAsync();
                    string lecturerId = string.Empty;
                    string matricNumber = string.Empty;
                    string deviceId = await GetDeviceIdAsync();
                    
                    if (string.IsNullOrEmpty(userId))
                    {
                        // Try to extract from token directly if claims aren't populated yet
                        (userId, userRole, lecturerId, matricNumber) = await ExtractUserInfoFromToken(token.Value);
                    }
                    else 
                    {
                        if (userRole?.ToLower() == "lectureradmin")
                        {
                            lecturerId = await GetLecturerIdAsync();
                            if (string.IsNullOrEmpty(lecturerId))
                            {
                                (_, _, lecturerId, _) = await ExtractUserInfoFromToken(token.Value);
                            }
                        }
                        else if (new[] { "student", "courserepadmin" }.Contains(userRole?.ToLower()))
                        {
                            matricNumber = await GetMatricNumberAsync();
                            if (string.IsNullOrEmpty(matricNumber))
                            {
                                (_, _, _, matricNumber) = await ExtractUserInfoFromToken(token.Value);
                            }
                        }
                    }
                    
                    await LogAuthenticationMessageAsync($"User ID: {userId}, Role: {userRole}, Device ID: {deviceId}" + 
                        (string.IsNullOrEmpty(lecturerId) ? "" : $", Lecturer ID: {lecturerId}") +
                        (string.IsNullOrEmpty(matricNumber) ? "" : $", Matric Number: {matricNumber}"));
                    
                    // Create offline credentials if we have a valid user ID and role is not SuperiorAdmin
                    if (!string.IsNullOrEmpty(userId))
                    {
                        userRole = string.IsNullOrEmpty(userRole) ? "user" : userRole;
                        
                        if (userRole.ToLower() != "superioradmin")
                        {
                            await LogAuthenticationMessageAsync($"Creating offline credentials for user {userId} with role {userRole}");
                            
                            // Store additional user data in localStorage for tracking
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
                            
                            // Use the offline credentials service to create credentials
                            var credentialsResult = await _offlineCredentialsService.CreateOfflineCredentialsFromCurrentUserAsync();
                            
                            if (credentialsResult)
                            {
                                await LogAuthenticationMessageAsync("Offline credentials created successfully");
                            }
                            else
                            {
                                await LogAuthenticationMessageAsync("Failed to create offline credentials");
                            }
                        }
                    }
                }
                else
                {
                    await LogAuthenticationMessageAsync("Failed to retrieve JWT token");
                    _navigationManager.NavigateTo("UnknownError", forceLoad: false);
                }
            }
            catch (Exception ex)
            {
                await LogAuthenticationMessageAsync($"Error in ProcessSuccessfulLoginAsync: {ex.Message}");
                _navigationManager.NavigateTo("UnknownError", forceLoad: false);
            }
        }
        
        private async Task<(string userId, string userRole, string lecturerId, string matricNumber)> ExtractUserInfoFromToken(string token)
        {
            string userId = string.Empty;
            string userRole = string.Empty;
            string lecturerId = string.Empty;
            string matricNumber = string.Empty;

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
                                    break;
                                }
                            }
                        }
                    }

                    // Extract matric number if user is student or course admin
                    if (new[] { "student", "courseadmin" }.Contains(userRole?.ToLower()))
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
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogAuthenticationMessageAsync($"Error extracting info from token: {ex.Message}");
            }
            
            return (userId, userRole, lecturerId, matricNumber);
        }
    }
}