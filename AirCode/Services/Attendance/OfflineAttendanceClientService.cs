using AirCode.Models.Attendance;
using AirCode.Models.Events;

namespace AirCode.Services.Attendance;
using AirCode.Domain.Entities;
using AirCode.Models.QRCode;
using AirCode.Models.Supabase;
using AirCode.Services.Storage;
using AirCode.Services.SupaBase;
using AirCode.Utilities.HelperScripts;
using Microsoft.Extensions.Logging;
using AttendanceRecord = AirCode.Models.Supabase.AttendanceRecord;
/// <summary>
/// Client-side service for handling offline attendance scanning (STUDENT USE ONLY)
/// Stores offline records, syncs when online, removes after successful sync
/// </summary>
public class OfflineAttendanceClientService : IOfflineAttendanceClientService
{
    private readonly ISupabaseEdgeFunctionService _edgeFunctionService;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ConnectivityService _connectivityService;
    private readonly QRCodeDecoder _qrCodeDecoder;
    private readonly ILogger<OfflineAttendanceClientService> _logger;
    
    private Timer _syncTimer;
    private string _matricNumber = string.Empty;
    
    private const string OFFLINE_RECORDS_KEY = "student_offline_attendance";
    private const string MATRIC_KEY = "student_matric";
    private const string DEVICE_GUID_KEY = "student_device_guid";

    public event EventHandler<OfflineAttendanceEventArgs> OfflineAttendanceRecorded;
    public event EventHandler<SyncStatusEventArgs> SyncStatusChanged;
    public event EventHandler<NetworkStatusEventArgs> NetworkStatusChanged;

    public OfflineAttendanceClientService(
        ISupabaseEdgeFunctionService edgeFunctionService,
        IBlazorAppLocalStorageService localStorage,
        ConnectivityService connectivityService,
        QRCodeDecoder qrCodeDecoder,
        ILogger<OfflineAttendanceClientService> logger)
    {
        _edgeFunctionService = edgeFunctionService;
        _localStorage = localStorage;
        _connectivityService = connectivityService;
        _qrCodeDecoder = qrCodeDecoder;
        _logger = logger;
        
        InitializeConnectivityMonitoring();
    }

    #region Public Methods

    public async Task InitializeAsync(string matricNumber)
    {
        _matricNumber = matricNumber;
        await _localStorage.SetItemAsync(MATRIC_KEY, matricNumber);
        
        await _connectivityService.InitializeAsync();
        StartPeriodicSync();
        
        // Sync existing offline records if online
        if (_connectivityService.IsOnline)
        {
            _ = Task.Run(async () => await TrySyncOfflineRecordsAsync());
        }
        
        _logger.LogInformation("Client service initialized for student: {MatricNumber}", matricNumber);
    }

