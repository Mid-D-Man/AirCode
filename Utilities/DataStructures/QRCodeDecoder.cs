using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace AirCode.Utilities.DataStructures
{
    /// <summary>
    /// Class for encoding and decoding secure QR codes with dual-purpose functionality:
    /// 1. Redirects external scanners to a website
    /// 2. Contains embedded session data only readable by the app
    /// </summary>
    public static class QRCodeDecoder
    {
        private const string APP_SIGNATURE = "AIRCODE";
        private const string URL_PREFIX = "https://example.com/s/";

        // Use string representations for keys so they can be passed to the JS crypto handler.
        private const string EncryptionKey = "AirCodeSecretKey1234567890123456"; // 32 characters for AES-256
        private const string IVString = "AirCodeInitVectr"; // 16 characters for AES

        /// <summary>
        /// Represents the decoded session data from a QR code.
        /// </summary>
        public class DecodedSessionData
        {
            public string SessionId { get; set; }
            public string CourseId { get; set; }
            public string CourseName { get; set; }
            public DateTime StartTime { get; set; }
            public int Duration { get; set; }
            public DateTime GeneratedTime { get; set; }
            public DateTime ExpirationTime { get; set; }
            public string Nonce { get; set; } // To prevent duplication
        }

        /// <summary>
        /// Asynchronously encodes session data into a QR code payload.
        /// The payload combines a redirect URL and an embedded, compressed, encrypted, and signed data string.
        /// Format: https://example.com/s/SESSION_ID#APP_SIGNATURE:ENCRYPTED_DATA:SIGNATURE
        /// </summary>
        public static async Task<string> EncodeSessionDataAsync(
            IJSRuntime js,
            string sessionId,
            string courseId,
            string courseName,
            DateTime startTime,
            int duration)
        {
            // Generate a unique nonce.
            string nonce = Guid.NewGuid().ToString("N");

            // Create the session data object.
            var sessionData = new DecodedSessionData
            {
                SessionId = sessionId,
                CourseId = courseId,
                CourseName = courseName,
                StartTime = startTime,
                Duration = duration,
                GeneratedTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(duration),
                Nonce = nonce
            };

            // Serialize the session data to JSON.
            string jsonData = JsonSerializer.Serialize(sessionData);

            // Compress the JSON to reduce the size.
            string compressedData = CompressString(jsonData);

            // Encrypt the compressed data using the JS cryptography handler.
            string encryptedData = await js.InvokeAsync<string>(
                "cryptographyHandler.encryptData", compressedData, EncryptionKey, IVString);

            // Compute a signature for the encrypted data using our JS crypto handler.
            string signature = await js.InvokeAsync<string>(
                "cryptographyHandler.signData", encryptedData, EncryptionKey);

            // Construct the final QR code payload.
            return $"{URL_PREFIX}{sessionId}#{APP_SIGNATURE}:{encryptedData}:{signature}";
        }

        /// <summary>
        /// Asynchronously decodes a QR code payload to extract the session data.
        /// Returns null if the data is invalid or the QR code has expired.
        /// </summary>
        public static async Task<DecodedSessionData> DecodeSessionDataAsync(
            IJSRuntime js,
            string qrCodeContent)
        {
            // Verify the QR code contains our application signature.
            int signatureIndex = qrCodeContent.IndexOf($"#{APP_SIGNATURE}:");
            if (signatureIndex == -1)
                return null;

            // Extract data components.
            string payload = qrCodeContent.Substring(signatureIndex + APP_SIGNATURE.Length + 2);
            string[] components = payload.Split(':');
            if (components.Length != 2)
                return null;

            string encryptedData = components[0];
            string providedSignature = components[1];

            // Verify the signature.
            string calculatedSignature = await js.InvokeAsync<string>(
                "cryptographyHandler.signData", encryptedData, EncryptionKey);
            if (calculatedSignature != providedSignature)
                return null;

            try
            {
                // Decrypt the data.
                string compressedData = await js.InvokeAsync<string>(
                    "cryptographyHandler.decryptData", encryptedData, EncryptionKey, IVString);

                // Decompress the JSON data.
                string jsonData = DecompressString(compressedData);

                // Deserialize the session data.
                DecodedSessionData sessionData = JsonSerializer.Deserialize<DecodedSessionData>(jsonData);

                // Check if the QR code has expired.
                if (DateTime.UtcNow > sessionData.ExpirationTime)
                    return null;

                return sessionData;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if a QR code payload is from our application and valid.
        /// </summary>
        public static async Task<bool> IsValidAppQrCodeAsync(IJSRuntime js, string qrCodeContent)
        {
            return await DecodeSessionDataAsync(js, qrCodeContent) != null;
        }

        /// <summary>
        /// Extracts the session ID from the QR code URL.
        /// </summary>
        public static string ExtractSessionIdFromUrl(string qrCodeContent)
        {
            if (qrCodeContent.StartsWith(URL_PREFIX))
            {
                int hashIndex = qrCodeContent.IndexOf('#');
                return hashIndex > 0
                    ? qrCodeContent.Substring(URL_PREFIX.Length, hashIndex - URL_PREFIX.Length)
                    : qrCodeContent.Substring(URL_PREFIX.Length);
            }

            return null;
        }

        #region Compression Helpers

        /// <summary>
        /// Compresses the given text using GZip and returns a Base64-encoded result.
        /// </summary>
        public static string CompressString(string text)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(text);
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gzipStream.Write(inputBytes, 0, inputBytes.Length);
                }

                // Get the compressed data and encode it as Base64.
                return Convert.ToBase64String(outputStream.ToArray());
            }
        }

        /// <summary>
        /// Decompresses a Base64-encoded, GZip-compressed string.
        /// </summary>
        public static string DecompressString(string compressedBase64)
        {
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