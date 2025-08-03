using AirCode.Domain.Entities;
using AirCode.Models.Supabase;
using AttendanceRecord = AirCode.Models.Supabase.AttendanceRecord;
namespace AirCode.Services.Attendance;

/// <summary>
/// Admin-side offline sync service interface for managing offline attendance sessions
/// This service is used by administrators and has direct database access
/// For client-side operations, use IOfflineAttendanceClientService
/// </summary>
public interface IOfflineSyncService
{
    #region Core Admin Sync Methods
    
    /// <summary>
    /// Sync all pending offline sessions to Firebase (admin use)
    /// </summary>
    Task<bool> SyncPendingRecordsAsync();
    
    /// <summary>
    /// Process individual offline attendance record (redirects to client service)
    /// </summary>
    [Obsolete("Use IOfflineAttendanceClientService for client operations")]
    Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecordModel recordModel);
    
    /// <summary>
    /// Sync offline session data to Firebase (admin use)
    /// </summary>
    Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session);
    
    /// <summary>
    /// Schedule periodic background sync for admin operations
    /// </summary>
    Task SchedulePeriodicSync();
    
    #endregion

    #region Client-Side Methods (Not Implemented - Use Client Service)
    
    /// <summary>
    /// Not implemented - use IOfflineAttendanceClientService
    /// </summary>
    [Obsolete("Use IOfflineAttendanceClientService.ProcessQRCodeScanAsync")]
    Task<bool> StoreOfflineAttendanceAsync(string encryptedQRPayload, string deviceId = null);
    
    /// <summary>
    /// Not implemented - use IOfflineAttendanceClientService
    /// </summary>
    [Obsolete("Use IOfflineAttendanceClientService.GetSyncStatusAsync")]
    Task<bool> HasPendingOfflineRecordsAsync();
    
    /// <summary>
    /// Not implemented - use IOfflineAttendanceClientService
    /// </summary>
    [Obsolete("Use IOfflineAttendanceClientService.GetSyncStatusAsync")]
    Task<int> GetPendingOfflineRecordsCountAsync();
    
    /// <summary>
    /// Not implemented - use IOfflineAttendanceClientService
    /// </summary>
    [Obsolete("Use IOfflineAttendanceClientService.InitializeAsync")]
    Task<bool> StoreMatricNumberAsync(string matricNumber);
    
    /// <summary>
    /// Not implemented - use IOfflineAttendanceClientService
    /// </summary>
    [Obsolete("Use IOfflineAttendanceClientService.ClearOfflineRecordsAsync")]
    Task<bool> ClearAllOfflineRecordsAsync();
    
    #endregion

    #region Admin Direct Database Methods
    
    /// <summary>
    /// Create offline session record in Supabase when admin creates session
    /// </summary>
    /// <param name="sessionData">Session data to create offline record for</param>
    /// <returns>True if created successfully</returns>
    Task<bool> CreateOfflineSessionRecordAsync(SessionData sessionData);
    
    /// <summary>
    /// Sync offline records directly to main attendance session (admin database access)
    /// </summary>
    /// <param name="courseCode">Course code to sync</param>
    /// <param name="sessionId">Main session ID to sync to</param>
    /// <returns>Batch sync result</returns>
    Task<BatchSyncResult> SyncOfflineRecordsToMainSessionAsync(string courseCode, string sessionId);
    
    /// <summary>
    /// Directly insert attendance record into database (admin only)
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="attendanceRecord">Attendance record to insert</param>
    /// <returns>True if inserted successfully</returns>
    Task<bool> DirectInsertAttendanceRecordAsync(string sessionId, AttendanceRecord attendanceRecord);
    
    /// <summary>
    /// Get all offline sessions for a course (admin view)
    /// </summary>
    /// <param name="courseCode">Course code</param>
    /// <returns>List of offline sessions</returns>
    Task<List<OfflineSessionData>> GetOfflineSessionsByCourseAsync(string courseCode);
    
    /// <summary>
    /// Archive old offline sessions (admin maintenance)
    /// </summary>
    /// <param name="olderThanDays">Archive sessions older than specified days</param>
    /// <returns>Number of sessions archived</returns>
    Task<int> ArchiveOldOfflineSessionsAsync(int olderThanDays = 30);
    
    #endregion
}