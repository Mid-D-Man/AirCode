using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AirCode.Domain.Enums;
using AirCode.Models.EdgeFunction;
using AirCode.Services.Cryptography;
using AirCode.Models.QRCode;
using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;

namespace AirCode.Utilities.HelperScripts
{
    /// <summary>
    /// My QR Code decoder with dual-purpose URL generation for external scanners and GitHub Pages integration
    /// Uses const keys for simplicity - both client and server use same keys
    /// </summary>
    public class QRCodeDecoder
    {
        private const string APP_SIGNATURE = "AIRCODE";
        private const string GITHUB_BASE_URL = "https://mid-d-man.github.io/AirCode/";
        
        // Const keys for QR code encryption/decryption - shared between client and server
        private const string QR_ENCRYPTION_KEY = "QWlyQ29kZUtleTEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI="; // Base64 encoded 32-byte key
        private const string QR_INITIALIZATION_VECTOR = "QWlyQ29kZUlWMTIzNDU2"; // Base64 encoded 16-byte IV
        
        private readonly ICryptographyService _cryptographyService;

        public QRCodeDecoder(ICryptographyService cryptographyService)
        {
            _cryptographyService = cryptographyService;
        }

        /// <summary>
        /// Gets the QR code encryption key
        /// </summary>
        private string GetEncryptionKey()
        {
            return QR_ENCRYPTION_KEY;
        }

        /// <summary>
        /// Gets the QR code initialization vector
        /// </summary>
        private string GetInitializationVector()
        {
            return QR_INITIALIZATION_VECTOR;
        }

        /// <summary>
        /// Gets the signing key (uses the encryption key)
        /// </summary>
        private string GetSigningKey()
        {
            try
            {
                var keyBytes = Convert.FromBase64String(QR_ENCRYPTION_KEY);
                return Encoding.UTF8.GetString(keyBytes);
            }
            catch
            {
                // If it's not Base64, use as-is
                return QR_ENCRYPTION_KEY;
            }
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
            try
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

                string jsonData = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                string compressedData = CompressString(jsonData);

                string encryptionKey = GetEncryptionKey();
                string initializationVector = GetInitializationVector();
                string encryptedData = await _cryptographyService.EncryptData(compressedData, encryptionKey, initializationVector);

                string signingKey = GetSigningKey();
                string signature = await _cryptographyService.SignData(encryptedData, signingKey);

                string payload = $"{APP_SIGNATURE}:{encryptedData}:{signature}";
                string encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

                string qrUrl = $"{GITHUB_BASE_URL}?session={sessionId}#{encodedPayload}";
                await MID_HelperFunctions.DebugMessageAsync($"QR Code encoded successfully for session: {sessionId}", DebugClass.Info);
                return qrUrl;
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Failed to encode session data: {ex.Message}", DebugClass.Exception);
                throw;
            }
        }

        /// <summary>
        /// Decodes QR code to extract full session data (for internal app use)
        /// Handles both legacy format and new GitHub Pages format
        /// </summary>
        public async Task<DecodedSessionData?> DecodeSessionDataAsync(string qrCodeContent)
        {
            try
            {
                string payload = ExtractPayloadFromQRCode(qrCodeContent);
                if (string.IsNullOrEmpty(payload))
                {
                    await MID_HelperFunctions.DebugMessageAsync("No payload found in QR code content", DebugClass.Warning);
                    return null;
                }

                string decodedPayload;
                try
                {
                    byte[] payloadBytes = Convert.FromBase64String(payload);
                    decodedPayload = Encoding.UTF8.GetString(payloadBytes);
                }
                catch
                {
                    decodedPayload = payload;
                }

                if (!decodedPayload.StartsWith(APP_SIGNATURE + ":"))
                {
                    await MID_HelperFunctions.DebugMessageAsync("QR code does not contain valid AirCode signature", DebugClass.Warning);
                    return null;
                }

                string[] components = decodedPayload.Substring(APP_SIGNATURE.Length + 1).Split(':');
                if (components.Length != 2)
                {
                    await MID_HelperFunctions.DebugMessageAsync("QR code payload format is invalid", DebugClass.Warning);
                    return null;
                }

                string encryptedData = components[0];
                string providedSignature = components[1];

                string signingKey = GetSigningKey();
                bool isValidSignature = await _cryptographyService.VerifyHmac(encryptedData, providedSignature, signingKey);

                if (!isValidSignature)
                {
                    await MID_HelperFunctions.DebugMessageAsync("QR code signature verification failed", DebugClass.Warning);
                    return null;
                }

                string encryptionKey = GetEncryptionKey();
                string initializationVector = GetInitializationVector();
                string compressedData = await _cryptographyService.DecryptData(encryptedData, encryptionKey, initializationVector);

                string jsonData = DecompressString(compressedData);
                DecodedSessionData? sessionData = JsonSerializer.Deserialize<DecodedSessionData>(jsonData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                if (sessionData == null)
                {
                    await MID_HelperFunctions.DebugMessageAsync("Failed to deserialize session data from QR code", DebugClass.Warning);
                    return null;
                }

                if (DateTime.UtcNow > sessionData.ExpirationTime)
                {
                    await MID_HelperFunctions.DebugMessageAsync($"QR code has expired for session: {sessionData.SessionId}", DebugClass.Info);
                    return null;
                }

                if (sessionData.GeneratedTime > DateTime.UtcNow.AddMinutes(5))
                {
                    await MID_HelperFunctions.DebugMessageAsync($"QR code appears to be from the future for session: {sessionData.SessionId}", DebugClass.Warning);
                    return null;
                }

                await MID_HelperFunctions.DebugMessageAsync($"QR Code decoded successfully for session: {sessionData.SessionId}", DebugClass.Info);
                return sessionData;
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.DebugMessageAsync($"QR Code decoding failed: {ex.Message}", DebugClass.Exception);
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
                return string.Empty;
            }

            // Handle legacy format with signature
            int signatureIndex = qrCodeContent.IndexOf($"#{APP_SIGNATURE}:");
            if (signatureIndex != -1)
            {
                return qrCodeContent.Substring(signatureIndex + 1); // Remove the '#'
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts partial payload data for Supabase Edge Function
        /// </summary>
        public async Task<QRCodePayloadData?> ExtractPayloadDataAsync(string qrCodeContent)
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
        public async Task<EdgeFunctionRequest?> CreateEdgeFunctionRequestAsync(
            string qrCodeContent, 
            AttendanceRecord attendanceData)
        {
            var payloadData = await ExtractPayloadDataAsync(qrCodeContent);
            if (payloadData == null)
                return null;

            string payloadJson = JsonSerializer.Serialize(payloadData);
            string signingKey = GetSigningKey();
            string signature = await _cryptographyService.SignData(payloadJson, signingKey);

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
        public static string? ExtractSessionIdFromUrl(string qrCodeContent)
        {
            if (qrCodeContent.StartsWith(GITHUB_BASE_URL))
            {
                try
                {
                    var uri = new Uri(qrCodeContent);
                    var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    return queryParams["session"];
                }
                catch
                {
                    return null;
                }
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