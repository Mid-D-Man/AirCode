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
/// Simplified client-side service for handling offline attendance scanning (STUDENT USE ONLY)
/// Uses the existing OfflineAttendanceRecordModel from Domain.Entities
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
            // First, decode the QR to extract session info
            var sessionData = await _qrCodeDecoder.DecodeSessionDataAsync(qrCodeContent);
            if (sessionData == null)
            {
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Invalid or expired QR code",
                    IsOfflineMode = false
                };
            }

            var isOnline = await _connectivityService.GetSimpleOnlineStatusAsync();
            
            if (isOnline)
            {
                // Try online first
                var onlineResult = await ProcessOnlineAttendanceAsync(qrCodeContent, sessionData);
                if (onlineResult.Success)
                {
                    // Online success - no need to store offline
                    return onlineResult;
                }
                
                // Online failed - check if it's because session doesn't exist on server
                if (IsSessionNotFoundError(onlineResult.ErrorCode))
                {
                    _logger.LogInformation("Session {SessionId} not found on server - likely offline generated, storing for later sync", 
                        sessionData.SessionId);
                    return await ProcessOfflineAttendanceAsync(qrCodeContent, sessionData, "Session not yet synced to server");
                }
                
                // Other online errors - try offline as fallback
                _logger.LogWarning("Online processing failed with error {ErrorCode}, falling back to offline", onlineResult.ErrorCode);
            }
            
            // Process offline (either we're offline or online failed for non-session reasons)
            return await ProcessOfflineAttendanceAsync(qrCodeContent, sessionData);
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

    private async Task<AttendanceResult> ProcessOnlineAttendanceAsync(string qrCodeContent, DecodedSessionData sessionData)
    {
        try
        {
            // Check for duplicate attendance first (locally)
            if (await HasExistingAttendanceRecord(sessionData.SessionId, sessionData.CourseCode))
            {
                return new AttendanceResult
                {
                    Success = false,
                    Message = "You have already marked attendance for this session",
                    ErrorCode = "DUPLICATE_ATTENDANCE",
                    IsOfflineMode = false
                };
            }

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
            
            // Convert DecodedSessionData to QRCodePayloadData for the result
            var payloadData = new QRCodePayloadData
            {
                SessionId = sessionData.SessionId,
                CourseCode = sessionData.CourseCode,
                StartTime = sessionData.StartTime,
                EndTime = sessionData.ExpirationTime,
                TemporalKey = sessionData.TemporalKey,
                UseTemporalKeyRefresh = sessionData.UseTemporalKeyRefresh,
                AllowOfflineConnectionAndSync = sessionData.AllowOfflineConnectionAndSync,
                SecurityFeatures = sessionData.SecurityFeatures
            };
            
            return new AttendanceResult
            {
                Success = result.Success,
                Message = result.Message,
                SessionData = payloadData,
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
                ErrorCode = "NETWORK_ERROR",
                IsOfflineMode = false
            };
        }
    }

    private async Task<AttendanceResult> ProcessOfflineAttendanceAsync(string qrCodeContent, DecodedSessionData sessionData, string reason = null)
    {
        try
        {
            var deviceGuid = await GetOrCreateDeviceGuidAsync();
            
            // Check for duplicate offline records for this session and student
            var existingRecords = await GetOfflineRecordsAsync();
            var existingRecord = existingRecords.FirstOrDefault(r => 
                r.SessionId == sessionData.SessionId && 
                r.MatricNumber == _matricNumber);

            if (existingRecord != null)
            {
                _logger.LogInformation("Duplicate offline attendance attempt for session {SessionId} by student {MatricNumber}", 
                    sessionData.SessionId, _matricNumber);
                
                return new AttendanceResult
                {
                    Success = false,
                    Message = "You have already recorded attendance for this session offline",
                    ErrorCode = "DUPLICATE_ATTENDANCE",
                    IsOfflineMode = true
                };
            }

            // Use the existing OfflineAttendanceRecordModel from Domain.Entities
            var offlineRecord = new OfflineAttendanceRecordModel
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = sessionData.SessionId,
                CourseCode = sessionData.CourseCode,
                EncryptedQrPayload = qrCodeContent,
                MatricNumber = _matricNumber,
                DeviceGuid = deviceGuid,
                ScannedAt = DateTime.UtcNow,
                RecordedAt = DateTime.UtcNow,
                Status = SyncStatus.Pending,
                SyncStatus = SyncStatus.Pending,
                OfflineReason = reason ?? "Offline mode"
            };

            existingRecords.Add(offlineRecord);
            await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, existingRecords);
            
            NotifyOfflineAttendanceRecorded(qrCodeContent);
            
            var message = reason != null 
                ? $"Attendance recorded offline: {reason}" 
                : "Attendance recorded offline. Will sync when online.";

            // Convert DecodedSessionData to QRCodePayloadData for the result
            var payloadData = new QRCodePayloadData
            {
                SessionId = sessionData.SessionId,
                CourseCode = sessionData.CourseCode,
                StartTime = sessionData.StartTime,
                EndTime = sessionData.ExpirationTime,
                TemporalKey = sessionData.TemporalKey,
                UseTemporalKeyRefresh = sessionData.UseTemporalKeyRefresh,
                AllowOfflineConnectionAndSync = sessionData.AllowOfflineConnectionAndSync,
                SecurityFeatures = sessionData.SecurityFeatures
            };
            
            return new AttendanceResult
            {
                Success = true,
                Message = message,
                IsOfflineMode = true,
                RequiresSync = true,
                SessionData = payloadData
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

            _logger.LogInformation("Starting sync of {Count} offline attendance records", offlineRecords.Count);

            var recordsToRemove = new List<string>();
            var syncedCount = 0;
            var allSuccessful = true;

            foreach (var record in offlineRecords.OrderBy(r => r.ScannedAt))
            {
                try
                {
                    // Create attendance data for sync request
                    var attendanceData = new AttendanceRecord
                    {
                        MatricNumber = record.MatricNumber,
                        HasScannedAttendance = true,
                        ScanTime = record.ScannedAt,
                        IsOnlineScan = false, // Mark as offline sync
                        DeviceGUID = record.DeviceGuid
                    };

                    var edgeRequest = await _qrCodeDecoder.CreateEdgeFunctionRequestAsync(
                        record.EncryptedQrPayload, attendanceData);
                    
                    if (edgeRequest != null)
                    {
                        var result = await _edgeFunctionService.ProcessAttendanceWithPayloadAsync(edgeRequest);
                        
                        if (result.Success)
                        {
                            recordsToRemove.Add(record.Id);
                            syncedCount++;
                            _logger.LogInformation("Successfully synced offline record: {RecordId} for session {SessionId}", 
                                record.Id, record.SessionId);
                        }
                        else if (ShouldRemoveFailedRecord(result.ErrorCode))
                        {
                            recordsToRemove.Add(record.Id);
                            _logger.LogWarning("Removing offline record {RecordId} due to {ErrorCode}: {Message}", 
                                record.Id, result.ErrorCode, result.Message);
                        }
                        else
                        {
                            allSuccessful = false;
                            _logger.LogWarning("Sync failed for record {RecordId}: {ErrorCode} - {Message}", 
                                record.Id, result.ErrorCode, result.Message);
                        }
                    }
                    else
                    {
                        recordsToRemove.Add(record.Id);
                        _logger.LogWarning("Invalid QR payload in offline record {RecordId}, removing", record.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing offline record: {RecordId}", record.Id);
                    allSuccessful = false;
                }
            }

            // Remove successfully synced or invalid records
            if (recordsToRemove.Any())
            {
                var remainingRecords = offlineRecords.Where(r => !recordsToRemove.Contains(r.Id)).ToList();
                await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, remainingRecords);
                
                _logger.LogInformation("Synced {SyncedCount} records, {RemainingCount} remain pending", 
                    syncedCount, remainingRecords.Count);
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

    private async Task<bool> HasExistingAttendanceRecord(string sessionId, string courseCode)
    {
        try
        {
            var offlineRecords = await GetOfflineRecordsAsync();
            return offlineRecords.Any(r => 
                r.SessionId == sessionId && 
                r.MatricNumber == _matricNumber &&
                r.CourseCode == courseCode);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSessionNotFoundError(string errorCode)
    {
        return errorCode switch
        {
            "SESSION_NOT_FOUND" => true,
            "SESSION_QUERY_ERROR" => true,
            "INVALID_SESSION" => true,
            _ => false
        };
    }

    private static bool ShouldRemoveFailedRecord(string errorCode)
    {
        return errorCode switch
        {
            "DUPLICATE_ATTENDANCE" => true, // Already recorded online
            "ALREADY_SCANNED" => true, // Same as duplicate
            "SESSION_EXPIRED" => true, // Too old to sync
            "TEMPORAL_KEY_EXPIRED" => true, // QR code expired
            "INVALID_SESSION" => true, // Session doesn't exist
            "INVALID_QR_CODE" => true, // Malformed QR
            "INVALID_JSON" => true, // Corrupted data
            _ => false
        };
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
                _logger.LogInformation("Network connectivity restored, starting sync");
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