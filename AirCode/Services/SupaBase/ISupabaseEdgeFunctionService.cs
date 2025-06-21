using AirCode.Models.QRCode;
using AirCode.Models.Supabase;

namespace AirCode.Services.SupaBase;

public interface ISupabaseEdgeFunctionService
{
    /// <summary>
    /// Legacy method - deprecated, use ProcessAttendanceWithPayloadAsync instead
    /// </summary>
    [Obsolete("Use ProcessAttendanceWithPayloadAsync for new payload structure")]
    Task<AttendanceProcessingResult> ProcessAttendanceAsync(string qrCodePayload, AttendanceRecord attendanceData);
    
    /// <summary>
    /// Process attendance with unencrypted payload structure
    /// </summary>
    Task<AttendanceProcessingResult> ProcessAttendanceWithPayloadAsync(EdgeFunctionRequest request);
    
    /// <summary>
    /// Validate QR code payload data with signature verification
    /// </summary>
    Task<QRValidationResult> ValidateQRPayloadAsync(QRCodePayloadData payloadData, string signature);
    
    /// <summary>
    /// Legacy QR validation - deprecated
    /// </summary>
    [Obsolete("Use ValidateQRPayloadAsync for new payload structure")]
    Task<QRValidationResult> ValidateQRCodeAsync(string qrCodePayload);
    
    /// <summary>
    /// Get server time from Supabase edge function
    /// </summary>
    Task<string> GetServerTimeAsync(string timeType = "utc");
    Task<string> GetRandomCatImageAsync();
}