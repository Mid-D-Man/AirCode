using AirCode.Domain.Entities;

namespace AirCode.Services.Attendance;
/// <summary>
/// offline sync service for offline stuff
/// </summary>
public interface IOfflineSyncService
{
    //wai
    Task<bool> SyncPendingRecordsAsync();
    Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecordModel recordModel);
    Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session);
    Task SchedulePeriodicSync();
}
