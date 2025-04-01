using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AirCode.Services.Storage;

namespace AirCode.Services.Auth
{
    public class SecureCredentialService : IOfflineCredentialService
    {
        private readonly ILocalStorageService _localStorage;
        private const string CREDENTIALS_KEY = "aircode_secure_credentials";
        private const string DEVICE_ID_KEY = "aircode_device_fingerprint";
        
        // Encryption parameters
        private static readonly byte[] AES_KEY = GenerateSecureKey("AirCodeEncryptionKey2025");
        private static readonly byte[] AES_IV = GenerateSecureKey("AirCodeInitVector2025").AsSpan(0, 16).ToArray();
        
        public SecureCredentialService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task<bool> StoreCredentials(string username, string password, bool isAdmin, string adminId)
        {
            try
            {
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
                
                await _localStorage.SetItemAsync(CREDENTIALS_KEY, encrypted);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing credentials: {ex.Message}");
                return false;
            }
        }

        public async Task<OfflineCredentials> GetStoredCredentials()
        {
            try
            {
                string encrypted = await _localStorage.GetItemAsync<string>(CREDENTIALS_KEY);
                
                if (string.IsNullOrEmpty(encrypted))
                    return null;
                
                string json = DecryptString(encrypted);
                return JsonSerializer.Deserialize<OfflineCredentials>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving credentials: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> HasStoredCredentials()
        {
            string encrypted = await _localStorage.GetItemAsync<string>(CREDENTIALS_KEY);
            return !string.IsNullOrEmpty(encrypted);
        }

        public async Task ClearCredentials()
        {
            await _localStorage.RemoveItemAsync(CREDENTIALS_KEY);
        }

        public async Task<bool> ValidateOfflineLogin(string username, string password, bool isAdmin, string adminId)
        {
            var storedCredentials = await GetStoredCredentials();
            if (storedCredentials == null)
                return false;
                
            // Get current device ID
            string deviceId = await GetOrCreateDeviceId();
            
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
        
        // Helper methods
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
            string deviceId = await _localStorage.GetItemAsync<string>(DEVICE_ID_KEY);
            
            if (string.IsNullOrEmpty(deviceId))
            {
                // Generate a unique device identifier
                deviceId = Guid.NewGuid().ToString();
                
                // Store the device ID
                await _localStorage.SetItemAsync(DEVICE_ID_KEY, deviceId);
            }
            
            return deviceId;
        }
    }
}