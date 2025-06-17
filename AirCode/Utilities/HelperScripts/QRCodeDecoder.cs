using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AirCode.Services.Cryptography;
using AirCode.Models.QRCode;
using AirCode.Services.SupaBase;

namespace AirCode.Utilities.HelperScripts
{
    /// <summary>
    /// Enhanced QR Code decoder with dual-purpose URL generation for external scanners and GitHub Pages integration
    /// </summary>
    public class QRCodeDecoder
    {
        private const string APP_SIGNATURE = "AIRCODE";
        private const string GITHUB_BASE_URL = "https://mid-d-man.github.io/AirCode/";
        private const string ENCRYPTION_KEY = "AirCodeSecretKey1234567890123456"; // 32 characters for AES-256
        private const string INITIALIZATION_VECTOR = "AirCodeInitVectr"; // 16 characters for AES
        
        private readonly ICryptographyService _cryptographyService;

        public QRCodeDecoder(ICryptographyService cryptographyService)
        {
            _cryptographyService = cryptographyService;
        }

        /// <summary>
        /// Encodes session data with temporal key provided from CreateAttendanceEvent
        /// </summary>
        public async Task<string> EncodeSessionDataAsync(
            string sessionId,
            string courseCode,
            DateTime startTime,
            int duration,
            bool allowOfflineConnectionAndSync,
            bool useTemporalKeyRefresh,
            AdvancedSecurityFeatures securityFeatures,
            string temporalKey)
        {
            var sessionData = new DecodedSessionData
            {
                SessionId = sessionId,
                CourseCode = courseCode,
                StartTime = startTime,
                Duration = duration,
                GeneratedTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(duration),
                TemporalKey = temporalKey,
                UseTemporalKeyRefresh = useTemporalKeyRefresh,
                AllowOfflineConnectionAndSync = allowOfflineConnectionAndSync,
                SecurityFeatures = securityFeatures
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

            // Create payload for URL fragment
            string payload = $"{APP_SIGNATURE}:{encryptedData}:{signature}";
            string encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

            // Return GitHub Pages URL with payload in fragment for external scanners
            return $"{GITHUB_BASE_URL}?session={sessionId}#{encodedPayload}";
        }

        /// <summary>
        /// Decodes QR code to extract full session data (for internal app use)
        /// Handles both legacy format and new GitHub Pages format
        /// </summary>
        public async Task<DecodedSessionData> DecodeSessionDataAsync(string qrCodeContent)
        {
            try
            {
                string payload = ExtractPayloadFromQRCode(qrCodeContent);
                if (string.IsNullOrEmpty(payload))
                    return null;

                // Decode the Base64 encoded payload
                string decodedPayload;
                try
                {
                    byte[] payloadBytes = Convert.FromBase64String(payload);
                    decodedPayload = Encoding.UTF8.GetString(payloadBytes);
                }
                catch
                {
                    // Fallback for legacy format
                    decodedPayload = payload;
                }

                if (!decodedPayload.StartsWith(APP_SIGNATURE + ":"))
                    return null;

                string[] components = decodedPayload.Substring(APP_SIGNATURE.Length + 1).Split(':');
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

                // Basic time validation only
                if (DateTime.UtcNow > sessionData.ExpirationTime)
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
        /// Extracts payload from QR code content, handling both GitHub Pages and legacy formats
        /// </summary>
        private string ExtractPayloadFromQRCode(string qrCodeContent)
        {
            // Handle GitHub Pages format: https://mid-d-man.github.io/AirCode/?session=123#encodedPayload
            if (qrCodeContent.StartsWith(GITHUB_BASE_URL))
            {
                int fragmentIndex = qrCodeContent.IndexOf('#');
                if (fragmentIndex > 0 && fragmentIndex < qrCodeContent.Length - 1)
                {
                    return qrCodeContent.Substring(fragmentIndex + 1);
                }
                return null;
            }

            // Handle legacy format with signature
            int signatureIndex = qrCodeContent.IndexOf($"#{APP_SIGNATURE}:");
            if (signatureIndex != -1)
            {
                return qrCodeContent.Substring(signatureIndex + 1); // Remove the '#'
            }

            return null;
        }

        /// <summary>
        /// Extracts partial payload data for Supabase Edge Function
        /// </summary>
        public async Task<QRCodePayloadData> ExtractPayloadDataAsync(string qrCodeContent)
        {
            var sessionData = await DecodeSessionDataAsync(qrCodeContent);
            if (sessionData == null)
                return null;

            var payloadData = new QRCodePayloadData
            {
                SessionId = sessionData.SessionId,
                CourseCode = sessionData.CourseCode,
                StartTime = sessionData.StartTime,
                EndTime = sessionData.ExpirationTime,
                TemporalKey = sessionData.TemporalKey,
                UseTemporalKeyRefresh = sessionData.UseTemporalKeyRefresh,
                AllowOfflineConnectionAndSync = sessionData.AllowOfflineConnectionAndSync,
                SecurityFeatures = sessionData.SecurityFeatures
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

            string payloadJson = JsonSerializer.Serialize(payloadData);
            string signature = await _cryptographyService.SignData(payloadJson, ENCRYPTION_KEY);

            return new EdgeFunctionRequest
            {
                QrCodePayload = payloadData,
                AttendanceData = attendanceData,
                PayloadSignature = signature
            };
        }

        #region Helper Methods

        /// <summary>
        /// Validates QR code format and authenticity
        /// </summary>
        public async Task<bool> IsValidAppQrCodeAsync(string qrCodeContent)
        {
            var decodedData = await DecodeSessionDataAsync(qrCodeContent);
            return decodedData != null;
        }

        /// <summary>
        /// Extracts session ID from GitHub Pages URL format
        /// </summary>
        public static string ExtractSessionIdFromUrl(string qrCodeContent)
        {
            if (qrCodeContent.StartsWith(GITHUB_BASE_URL))
            {
                var uri = new Uri(qrCodeContent);
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return queryParams["session"];
            }

            // Legacy format fallback
            if (!qrCodeContent.Contains("/"))
                return null;

            int hashIndex = qrCodeContent.IndexOf('#');
            int lastSlashIndex = qrCodeContent.LastIndexOf('/', hashIndex > 0 ? hashIndex : qrCodeContent.Length);
            
            if (lastSlashIndex < 0)
                return null;

            return hashIndex > 0
                ? qrCodeContent.Substring(lastSlashIndex + 1, hashIndex - lastSlashIndex - 1)
                : qrCodeContent.Substring(lastSlashIndex + 1);
        }

        /// <summary>
        /// Validates session time window
        /// </summary>
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
