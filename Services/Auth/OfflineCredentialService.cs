using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AirCode.Services.Storage;
using Microsoft.JSInterop;

namespace AirCode.Services.Auth
{
    public class OfflineCredentialService : IOfflineCredentialService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IBlazorAppLocalStorageService _blazorAppLocalStorage;
        
        private const string CREDENTIALS_KEY = "aircode_secure_credentials";
        private const string DEVICE_ID_KEY = "aircode_device_fingerprint";
        // Encryption parameters
        private static readonly byte[] AES_KEY = GenerateSecureKey("AirCodeEncryptionKey2025");
        private static readonly byte[] AES_IV = GenerateSecureKey("AirCodeInitVector2025").AsSpan(0, 16).ToArray();
        
        public OfflineCredentialService(IJSRuntime jsRuntime, IBlazorAppLocalStorageService blazorAppLocalStorage)
        {
            _jsRuntime = jsRuntime;
            _blazorAppLocalStorage = blazorAppLocalStorage;
        }

        public async Task<bool> StoreCredentials(string username, string password, bool isAdmin, string adminId)
        {
            try
            {
                // Try C# secure implementation first
                // Generate or retrieve device ID
                string deviceId = await GetOrCreateDeviceId();
                
                // Create credentials object
                var credentials = new OfflineCredentials
                {
                    Username = username,
                    PasswordHash = ComputePasswordHash(password, deviceId),
                    IsAdmin = isAdmin,
                    AdminId = adminId,
                    DeviceId = deviceId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                
                // Serialize, encrypt and store
                string json = JsonSerializer.Serialize(credentials);
                string encrypted = EncryptString(json);
                
                await _blazorAppLocalStorage.SetItemAsync(CREDENTIALS_KEY, encrypted);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in C# credential storage: {ex.Message}. Falling back to JS implementation.");
                
                // Fall back to JS implementation if C# fails
                try 
                {
                    return await _jsRuntime.InvokeAsync<bool>(
                        "credentialManager.storeCredentials", 
                        username, password, isAdmin, adminId);
                }
                catch (Exception jsEx)
                {
                    Console.WriteLine($"JS fallback also failed: {jsEx.Message}");
                    return false;
                }
            }
        }

        public async Task<OfflineCredentials> GetStoredCredentials()
        {
            try
            {
                // Try C# secure implementation first
                string encrypted = await _blazorAppLocalStorage.GetItemAsync<string>(CREDENTIALS_KEY);
                
                if (!string.IsNullOrEmpty(encrypted))
                {
                    string json = DecryptString(encrypted);
                    return JsonSerializer.Deserialize<OfflineCredentials>(json);
                }
                
                // If no C# credentials found, try JS fallback
                var credentialsJson = await _jsRuntime.InvokeAsync<string>(
                    "credentialManager.getStoredCredentials");
                
                if (string.IsNullOrEmpty(credentialsJson))
                    return null;
                
                return JsonSerializer.Deserialize<OfflineCredentials>(credentialsJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving credentials: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> HasStoredCredentials()
        {
            try
            {
                // Check C# storage first
                string encrypted = await _blazorAppLocalStorage.GetItemAsync<string>(CREDENTIALS_KEY);
                if (!string.IsNullOrEmpty(encrypted))
                    return true;
                
                // Fall back to JS check
                return await _jsRuntime.InvokeAsync<bool>("offlineManager.hasStoredCredentials");
            }
            catch
            {
                return false;
            }
        }

        public async Task ClearCredentials()
        {
            // Clear from C# secure storage
            await _blazorAppLocalStorage.RemoveItemAsync(CREDENTIALS_KEY);
            
            // Also clear from JS storage as fallback
            try
            {
                await _jsRuntime.InvokeVoidAsync("credentialManager.clearCredentials");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing JS credentials: {ex.Message}");
            }
        }

        public async Task<bool> ValidateOfflineLogin(string username, string password, bool isAdmin, string adminId)
        {
            try
            {
                // Try C# secure validation first
                var storedCredentials = await GetStoredCredentials();
                if (storedCredentials == null)
                    return false;
                    
                // Get current device ID 
                string deviceId = await GetOrCreateDeviceId();
                
                // Check if we have C# credentials (with device ID)
                if (!string.IsNullOrEmpty(storedCredentials.DeviceId))
                {
                    // Verify device ID matches
                    if (storedCredentials.DeviceId != deviceId)
                        return false;
                        
                    // Compute hash of provided password with stored device ID
                    string passwordHash = ComputePasswordHash(password, deviceId);
                    
                    // Compare credentials
                    return storedCredentials.Username.Trim().ToLower() == username.Trim().ToLower() &&
                           storedCredentials.PasswordHash == passwordHash &&
                           storedCredentials.IsAdmin == isAdmin &&
                           (storedCredentials.IsAdmin == false || storedCredentials.AdminId == adminId);
                }
                else
                {
                    // Fall back to JS validation logic for JS-stored credentials
                    var passwordHash = await _jsRuntime.InvokeAsync<string>(
                        "credentialManager.simpleHash", password);
                
                    // Compare credentials
                    return storedCredentials.Username.Trim().ToLower() == username.Trim().ToLower() &&
                           storedCredentials.PasswordHash == passwordHash &&
                           storedCredentials.IsAdmin == isAdmin &&
                           (storedCredentials.IsAdmin == false || storedCredentials.AdminId == adminId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating credentials: {ex.Message}");
                
                // Last resort - try pure JS validation
                try
                {
                    return await _jsRuntime.InvokeAsync<bool>(
                        "credentialManager.validateCredentials",
                        username, password, isAdmin, adminId);
                }
                catch
                {
                    return false;
                }
            }
        }
        
        // Helper methods from SecureCredentialService
        private static byte[] GenerateSecureKey(string seed)
        {
            using (var deriveBytes = new Rfc2898DeriveBytes(
                seed, 
                Encoding.UTF8.GetBytes("AirCodeSaltValue2025"), 
                10000, 
                HashAlgorithmName.SHA256))
            {
                return deriveBytes.GetBytes(32); // 256 bits
            }
        }
        
        private string EncryptString(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = AES_KEY;
                aes.IV = AES_IV;
                
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                    }
                    
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
        
        private string DecryptString(string cipherText)
        {
            byte[] buffer = Convert.FromBase64String(cipherText);
            
            using (Aes aes = Aes.Create())
            {
                aes.Key = AES_KEY;
                aes.IV = AES_IV;
                
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                
                using (MemoryStream ms = new MemoryStream(buffer))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
        
        private string ComputePasswordHash(string password, string deviceId)
        {
            // Mix password with device ID for extra security
            string combined = $"{password}:{deviceId}:AirCode2025";
            
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return Convert.ToBase64String(hashBytes);
            }
        }
        
        private async Task<string> GetOrCreateDeviceId()
        {
            string deviceId = await _blazorAppLocalStorage.GetItemAsync<string>(DEVICE_ID_KEY);
            
            if (string.IsNullOrEmpty(deviceId))
            {
                // Generate a unique device identifier
                deviceId = Guid.NewGuid().ToString();
                
                // Store the device ID
                await _blazorAppLocalStorage.SetItemAsync(DEVICE_ID_KEY, deviceId);
            }
            
            return deviceId;
        }
    }

    public class OfflineCredentials
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool IsAdmin { get; set; }
        public string AdminId { get; set; }
        public string DeviceId { get; set; } // New field from SecureCredentialService
        public long Timestamp { get; set; }
    }
}