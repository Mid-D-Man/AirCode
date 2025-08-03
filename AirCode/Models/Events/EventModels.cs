namespace AirCode.Models.Events;

public class OfflineAttendanceEventArgs : EventArgs
{
    public string QRCodeContent { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class SyncStatusEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public bool IsInProgress { get; set; }
    public DateTime Timestamp { get; set; }
}

public class NetworkStatusEventArgs : EventArgs
{
    public bool IsOnline { get; set; }
    public DateTime Timestamp { get; set; }
}