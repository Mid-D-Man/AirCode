using Microsoft.JSInterop;

namespace AirCode.Services.Cryptography;

public class CryptographyService : ICryptographyService
    {
        private readonly IJSRuntime _jsRuntime;

        public CryptographyService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Generates a random AES key with the specified bit length
        /// </summary>
        /// <param name="bitLength">Key size: 128, 192, or 256 bits</param>
        /// <returns>Base64-encoded AES key</returns>
        public async Task<string> GenerateAesKey(int bitLength = 256)
        {
            return await _jsRuntime.InvokeAsync<string>(
                "cryptographyHandler.generateAesKey", 
                bitLength);
        }

        /// <summary>
        /// Generates a random Initialization Vector for AES encryption
        /// </summary>
        /// <returns>Base64-encoded IV</returns>
        public async Task<string> GenerateIv()
        {
            return await _jsRuntime.InvokeAsync<string>(
                "cryptographyHandler.generateIv");
        }

        /// <summary>
        /// Encrypts data using AES-CBC
        /// </summary>
        /// <param name="data">Plain text data to encrypt</param>
        /// <param name="key">Base64-encoded AES key</param>
        /// <param name="iv">Base64-encoded initialization vector</param>
        /// <returns>Base64-encoded encrypted data</returns>
        public async Task<string> EncryptData(string data, string key, string iv)
        {
            return await _jsRuntime.InvokeAsync<string>(
                "cryptographyHandler.encryptData", 
                data, key, iv);
        }

        /// <summary>
        /// Decrypts AES-CBC encrypted data
        /// </summary>
        /// <param name="encryptedData">Base64-encoded encrypted data</param>
        /// <param name="key">Base64-encoded AES key</param>
        /// <param name="iv">Base64-encoded initialization vector</param>
        /// <returns>Decrypted plain text</returns>
        public async Task<string> DecryptData(string encryptedData, string key, string iv)
        {
            return await _jsRuntime.InvokeAsync<string>(
                "cryptographyHandler.decryptData", 
                encryptedData, key, iv);
        }

        /// <summary>
        /// Creates a hash of the provided data
        /// </summary>
        /// <param name="data">Data to hash</param>
        /// <param name="algorithm">Hash algorithm (default: SHA-256)</param>
        /// <returns>Base64-encoded hash</returns>
        public async Task<string> HashData(string data, string algorithm = "SHA-256")
        {
            return await _jsRuntime.InvokeAsync<string>(
                "cryptographyHandler.hashData", 
                data, algorithm);
        }

        /// <summary>
        /// Signs data using HMAC
        /// </summary>
        /// <param name="data">Data to sign</param>
        /// <param name="key">Key for HMAC</param>
        /// <param name="algorithm">Hash algorithm (default: SHA-256)</param>
        /// <returns>Hex-encoded signature (first 8 bytes)</returns>
        public async Task<string> SignData(string data, string key, string algorithm = "SHA-256")
        {
            return await _jsRuntime.InvokeAsync<string>(
                "cryptographyHandler.signData", 
                data, key, algorithm);
        }

        /// <summary>
        /// Verifies an HMAC signature
        /// </summary>
        /// <param name="data">Original data</param>
        /// <param name="signature">Signature to verify</param>
        /// <param name="key">Key for HMAC</param>
        /// <param name="algorithm">Hash algorithm (default: SHA-256)</param>
        /// <returns>True if signature is valid</returns>
        public async Task<bool> VerifyHmac(string data, string signature, string key, string algorithm = "SHA-256")
        {
            return await _jsRuntime.InvokeAsync<bool>(
                "cryptographyHandler.verifyHmac", 
                data, signature, key, algorithm);
        }

        /// <summary>
        /// Generates a code challenge for PKCE OAuth flow
        /// </summary>
        /// <param name="codeVerifier">Code verifier string</param>
        /// <returns>Code challenge</returns>
        public async Task<string> GenerateCodeChallenge(string codeVerifier)
        {
            return await _jsRuntime.InvokeAsync<string>(
                "generateCodeChallenge", 
                codeVerifier);
        }
    }