using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AirCode.Services.Cryptography;
using AirCode.Models.QRCode;
using AirCode.Services.SupaBase;

namespace AirCode.Utilities.HelperScripts
{
    /// <summary>
    /// Updated QR Code decoder with temporal key security and simplified payload structure
    /// </summary>
    public class QRCodeDecoder
    {
        private const string APP_SIGNATURE = "AIRCODE";
        private const string URL_PREFIX = "https://mid-d-man.github.io/";
        private const string ENCRYPTION_KEY = "AirCodeSecretKey1234567890123456"; // 32 characters for AES-256
        private const string INITIALIZATION_VECTOR = "AirCodeInitVectr"; // 16 characters for AES
        
        private readonly ICryptographyService _cryptographyService;

        public QRCodeDecoder(ICryptographyService cryptographyService)
        {
            _cryptographyService = cryptographyService;
        }

        /// <summary>
        /// Encodes session data into a QR code payload (still encrypted for full security)
        /// </summary>
        public async Task<string> EncodeSessionDataAsync(
            string sessionId,
            string courseCode,
            DateTime startTime,
            int duration,
            bool advanceSecurityModeEnabled = false)
        {
            // Generate temporal key - this acts as a time-based unique identifier
            // Think of it like a temporary password that expires with the session
            string temporalKey = GenerateTemporalKey(sessionId, startTime);

            var sessionData = new DecodedSessionData
            {
                SessionId = sessionId,
                CourseCode = courseCode,
                StartTime = startTime,
                Duration = duration,
                GeneratedTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(duration),
                TemporalKey = temporalKey,
                AdvanceSecurityModeEnabled = advanceSecurityModeEnabled
                // Removed LectureId and Nonce as requested
            };

            string jsonData = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            string compressedData = CompressString(jsonData);
            string base64Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(ENCRYPTION_KEY));
            string base64IV = Convert.ToBase64String(Encoding.UTF8.GetBytes(INITIALIZATION_VECTOR));

            string encryptedData = await _cryptographyService.EncryptData(
                compressedData, base64Key, base64IV);

            string signature = await _cryptographyService.SignData(
                encryptedData, ENCRYPTION_KEY);

            return $"{URL_PREFIX}{sessionId}#{APP_SIGNATURE}:{encryptedData}:{signature}";
        }

        /// <summary>
        /// Decodes QR code to extract full session data (for internal app use)
        /// </summary>
        public async Task<DecodedSessionData> DecodeSessionDataAsync(string qrCodeContent)
        {
            try
            {
                int signatureIndex = qrCodeContent.IndexOf($"#{APP_SIGNATURE}:");
                if (signatureIndex == -1)
                    return null;

                string payload = qrCodeContent.Substring(signatureIndex + APP_SIGNATURE.Length + 2);
                string[] components = payload.Split(':');
                if (components.Length != 2)
                    return null;

                string encryptedData = components[0];
                string providedSignature = components[1];

                bool isValidSignature = await _cryptographyService.VerifyHmac(
                    encryptedData, providedSignature, ENCRYPTION_KEY);
                if (!isValidSignature)
                    return null;

                string base64Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(ENCRYPTION_KEY));
                string base64IV = Convert.ToBase64String(Encoding.UTF8.GetBytes(INITIALIZATION_VECTOR));

                string compressedData = await _cryptographyService.DecryptData(
                    encryptedData, base64Key, base64IV);

                string jsonData = DecompressString(compressedData);

                DecodedSessionData sessionData = JsonSerializer.Deserialize<DecodedSessionData>(
                    jsonData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                // Validate expiration and temporal key
                if (DateTime.UtcNow > sessionData.ExpirationTime)
                    return null;

                if (!ValidateTemporalKey(sessionData.TemporalKey, sessionData.SessionId, sessionData.StartTime))
                    return null;

                if (sessionData.GeneratedTime > DateTime.UtcNow.AddMinutes(5))
                    return null;

                return sessionData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"QR Code decoding failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts partial payload data for Supabase Edge Function
        /// This creates the unencrypted payload with signature
        /// </summary>
        public async Task<QRCodePayloadData> ExtractPayloadDataAsync(string qrCodeContent)
        {
            // First decode the full session data
            var sessionData = await DecodeSessionDataAsync(qrCodeContent);
            if (sessionData == null)
                return null;

            // Create the partial payload (unencrypted)
            var payloadData = new QRCodePayloadData
            {
                SessionId = sessionData.SessionId,
                CourseCode = sessionData.CourseCode,
                StartTime = sessionData.StartTime,
                EndTime = sessionData.ExpirationTime, // Using ExpirationTime as EndTime
                TemporalKey = sessionData.TemporalKey,
                AdvanceSecurityModeEnabled = sessionData.AdvanceSecurityModeEnabled
            };

            return payloadData;
        }

        /// <summary>
        /// Creates signed request for Supabase Edge Function
        /// </summary>
        public async Task<EdgeFunctionRequest> CreateEdgeFunctionRequestAsync(
            string qrCodeContent, 
            AttendanceRecord attendanceData)
        {
            var payloadData = await ExtractPayloadDataAsync(qrCodeContent);
            if (payloadData == null)
                return null;

            // Sign the payload data for integrity (not encryption)
            string payloadJson = JsonSerializer.Serialize(payloadData);
            string signature = await _cryptographyService.SignData(payloadJson, ENCRYPTION_KEY);

            return new EdgeFunctionRequest
            {
                QrCodePayload = payloadData,
                AttendanceData = attendanceData,
                PayloadSignature = signature
            };
        }

        #region Temporal Key Methods

        /// <summary>
        /// Generates a temporal key based on session and time
        /// Think of this like a time-locked combination that only works during the session
        /// </summary>
        private string GenerateTemporalKey(string sessionId, DateTime startTime)
        {
            // Create a time-based key that incorporates session info
            var keyData = $"{sessionId}:{startTime:yyyyMMddHHmm}:TEMPORAL";
            var keyBytes = Encoding.UTF8.GetBytes(keyData);
            
            // Use SHA256 to create a consistent temporal key
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(keyBytes);
                return Convert.ToBase64String(hashBytes).Substring(0, 16); // 16 char key
            }
        }

        /// <summary>
        /// Validates that the temporal key matches the expected value
        /// This prevents replay attacks and ensures session authenticity
        /// </summary>
        private bool ValidateTemporalKey(string providedKey, string sessionId, DateTime startTime)
        {
            string expectedKey = GenerateTemporalKey(sessionId, startTime);
            return string.Equals(providedKey, expectedKey, StringComparison.Ordinal);
        }

        #endregion

        #region Helper Methods

        public async Task<bool> IsValidAppQrCodeAsync(string qrCodeContent)
        {
            var decodedData = await DecodeSessionDataAsync(qrCodeContent);
            return decodedData != null;
        }

        public static string ExtractSessionIdFromUrl(string qrCodeContent)
        {
            if (!qrCodeContent.StartsWith(URL_PREFIX))
                return null;

            int hashIndex = qrCodeContent.IndexOf('#');
            return hashIndex > 0
                ? qrCodeContent.Substring(URL_PREFIX.Length, hashIndex - URL_PREFIX.Length)
                : qrCodeContent.Substring(URL_PREFIX.Length);
        }

        public async Task<bool> IsWithinValidTimeWindow(string qrCodeContent)
        {
            var sessionData = await DecodeSessionDataAsync(qrCodeContent);
            if (sessionData == null)
                return false;

            var now = DateTime.UtcNow;
            return now >= sessionData.StartTime && now <= sessionData.ExpirationTime;
        }

        #endregion

        #region Compression Helpers

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