using AirCode.Services.Attendance;

namespace AirCode.Domain.Entities;
// Client offline storage model
/// <summary>
/// main model for storing full offline attendance record with qrcode payload and nessesary data
/// </summary>
public class OfflineAttendanceRecordModel
{
    /// <summary>
    /// Unique identifier for this offline record
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Session ID extracted from QR code for duplicate checking
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Course code for additional validation
    /// </summary>
    public string CourseCode { get; set; } = string.Empty;

    /// <summary>
    /// Original encrypted QR code payload
    /// </summary>
    public string EncryptedQrPayload { get; set; } = string.Empty;

    /// <summary>
    /// Student's matriculation number
    /// </summary>
    public string MatricNumber { get; set; } = string.Empty;

    /// <summary>
    /// Device GUID for security tracking
    /// </summary>
    public string DeviceGuid { get; set; } = string.Empty;

    /// <summary>
    /// When the QR code was actually scanned
    /// </summary>
    public DateTime ScannedAt { get; set; }

    /// <summary>
    /// When this offline record was created/stored
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Current sync status of this record
    /// </summary>
    public SyncStatus Status { get; set; }

    /// <summary>
    /// Legacy sync status property for compatibility
    /// </summary>
    public SyncStatus SyncStatus { get; set; }

    /// <summary>
    /// Reason why this was stored offline (e.g., "No network", "Session not found")
    /// </summary>
    public string OfflineReason { get; set; } = string.Empty;

    /// <summary>
    /// Number of sync attempts for this record
    /// </summary>
    public int SyncAttempts { get; set; } = 0;

    /// <summary>
    /// Last error encountered during sync attempt
    /// </summary>
    public string LastSyncError { get; set; } = string.Empty;

    /// <summary>
    /// Last time sync was attempted
    /// </summary>
    public DateTime? LastSyncAttempt { get; set; }
}

/// <summary>
/// Sync status enumeration for offline records
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Record is waiting to be synced
    /// </summary>
    Pending,

    /// <summary>
    /// Record is currently being synced
    /// </summary>
    Syncing,

    /// <summary>
    /// Record was successfully synced and can be removed
    /// </summary>
    Synced,

    /// <summary>
    /// Record failed to sync after multiple attempts
    /// </summary>
    Failed,

    /// <summary>
    /// Record is invalid and should be removed
    /// </summary>
    Invalid
}

public class OfflineSessionData
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public SessionData SessionDetails { get; set; }
    public List<OfflineAttendanceRecordModel> PendingAttendanceRecords { get; set; } = new();
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
}

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public object Data { get; set; }
    public int RetryAttempt { get; set; }
}

public class BatchSyncResult
{
    public int TotalRecords { get; set; }
    public int ProcessedSuccessfully { get; set; }
    public int Failed { get; set; }
    public List<SyncResult> IndividualResults { get; set; } = new();
    public bool AllSuccessful => Failed == 0;
    public double SuccessRate => TotalRecords > 0 ? (double)ProcessedSuccessfully / TotalRecords : 0;
}