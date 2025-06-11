// Models/QRCodeModels.cs
using System.Text.Json;
using AirCode.Services.SupaBase;

namespace AirCode.Models.QRCode
{
    /// <summary>
    /// Partial QR code payload data sent to Supabase Edge Function
    /// This is NOT encrypted or compressed, only signed for integrity
    /// </summary>
    public class QRCodePayloadData
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TemporalKey { get; set; } = string.Empty; // Replaces nonce
        public bool AdvanceSecurityModeEnabled { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Complete session data used internally in the app (encrypted in QR codes)
    /// </summary>
    public class DecodedSessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
        public DateTime GeneratedTime { get; set; }
        public DateTime ExpirationTime { get; set; }
        public string TemporalKey { get; set; } = string.Empty; // Replaces nonce
        public bool AdvanceSecurityModeEnabled { get; set; }
        // Removed LectureId as requested

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Request payload for Supabase Edge Function
    /// Contains unencrypted QR payload data and attendance record
    /// </summary>
    public class EdgeFunctionRequest
    {
        public QRCodePayloadData QrCodePayload { get; set; }
        public AttendanceRecord AttendanceData { get; set; }
        public string PayloadSignature { get; set; } = string.Empty; // HMAC signature for integrity
    }
}