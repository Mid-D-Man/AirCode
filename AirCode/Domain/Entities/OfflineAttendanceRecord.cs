using AirCode.Services.Attendance;

namespace AirCode.Domain.Entities;
// Client offline storage model
public class OfflineAttendanceRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string MatricNumber { get; set; } = string.Empty;
    public string DeviceGuid { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }
    public string EncryptedQrPayload { get; set; } = string.Empty; // Store raw QR without decryption
    public string TemporalKey { get; set; } = string.Empty;
    public bool UseTemporalKeyRefresh { get; set; }
    public int SecurityFeatures { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime ScannedAt { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public int SyncAttempts { get; set; } = 0;
    public string ErrorDetails { get; set; } = string.Empty;
}

public class OfflineSessionData
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public SessionData SessionDetails { get; set; }
    public List<OfflineAttendanceRecord> PendingAttendanceRecords { get; set; } = new();
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
}

public enum SyncStatus
{
    Pending,
    Processing,
    Synced,
    Failed,
    Expired
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