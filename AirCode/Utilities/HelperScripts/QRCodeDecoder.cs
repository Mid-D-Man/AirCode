
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AirCode.Services.Cryptography;

namespace AirCode.Utilities.DataStructures
{
    /// <summary>
    /// Class for encoding and decoding secure QR codes with dual-purpose functionality:
    /// 1. Redirects external scanners to a website
    /// 2. Contains embedded session data only readable by the app
    /// </summary>
    public class QRCodeDecoder
    {
        private const string APP_SIGNATURE = "AIRCODE";
        private const string URL_PREFIX = "https://example.com/s/";
        
        // Use proper 256-bit AES key (32 bytes) and 128-bit IV (16 bytes)
        private const string ENCRYPTION_KEY = "AirCodeSecretKey1234567890123456"; // 32 characters for AES-256
        private const string INITIALIZATION_VECTOR = "AirCodeInitVectr"; // 16 characters for AES
        
        private readonly ICryptographyService _cryptographyService;

        public QRCodeDecoder(ICryptographyService cryptographyService)
        {
            _cryptographyService = cryptographyService;
        }

        /// <summary>
        /// Represents the decoded session data from a QR code.
        /// </summary>
        public class DecodedSessionData
        {
            public string SessionId { get; set; }
            public string CourseCode { get; set; }
            public DateTime StartTime { get; set; }
            public int Duration { get; set; }
            public DateTime GeneratedTime { get; set; }
            public DateTime ExpirationTime { get; set; }
            public string Nonce { get; set; } // To prevent replay attacks
            public string LectureId { get; set; } // Additional context for the session though .replace with initiator name (so people know who)
        }

        /// <summary>
        /// Asynchronously encodes session data into a QR code payload.
        /// Format: https://example.com/s/SESSION_ID#APP_SIGNATURE:ENCRYPTED_DATA:SIGNATURE
        /// </summary>
        public async Task<string> EncodeSessionDataAsync(
            string sessionId,
            string courseCode,
            DateTime startTime,
            int duration,
            string lectureId = null)
        {
            // Generate a unique nonce for replay attack prevention
            string nonce = Guid.NewGuid().ToString("N");

            // Create the session data object
            var sessionData = new DecodedSessionData
            {
                SessionId = sessionId,
                CourseCode = courseCode,
                StartTime = startTime,
                Duration = duration,
                GeneratedTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(duration),
                Nonce = nonce,
                LectureId = lectureId
            };

            // Serialize to JSON
            string jsonData = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Compress the JSON to reduce QR code complexity
            string compressedData = CompressString(jsonData);

            // Convert hardcoded keys to Base64 for the cryptography service
            string base64Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(ENCRYPTION_KEY));
            string base64IV = Convert.ToBase64String(Encoding.UTF8.GetBytes(INITIALIZATION_VECTOR));

            // Encrypt the compressed data
            string encryptedData = await _cryptographyService.EncryptData(
                compressedData, base64Key, base64IV);

            // Generate HMAC signature for integrity verification
            string signature = await _cryptographyService.SignData(
                encryptedData, ENCRYPTION_KEY);

            // Construct the final QR code payload
            return $"{URL_PREFIX}{sessionId}#{APP_SIGNATURE}:{encryptedData}:{signature}";
        }

        /// <summary>
        /// Asynchronously decodes a QR code payload to extract the session data.
        /// Returns null if the data is invalid, tampered with, or expired.
        /// </summary>
        public async Task<DecodedSessionData> DecodeSessionDataAsync(string qrCodeContent)
        {
            try
            {
                // Verify the QR code contains our application signature
                int signatureIndex = qrCodeContent.IndexOf($"#{APP_SIGNATURE}:");
                if (signatureIndex == -1)
                    return null;

                // Extract data components
                string payload = qrCodeContent.Substring(signatureIndex + APP_SIGNATURE.Length + 2);
                string[] components = payload.Split(':');
                if (components.Length != 2)
                    return null;

                string encryptedData = components[0];
                string providedSignature = components[1];

                // Verify the HMAC signature for integrity
                bool isValidSignature = await _cryptographyService.VerifyHmac(
                    encryptedData, providedSignature, ENCRYPTION_KEY);
                if (!isValidSignature)
                    return null;

                // Convert hardcoded keys to Base64 for the cryptography service
                string base64Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(ENCRYPTION_KEY));
                string base64IV = Convert.ToBase64String(Encoding.UTF8.GetBytes(INITIALIZATION_VECTOR));

                // Decrypt the data
                string compressedData = await _cryptographyService.DecryptData(
                    encryptedData, base64Key, base64IV);

                // Decompress the JSON data
                string jsonData = DecompressString(compressedData);

                // Deserialize the session data
                DecodedSessionData sessionData = JsonSerializer.Deserialize<DecodedSessionData>(
                    jsonData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                // Validate expiration
                if (DateTime.UtcNow > sessionData.ExpirationTime)
                    return null;

                // Additional validation: ensure the session hasn't been used beyond its time window
                if (sessionData.GeneratedTime > DateTime.UtcNow.AddMinutes(5)) // Future timestamp protection
                    return null;

                return sessionData;
            }
            catch (Exception ex)
            {
                // Log the exception in production
                Console.WriteLine($"QR Code decoding failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if a QR code payload is from our application and valid.
        /// </summary>
        public async Task<bool> IsValidAppQrCodeAsync(string qrCodeContent)
        {
            var decodedData = await DecodeSessionDataAsync(qrCodeContent);
            return decodedData != null;
        }

        /// <summary>
        /// Extracts the session ID from the QR code URL for quick identification.
        /// </summary>
        public static string ExtractSessionIdFromUrl(string qrCodeContent)
        {
            if (!qrCodeContent.StartsWith(URL_PREFIX))
                return null;

            int hashIndex = qrCodeContent.IndexOf('#');
            return hashIndex > 0
                ? qrCodeContent.Substring(URL_PREFIX.Length, hashIndex - URL_PREFIX.Length)
                : qrCodeContent.Substring(URL_PREFIX.Length);
        }

        /// <summary>
        /// Validates the time window of a QR code without full decryption.
        /// Useful for quick pre-validation checks.
        /// </summary>
        public async Task<bool> IsWithinValidTimeWindow(string qrCodeContent)
        {
            var sessionData = await DecodeSessionDataAsync(qrCodeContent);
            if (sessionData == null)
                return false;

            var now = DateTime.UtcNow;
            return now >= sessionData.StartTime && now <= sessionData.ExpirationTime;
        }

        #region Compression Helpers

        /// <summary>
        /// Compresses text using GZip and returns Base64-encoded result.
        /// </summary>
        private static string CompressString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            byte[] inputBytes = Encoding.UTF8.GetBytes(text);
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
                {
                    gzipStream.Write(inputBytes, 0, inputBytes.Length);
                }
                return Convert.ToBase64String(outputStream.ToArray());
            }
        }

        /// <summary>
        /// Decompresses Base64-encoded, GZip-compressed string.
        /// </summary>
        private static string DecompressString(string compressedBase64)
        {
            if (string.IsNullOrEmpty(compressedBase64))
                return string.Empty;

            byte[] compressedBytes = Convert.FromBase64String(compressedBase64);
            using (var inputStream = new MemoryStream(compressedBytes))
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                gzipStream.CopyTo(outputStream);
                return Encoding.UTF8.GetString(outputStream.ToArray());
            }
        }

        #endregion
    }
}