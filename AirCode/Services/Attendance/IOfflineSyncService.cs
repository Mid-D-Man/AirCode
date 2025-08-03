using AirCode.Domain.Entities;

namespace AirCode.Services.Attendance;

/// <summary>
/// Admin-side offline sync service interface for managing offline attendance sessions and syncing
/// This service is used by administrators and has direct database access, bypassing edge function validation
/// </summary>
public interface IOfflineSyncService
{
    #region Core Sync Methods
    
    /// <summary>
    /// Sync all pending offline records to the server
    /// </summary>
    Task<bool> SyncPendingRecordsAsync();
    
    /// <summary>
    /// Process individual offline attendance record
    /// </summary>
    Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecordModel recordModel);
    
    /// <summary>
    /// Sync offline session data (admin use)
    /// </summary>
    Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session);
    
    /// <summary>
    /// Schedule periodic background sync
    /// </summary>
    Task SchedulePeriodicSync();
    
    #endregion

    #region Client-Side Offline Methods
    
    /// <summary>
    /// Store offline attendance record when client scans QR code without internet
    /// </summary>
    /// <param name="encryptedQRPayload">The encrypted QR code payload</param>
    /// <param name="deviceId">Optional device identifier, will generate if not provided</param>
    /// <returns>True if stored successfully</returns>
    Task<bool> StoreOfflineAttendanceAsync(string encryptedQRPayload, string deviceId = null);
    
    /// <summary>
    /// Check if user has pending offline attendance records
    /// </summary>
    /// <returns>True if there are pending records</returns>
    Task<bool> HasPendingOfflineRecordsAsync();
    
    /// <summary>
    /// Get count of pending offline records
    /// </summary>
    /// <returns>Number of pending offline records</returns>
    Task<int> GetPendingOfflineRecordsCountAsync();
    
    /// <summary>
    /// Store student's matric number for offline use
    /// </summary>
    /// <param name="matricNumber">Student's matric number</param>
    /// <returns>True if stored successfully</returns>
    Task<bool> StoreMatricNumberAsync(string matricNumber);
    
    /// <summary>
    /// Clear all offline records (use with caution)
    /// </summary>
    /// <returns>True if cleared successfully</returns>
    Task<bool> ClearAllOfflineRecordsAsync();
    
    #endregion

    #region Admin Direct Database Methods
    
    /// <summary>
    /// Create offline session record directly in database (admin bypasses edge function)
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