using AirCode.Domain.Entities;

namespace AirCode.Services.Attendance;
public interface IOfflineSyncService
{
    //wai
    Task<bool> SyncPendingRecordsAsync();
    Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecord record);
    Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session);
    Task SchedulePeriodicSync();
}
