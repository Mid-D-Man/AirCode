using System;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace AirCode.Utilities.DataStructures
{
    /// <summary>
    /// Class for decoding QR codes with special application-specific format
    /// </summary>
    public static class QRCodeDecoder
    {
        private const string APP_SIGNATURE = "AIRCODE";
        private const string URL_PREFIX = "https://example.com/s/";
        
        /// <summary>
        /// Decoded session data from a QR code
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
        }

        /// <summary>
        /// Encodes session data into a dual-purpose QR code content
        /// The resulting string will:
        /// 1. Redirect to website when scanned by regular QR scanners
        /// 2. Contain embedded session data that only our app can read
        /// </summary>
        public static string EncodeSessionData(string sessionId, string courseId, string courseName, 
            DateTime startTime, int duration)
        {
            // Create the session data object
            var sessionData = new DecodedSessionData
            {
                SessionId = sessionId,
                CourseId = courseId,
                CourseName = courseName,
                StartTime = startTime,
                Duration = duration,
                GeneratedTime = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(duration)
            };
            
            // Serialize the session data to JSON
            string jsonData = JsonSerializer.Serialize(sessionData);
            
            // Convert to Base64 for embedding
            string base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonData));
            
            // Add a simple checksum for additional security
            string checksum = ComputeChecksum(base64Data);
            
            // Generate the token that combines our URL with the embedded data
            // Format: https://example.com/s/SESSION_ID#APP_SIGNATURE:BASE64_DATA:CHECKSUM
            return $"{URL_PREFIX}{sessionId}#{APP_SIGNATURE}:{base64Data}:{checksum}";
        }
        
        /// <summary>
        /// Decodes QR code content to extract session data
        /// Returns null if the content doesn't match our application's format
        /// </summary>
        public static DecodedSessionData DecodeSessionData(string qrCodeContent)
        {
            // Verify this is our application's QR code
            int signatureIndex = qrCodeContent.IndexOf($"#{APP_SIGNATURE}:");
            if (signatureIndex == -1)
                return null; // Not our QR code
                
            // Extract the data components
            string[] components = qrCodeContent.Substring(signatureIndex + APP_SIGNATURE.Length + 2).Split(':');
            if (components.Length != 2)
                return null; // Invalid format
                
            string base64Data = components[0];
            string providedChecksum = components[1];
            
            // Verify checksum to prevent tampering
            string calculatedChecksum = ComputeChecksum(base64Data);
            if (calculatedChecksum != providedChecksum)
                return null; // Invalid checksum, data may have been tampered with
                
            try
            {
                // Decode the Base64 string to JSON
                string jsonData = Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
                
                // Deserialize to session data object
                DecodedSessionData sessionData = JsonSerializer.Deserialize<DecodedSessionData>(jsonData);
                
                // Validate expiration
                if (DateTime.UtcNow > sessionData.ExpirationTime)
                    return null; // QR code has expired
                    
                return sessionData;
            }
            catch (Exception)
            {
                // If any decoding error occurs, return null
                return null;
            }
        }
        
        /// <summary>
        /// Computes a simple checksum for the data to verify integrity
        /// </summary>
        private static string ComputeChecksum(string data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                // Use only first 8 bytes of hash for brevity in QR code
                return BitConverter.ToString(hashBytes, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }
        
        /// <summary>
        /// Checks if a QR code content is from our application and is valid
        /// </summary>
        public static bool IsValidAppQrCode(string qrCodeContent)
        {
            return DecodeSessionData(qrCodeContent) != null;
        }
        
        /// <summary>
        /// Extracts the session ID from a QR code URL (works even for external scanners)
        /// </summary>
        public static string ExtractSessionIdFromUrl(string qrCodeContent)
        {
            if (qrCodeContent.StartsWith(URL_PREFIX))
            {
                int hashIndex = qrCodeContent.IndexOf('#');
                string sessionIdPart = hashIndex > 0 
                    ? qrCodeContent.Substring(URL_PREFIX.Length, hashIndex - URL_PREFIX.Length)
                    : qrCodeContent.Substring(URL_PREFIX.Length);
                    
                return sessionIdPart;
            }
            
            return null;
        }
    }
}