    public async Task<AttendanceResult> ProcessQRCodeScanAsync(string qrCodeContent)
    {
        try
        {
            var isOnline = await _connectivityService.GetSimpleOnlineStatusAsync();
            
            if (isOnline)
            {
                // Try online first
                var onlineResult = await ProcessOnlineAttendanceAsync(qrCodeContent);
                if (onlineResult.Success)
                {
                    return onlineResult;
                }
                _logger.LogWarning("Online processing failed, falling back to offline");
            }
            
            // Process offline
            return await ProcessOfflineAttendanceAsync(qrCodeContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing QR scan");
            return new AttendanceResult
            {
                Success = false,
                Message = "Processing error occurred",
                IsOfflineMode = !_connectivityService.IsOnline
            };
        }
    }

    public async Task<SyncStatusInfo> GetSyncStatusAsync()
    {
        var offlineRecords = await GetOfflineRecordsAsync();
        
        return new SyncStatusInfo
        {
            IsOnline = _connectivityService.IsOnline,
            PendingRecordsCount = offlineRecords.Count,
            HasPendingRecords = offlineRecords.Any(),
            LastSyncAttempt = DateTime.UtcNow // TODO: Store actual timestamp
        };
    }

    public async Task<bool> ManualSyncAsync()
    {
        if (!_connectivityService.IsOnline)
        {
            return false;
        }
        
        NotifySyncStatusChanged("Syncing offline records...", true);
        var result = await TrySyncOfflineRecordsAsync();
        
        var message = result ? "Sync completed successfully" : "Sync failed";
        NotifySyncStatusChanged(message, false);
        
        return result;
    }

    public async Task<bool> ClearOfflineRecordsAsync()
    {
        try
        {
            await _localStorage.RemoveItemAsync(OFFLINE_RECORDS_KEY);
            NotifySyncStatusChanged("Offline records cleared", false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear offline records");
            return false;
        }
    }

    #endregion

    #region Private Methods

    private async Task<AttendanceResult> ProcessOnlineAttendanceAsync(string qrCodeContent)
    {
        try
        {
            // Create attendance data for edge function
            var attendanceData = new AttendanceRecord
            {
                MatricNumber = _matricNumber,
                HasScannedAttendance = true,
                ScanTime = DateTime.UtcNow,
                IsOnlineScan = true,
                DeviceGUID = await GetOrCreateDeviceGuidAsync()
            };

            var edgeRequest = await _qrCodeDecoder.CreateEdgeFunctionRequestAsync(qrCodeContent, attendanceData);
            if (edgeRequest == null)
            {
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Invalid or expired QR code",
                    IsOfflineMode = false
                };
            }

            var result = await _edgeFunctionService.ProcessAttendanceWithPayloadAsync(edgeRequest);
            
            return new AttendanceResult
            {
                Success = result.Success,
                Message = result.Message,
                SessionData = result.SessionData,
                IsOfflineMode = false,
                ErrorCode = result.ErrorCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Online processing failed");
            return new AttendanceResult
            {
                Success = false,
                Message = "Network error",
                IsOfflineMode = false
            };
        }
    }

    private async Task<AttendanceResult> ProcessOfflineAttendanceAsync(string qrCodeContent)
    {
        try
        {
            var deviceGuid = await GetOrCreateDeviceGuidAsync();
            
            var offlineRecord = new OfflineAttendanceRecordModel
            {
                Id = Guid.NewGuid().ToString(),
                EncryptedQrPayload = qrCodeContent,
                MatricNumber = _matricNumber,
                DeviceGuid = deviceGuid,
                ScannedAt = DateTime.UtcNow,
                RecordedAt = DateTime.UtcNow,
                Status = SyncStatus.Pending,
                SyncStatus = SyncStatus.Pending
            };

            // Check for duplicates
            var existingRecords = await GetOfflineRecordsAsync();
            if (existingRecords.Any(r => r.EncryptedQrPayload == qrCodeContent && 
                                        r.MatricNumber == _matricNumber))
            {
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Duplicate attendance record",
                    IsOfflineMode = true
                };
            }

            existingRecords.Add(offlineRecord);
            await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, existingRecords);
            
            NotifyOfflineAttendanceRecorded(qrCodeContent);
            
            return new AttendanceResult
            {
                Success = true,
                Message = "Attendance recorded offline. Will sync when online.",
                IsOfflineMode = true,
                RequiresSync = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offline processing failed");
            return new AttendanceResult
            {
                Success = false,
                Message = "Failed to store offline attendance",
                IsOfflineMode = true
            };
        }
    }

    private async Task<bool> TrySyncOfflineRecordsAsync()
    {
        try
        {
            var offlineRecords = await GetOfflineRecordsAsync();
            if (!offlineRecords.Any())
            {
                return true;
            }

            var recordsToRemove = new List<string>();
            var allSuccessful = true;

            foreach (var record in offlineRecords)
            {
                try
                {
                    // Create attendance data for sync request
                    var attendanceData = new AttendanceRecord
                    {
                        MatricNumber = record.MatricNumber,
                        HasScannedAttendance = true,
                        ScanTime = record.ScannedAt,
                        IsOnlineScan = false,
                        DeviceGUID = record.DeviceGuid
                    };

                    var edgeRequest = await _qrCodeDecoder.CreateEdgeFunctionRequestAsync(
                        record.EncryptedQrPayload, attendanceData);
                    
                    if (edgeRequest != null)
                    {
                        var result = await _edgeFunctionService.ProcessAttendanceWithPayloadAsync(edgeRequest);
                        
                        if (result.Success || result.ErrorCode == "ALREADY_SCANNED")
                        {
                            recordsToRemove.Add(record.Id);
                            _logger.LogInformation("Synced offline record: {RecordId}", record.Id);
                        }
                        else if (result.ErrorCode == "SESSION_EXPIRED")
                        {
                            recordsToRemove.Add(record.Id);
                            _logger.LogWarning("Removed expired record: {RecordId}", record.Id);
                        }
                        else
                        {
                            allSuccessful = false;
                            _logger.LogWarning("Sync failed for record {RecordId}: {Error}", 
                                record.Id, result.Message);
                        }
                    }
                    else
                    {
                        recordsToRemove.Add(record.Id);
                        _logger.LogWarning("Invalid QR payload, removing record: {RecordId}", record.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing record: {RecordId}", record.Id);
                    allSuccessful = false;
                }
            }

            // Remove successfully synced/expired records
            if (recordsToRemove.Any())
            {
                var remainingRecords = offlineRecords.Where(r => !recordsToRemove.Contains(r.Id)).ToList();
                await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, remainingRecords);
            }

            return allSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync process failed");
            return false;
        }
    }

    private async Task<List<OfflineAttendanceRecordModel>> GetOfflineRecordsAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<List<OfflineAttendanceRecordModel>>(OFFLINE_RECORDS_KEY) 
                   ?? new List<OfflineAttendanceRecordModel>();
        }
        catch
        {
            return new List<OfflineAttendanceRecordModel>();
        }
    }

    private async Task<string> GetOrCreateDeviceGuidAsync()
    {
        try
        {
            var deviceGuid = await _localStorage.GetItemAsync<string>(DEVICE_GUID_KEY);
            if (string.IsNullOrEmpty(deviceGuid))
            {
                deviceGuid = Guid.NewGuid().ToString();
                await _localStorage.SetItemAsync(DEVICE_GUID_KEY, deviceGuid);
            }
            return deviceGuid;
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    private void InitializeConnectivityMonitoring()
    {
        _connectivityService.ConnectivityChanged += (status) =>
        {
            NetworkStatusChanged?.Invoke(this, new NetworkStatusEventArgs
            {
                IsOnline = status.IsOnline,
                Timestamp = DateTime.UtcNow
            });
            
            if (status.IsOnline)
            {
                _ = Task.Run(async () => await TrySyncOfflineRecordsAsync());
            }
        };
    }

    private void StartPeriodicSync()
    {
        _syncTimer = new Timer(async _ =>
        {
            if (_connectivityService.IsOnline)
            {
                await TrySyncOfflineRecordsAsync();
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));
    }

    private void NotifyOfflineAttendanceRecorded(string qrCodeContent)
    {
        OfflineAttendanceRecorded?.Invoke(this, new OfflineAttendanceEventArgs
        {
            QRCodeContent = qrCodeContent,
            Timestamp = DateTime.UtcNow
        });
    }

    private void NotifySyncStatusChanged(string message, bool isInProgress)
    {
        SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
        {
            Message = message,
            IsInProgress = isInProgress,
            Timestamp = DateTime.UtcNow
        });
    }

    #endregion

    public void Dispose()
    {
        _syncTimer?.Dispose();
    }
}