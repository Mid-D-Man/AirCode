using AirCode.Models.QRCode;

namespace AirCode.Models.Attendance;


public class AttendanceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public QRCodePayloadData? SessionData { get; set; }
    public bool IsOfflineMode { get; set; }
    public bool RequiresSync { get; set; }
}

public class SyncStatusInfo
{
    public bool IsOnline { get; set; }
    public int PendingRecordsCount { get; set; }
    public bool HasPendingRecords { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}
