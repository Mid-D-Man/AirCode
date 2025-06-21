using AirCode.Domain.Entities;
using AirCode.Services.Firebase;
using AirCode.Services.Storage;
using AirCode.Services.SupaBase;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Services.Attendance;
//maye handle this towmorrow

/*
public class OfflineSyncService : IOfflineSyncService
{
    private readonly ISupabaseEdgeFunctionService _edgeService;
    private readonly IFirestoreService _firestoreService;
    private readonly IBlazorAppLocalStorageService _offlineStorage;
   
    public async Task<bool> SyncPendingRecordsAsync()
    {
        var pendingRecords = await _offlineStorage.GetPendingRecordsAsync();
        var syncResults = new List<SyncResult>();
        
        foreach (var record in pendingRecords)
        {
            try
            {
                var result = await ProcessOfflineAttendanceAsync(record);
                syncResults.Add(result);
                
                if (result.Success)
                {
                    record.Status = SyncStatus.Synced;
                    await _offlineStorage.UpdateRecordAsync(record);
                }
            }
            catch (Exception ex)
            {
                record.RetryCount++;
                record.ErrorDetails = ex.Message;
                record.Status = record.RetryCount > 3 ? SyncStatus.Failed : SyncStatus.Pending;
                await _offlineStorage.UpdateRecordAsync(record);
            }
        }
        
        return syncResults.All(r => r.Success);
    }
    
    public async Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecord record)
    {
        // Decode QR payload
        var sessionData = await QRDecoder.DecodeSessionDataAsync(record.EncryptedQRPayload);
        
        if (sessionData == null)
        {
            return new SyncResult { Success = false, Message = "Invalid QR payload" };
        }
        
        // Check session validity (temporal keys, expiration)
        if (!await ValidateSessionForOfflineSync(sessionData, record.ScannedAt))
        {
            return new SyncResult { Success = false, Message = "Session expired or invalid" };
        }
        
        // Process attendance with retroactive timestamp
        var attendanceRecord = new AttendanceRecord
        {
            MatricNumber = await GetMatricNumberFromDevice(record.DeviceId),
            HasScannedAttendance = true,
            IsOnlineScan = false,
            ScanTime = record.ScannedAt // Use original scan time
        };
        
        return await _edgeService.ProcessAttendanceWithPayloadAsync(
            await QRCodeDecoder.CreateEdgeFunctionRequestAsync(record.EncryptedQRPayload, attendanceRecord)
        );
    }
}
*/