using AirCode.Domain.Entities;

namespace AirCode.Services.Attendance;
public interface IOfflineSyncService
{
    Task<bool> SyncPendingRecordsAsync();
    Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecord record);
    Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session);
    Task SchedulePeriodicSync();
}
