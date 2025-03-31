using System.Text.Json;
using Microsoft.JSInterop;

namespace AirCode.Services.Auth;

public class OfflineCredentialService : IOfflineCredentialService
    {
        private readonly IJSRuntime _jsRuntime;

        public OfflineCredentialService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<bool> StoreCredentials(string username, string password, bool isAdmin, string adminId)
        {
            return await _jsRuntime.InvokeAsync<bool>(
                "credentialManager.storeCredentials", 
                username, password, isAdmin, adminId);
        }

        public async Task<OfflineCredentials> GetStoredCredentials()
        {
            var credentialsJson = await _jsRuntime.InvokeAsync<string>(
                "credentialManager.getStoredCredentials");
            
            if (string.IsNullOrEmpty(credentialsJson))
                return null;
            
            return JsonSerializer.Deserialize<OfflineCredentials>(credentialsJson);
        }

        public async Task<bool> HasStoredCredentials()
        {
            return await _jsRuntime.InvokeAsync<bool>("offlineManager.hasStoredCredentials");
        }

        public async Task ClearCredentials()
        {
            await _jsRuntime.InvokeVoidAsync("credentialManager.clearCredentials");
        }

        public async Task<bool> ValidateOfflineLogin(string username, string password, bool isAdmin, string adminId)
        {
            // Get stored credentials
            var storedCredentials = await GetStoredCredentials();
            if (storedCredentials == null)
                return false;
            
            // Hash the provided password using the same algorithm as in JS
            var passwordHash = await _jsRuntime.InvokeAsync<string>(
                "credentialManager.simpleHash", password);
            
            // Compare credentials
            return storedCredentials.Username.Trim().ToLower() == username.Trim().ToLower() &&
                   storedCredentials.PasswordHash == passwordHash &&
                   storedCredentials.IsAdmin == isAdmin &&
                   (storedCredentials.IsAdmin == false || storedCredentials.AdminId == adminId);
        }
    }

    public class OfflineCredentials
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool IsAdmin { get; set; }
        public string AdminId { get; set; }
        public string DeviceId { get; set; }
        public long Timestamp { get; set; }
    }