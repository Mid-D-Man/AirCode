// Models/EdgeFunction/EdgeFunctionModels.cs

using System.Text.Json;
using AirCode.Models.QRCode;
using AirCode.Models.Supabase;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Models.EdgeFunction
{
    
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
    public class AttendanceProcessingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
        public QRCodePayloadData SessionData { get; set; } = new();
        public AttendanceRecord ProcessedAttendance { get; set; } = new();

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public class QRValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public QRCodePayloadData SessionData { get; set; } = new();
        public DateTime? ExpirationTime { get; set; }
        public bool IsExpired { get; set; }
        
        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public class EdgeFunctionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public EdgeSessionData SessionData { get; set; } = new();
        public EdgeProcessedAttendance ProcessedAttendance { get; set; } = new();

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class EdgeSessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class EdgeProcessedAttendance
    {
        public string MatricNumber { get; set; } = string.Empty;
        public DateTime ScannedAt { get; set; }
        public bool IsOnlineScan { get; set; }
        
        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class ServerTimeResponse
    {
        public object Time { get; set; } = new();

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }

    public class EdgeFunctionErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool CanRetry { get; set; }
    }

    public class SessionDataResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsOfflineMode { get; set; }
    }

    public class ProcessedAttendanceResponse
    {
        public string MatricNumber { get; set; } = string.Empty;
        public DateTime ScannedAt { get; set; }
        public bool IsOfflineRecord { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
    }
    public class CatResponse
    {
        public string ImageUrl { get; set; } = string.Empty;

        public override string ToString() => 
            MID_HelperFunctions.GetStructOrClassMemberValues(this);
    }
    
    public class DeleteUserRequest
    {
        public string Auth0UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
    }

    public class DeleteUserResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

}