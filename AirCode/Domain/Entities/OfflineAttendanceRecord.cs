using AirCode.Services.Attendance;

namespace AirCode.Domain.Entities;
// Client offline storage model
public class OfflineAttendanceRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EncryptedQRPayload { get; set; } // Store raw QR without decryption
    public DateTime ScannedAt { get; set; }
    public string DeviceId { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public string ErrorDetails { get; set; }
}
public class OfflineSessionData
{
    public string SessionId { get; set; }
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