using AirCode.Models.EdgeFunction;
using AirCode.Models.QRCode;
using AirCode.Models.Supabase;

namespace AirCode.Services.SupaBase
{
    /// <summary>
    /// Enhanced interface for Supabase Edge Function service with offline support
    /// </summary>
    public interface ISupabaseEdgeFunctionService
    {
        #region Online Attendance Processing
        
        /// <summary>
        /// Process attendance with unencrypted payload structure (online mode)
        /// </summary>
        /// <param name="request">Edge function request with payload and attendance data</param>
        /// <returns>Attendance processing result</returns>
        Task<AttendanceProcessingResult> ProcessAttendanceWithPayloadAsync(EdgeFunctionRequest request);
        
        /// <summary>
        /// Validate QR code payload data with signature verification
        /// </summary>
        /// <param name="payloadData">QR code payload data</param>
        /// <param name="signature">HMAC signature for verification</param>
        /// <returns>Validation result</returns>
        Task<QRValidationResult> ValidateQRPayloadAsync(QRCodePayloadData payloadData, string signature);
        
        #endregion

        #region Offline Attendance Processing
        
        /// <summary>
        /// Process offline attendance record through offline edge function
        /// </summary>
        /// <param name="request">Edge function request with offline attendance data</param>
        /// <returns>Attendance processing result</returns>
        Task<AttendanceProcessingResult> ProcessOfflineAttendanceAsync(EdgeFunctionRequest request);
        
        /// <summary>
        /// Check if offline session exists for given session ID
        /// </summary>
        /// <param name="sessionId">Session ID to check</param>
        /// <returns>True if offline session exists</returns>
        Task<bool> CheckOfflineSessionExistsAsync(string sessionId);
        
        #endregion

        #region Utility Functions
        
        /// <summary>
        /// Get server time from Supabase edge function
        /// </summary>
        /// <param name="timeType">Type of time (utc, local, etc.)</param>
        /// <returns>Server time as string</returns>
        Task<string> GetServerTimeAsync(string timeType = "utc");
        
        /// <summary>
        /// Get random cat image URL (testing function)
        /// </summary>
        /// <returns>Cat image URL</returns>
        Task<string> GetRandomCatImageAsync();
        
        #endregion
        
        #region User Ops(admin only stuff)

        Task<DeleteUserResponse> DeleteUserAsync(DeleteUserRequest requestModel);
        
        #endregion

        #region Legacy Methods (Deprecated)
        
        /// <summary>
        /// Legacy method - deprecated, use ProcessAttendanceWithPayloadAsync instead
        /// </summary>
        [Obsolete("Use ProcessAttendanceWithPayloadAsync for new payload structure")]
        Task<AttendanceProcessingResult> ProcessAttendanceAsync(string qrCodeContent, AttendanceRecord attendanceData);
        
        /// <summary>
        /// Legacy QR validation - deprecated
        /// </summary>
        [Obsolete("Use ValidateQRPayloadAsync for new payload structure")]
        Task<QRValidationResult> ValidateQRCodeAsync(string qrCodeContent);
        
        #endregion
    }
}