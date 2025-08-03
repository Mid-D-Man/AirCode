using AirCode.Domain.Entities;
using AirCode.Models.QRCode;
using AirCode.Models.Supabase;
using AirCode.Services.Storage;
using AirCode.Services.SupaBase;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AirCode.Models.EdgeFunction;
using AttendanceRecord = AirCode.Models.Supabase.AttendanceRecord;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Services.Attendance
{
    /// <summary>
    /// Enhanced offline sync service for handling client-side offline attendance
    /// </summary>
    public class OfflineSyncService : IOfflineSyncService
    {
        private readonly ISupabaseDatabase _database;
        private readonly ISupabaseEdgeFunctionService _edgeService;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly ILogger<OfflineSyncService> _logger;
        
        private const string OFFLINE_RECORDS_KEY = "offline_attendance_records";
        private const string OFFLINE_SESSIONS_KEY = "offline_session_data";
        private const string MATRIC_NUMBER_KEY = "current_matric_number";
        private const string DEVICE_GUID_KEY = "device_guid";
        private const int MAX_RETRY_COUNT = 3;
        private const int SYNC_RETRY_DELAY_MS = 2000;

        public OfflineSyncService(
            ISupabaseDatabase database,
            ISupabaseEdgeFunctionService edgeService,
            IBlazorAppLocalStorageService localStorage,
            ILogger<OfflineSyncService> logger)
        {
            _database = database;
            _edgeService = edgeService;
            _localStorage = localStorage;
            _logger = logger;
        }

        #region Client-Side Offline Attendance Methods

        /// <summary>
        /// Store offline attendance record when client scans QR code offline
        /// </summary>
        public async Task<bool> StoreOfflineAttendanceAsync(string encryptedQRPayload, string deviceId = null)
        {
            try
            {
                // Get or generate device GUID
                deviceId ??= await GetOrCreateDeviceGuidAsync();
                var matricNumber = await GetStoredMatricNumberAsync();
                
                if (string.IsNullOrEmpty(matricNumber))
                {
                    _logger.LogError("No matric number stored for offline attendance");
                    return false;
                }

                var offlineRecord = new OfflineAttendanceRecordModel
                {
                    Id = Guid.NewGuid().ToString(),
                    EncryptedQrPayload = encryptedQRPayload,
                    MatricNumber = matricNumber,
                    DeviceGuid = deviceId,
                    ScannedAt = DateTime.UtcNow,
                    RecordedAt = DateTime.UtcNow,
                    Status = SyncStatus.Pending,
                    SyncStatus = SyncStatus.Pending,
                    RetryCount = 0,
                    SyncAttempts = 0
                };

                // Check for duplicates before storing
                var existingRecords = await GetPendingOfflineRecordsAsync();
                var duplicate = existingRecords.FirstOrDefault(r => 
                    r.EncryptedQrPayload == encryptedQRPayload && 
                    r.MatricNumber == matricNumber &&
                    r.Status != SyncStatus.Failed);

                if (duplicate != null)
                {
                    _logger.LogWarning("Duplicate offline attendance record detected, skipping storage");
                    return false;
                }

                existingRecords.Add(offlineRecord);
                await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, existingRecords);
                
                _logger.LogInformation("Stored offline attendance record: {RecordId} for student: {MatricNumber}", 
                    offlineRecord.Id, matricNumber);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store offline attendance record");
                return false;
            }
        }

        /// <summary>
        /// Sync pending offline records when connection is restored
        /// </summary>
        public async Task<bool> SyncPendingRecordsAsync()
        {
            try
            {
                _logger.LogInformation("Starting offline records synchronization");
                
                var pendingRecords = await GetPendingOfflineRecordsAsync();
                if (!pendingRecords.Any())
                {
                    _logger.LogInformation("No pending offline records to sync");
                    return true;
                }

                var syncResults = new List<SyncResult>();
                var recordsToRemove = new List<string>();
                
                foreach (var record in pendingRecords.Where(r => r.Status == SyncStatus.Pending || r.Status == SyncStatus.Failed))
                {
                    try
                    {
                        var result = await ProcessOfflineAttendanceAsync(record);
                        syncResults.Add(result);
                        
                        // Update record status based on sync result
                        if (result.Success)
                        {
                            record.Status = SyncStatus.Synced;
                            record.SyncStatus = SyncStatus.Synced;
                            recordsToRemove.Add(record.Id);
                            _logger.LogInformation("Successfully synced offline record: {RecordId}", record.Id);
                        }
                        else
                        {
                            record.RetryCount++;
                            record.SyncAttempts++;
                            record.ErrorDetails = result.ErrorMessage;
                            
                            // Check if we should delete the record (expired session)
                            if (result.ErrorCode == "SESSION_EXPIRED")
                            {
                                recordsToRemove.Add(record.Id);
                                _logger.LogWarning("Removing expired offline record: {RecordId}", record.Id);
                            }
                            else if (record.RetryCount >= MAX_RETRY_COUNT)
                            {
                                record.Status = SyncStatus.Failed;
                                record.SyncStatus = SyncStatus.Failed;
                                _logger.LogError("Offline record failed after max retries: {RecordId}", record.Id);
                            }
                            else
                            {
                                record.Status = SyncStatus.Pending;
                                _logger.LogWarning("Offline record sync failed, will retry: {RecordId}. Error: {Error}", 
                                    record.Id, result.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while processing offline record: {RecordId}", record.Id);
                        record.RetryCount++;
                        record.ErrorDetails = ex.Message;
                        if (record.RetryCount >= MAX_RETRY_COUNT)
                        {
                            record.Status = SyncStatus.Failed;
                        }
                    }
                }

                // Remove successfully synced and expired records
                if (recordsToRemove.Any())
                {
                    var updatedRecords = pendingRecords.Where(r => !recordsToRemove.Contains(r.Id)).ToList();
                    await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, updatedRecords);
                }
                else
                {
                    // Update existing records with new status
                    await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, pendingRecords);
                }
                
                var successfulSyncs = syncResults.Count(r => r.Success);
                _logger.LogInformation("Sync completed: {Successful}/{Total} records synced successfully", 
                    successfulSyncs, syncResults.Count);
                
                return successfulSyncs > 0 || !pendingRecords.Any(r => r.Status == SyncStatus.Pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync pending offline records");
                return false;
            }
        }

        /// <summary>
        /// Process individual offline attendance record through edge function
        /// </summary>
        public async Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecordModel record)
        {
            try
            {
                _logger.LogDebug("Processing offline attendance record: {RecordId}", record.Id);
                
                // Decode QR payload to get session details
                var qrPayloadData = await DecodeQRPayloadAsync(record.EncryptedQrPayload);
                if (qrPayloadData == null)
                {
                    return new SyncResult
                    {
                        Success = false,
                        Message = "Failed to decode QR payload",
                        ErrorCode = "INVALID_QR_PAYLOAD",
                        RetryAttempt = record.RetryCount
                    };
                }

                // Create attendance data for edge function
                var attendanceData = new
                {
                    MatricNumber = record.MatricNumber,
                    DeviceGUID = record.DeviceGuid,
                    ScannedAt = record.ScannedAt
                };

                // Create edge function request for offline processing
                var edgeRequest = new EdgeFunctionRequest
                {
                    QrCodePayload = qrPayloadData,
                    AttendanceData = attendanceData
                };

                // Call the offline attendance edge function
                var result = await CallOfflineAttendanceEdgeFunctionAsync(edgeRequest);
                
                return new SyncResult
                {
                    Success = result.Success,
                    Message = result.Message,
                    ErrorMessage = result.Success ? string.Empty : result.Message,
                    ErrorCode = result.ErrorCode ?? string.Empty,
                    Data = result,
                    RetryAttempt = record.RetryCount,
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process offline attendance record: {RecordId}", record.Id);
                return new SyncResult
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorMessage = ex.Message,
                    ErrorCode = "PROCESSING_ERROR",
                    RetryAttempt = record.RetryCount
                };
            }
        }

        /// <summary>
        /// Check if user has pending offline attendance records
        /// </summary>
        public async Task<bool> HasPendingOfflineRecordsAsync()
        {
            try
            {
                var records = await GetPendingOfflineRecordsAsync();
                return records.Any(r => r.Status == SyncStatus.Pending || r.Status == SyncStatus.Failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check pending offline records");
                return false;
            }
        }

        /// <summary>
        /// Get count of pending offline records
        /// </summary>
        public async Task<int> GetPendingOfflineRecordsCountAsync()
        {
            try
            {
                var records = await GetPendingOfflineRecordsAsync();
                return records.Count(r => r.Status == SyncStatus.Pending || r.Status == SyncStatus.Failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending offline records count");
                return 0;
            }
        }

        /// <summary>
        /// Clear all offline records (use with caution)
        /// </summary>
        public async Task<bool> ClearAllOfflineRecordsAsync()
        {
            try
            {
                await _localStorage.RemoveItemAsync(OFFLINE_RECORDS_KEY);
                _logger.LogInformation("Cleared all offline attendance records");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear offline records");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private async Task<List<OfflineAttendanceRecordModel>> GetPendingOfflineRecordsAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<List<OfflineAttendanceRecordModel>>(OFFLINE_RECORDS_KEY) 
                       ?? new List<OfflineAttendanceRecordModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending offline records");
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
                    _logger.LogInformation("Generated new device GUID: {DeviceGuid}", deviceGuid);
                }
                return deviceGuid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get or create device GUID");
                return Guid.NewGuid().ToString(); // Fallback
            }
        }

        private async Task<string> GetStoredMatricNumberAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<string>(MATRIC_NUMBER_KEY) ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get stored matric number");
                return string.Empty;
            }
        }

        /// <summary>
        /// Store matric number for offline use
        /// </summary>
        public async Task<bool> StoreMatricNumberAsync(string matricNumber)
        {
            try
            {
                await _localStorage.SetItemAsync(MATRIC_NUMBER_KEY, matricNumber);
                _logger.LogInformation("Stored matric number for offline use");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store matric number");
                return false;
            }
        }

        private async Task<QRCodePayloadData> DecodeQRPayloadAsync(string encryptedQRPayload)
        {
            try
            {
                // TODO: Implement QR payload decoding using your existing QR decoder utilities
                // For now, this is a placeholder that assumes the payload contains the necessary data
                // You'll need to integrate this with your existing QRCodeDecoder service
                
                // Example implementation (replace with actual decoder):
                var decodedJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encryptedQRPayload));
                var payloadData = JsonSerializer.Deserialize<QRCodePayloadData>(decodedJson);
                
                return payloadData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode QR payload");
                return null;
            }
        }

        private async Task<AttendanceProcessingResult> CallOfflineAttendanceEdgeFunctionAsync(EdgeFunctionRequest request)
        {
            try
            {
                // Create HTTP client request to the offline attendance edge function
                using var httpClient = new HttpClient();
                var supabaseUrl = "https://bjwbwcbumfqcdmrsbtkf.supabase.co";
                var functionUrl = $"{supabaseUrl}/functions/v1/process-offline-attendance";
                
                var requestJson = JsonSerializer.Serialize(request);
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                
                // Add authorization header if needed
                var supabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImJqd2J3Y2J1bWZxY2RtcnNidGtmIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NDM3MTY0NzYsImV4cCI6MjA1OTI5MjQ3Nn0.EHg5Op2GVE9GhNmR9tEKFyVow2987rp3dsIgJxFVJD8";
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", supabaseKey);
                
                var response = await httpClient.PostAsync(functionUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Offline edge function response: Status={Status}, Content={Content}", 
                    response.StatusCode, responseContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var edgeResponse = JsonSerializer.Deserialize<EdgeFunctionResponse>(responseContent);
                    return new AttendanceProcessingResult
                    {
                        Success = edgeResponse?.Success ?? false,
                        Message = edgeResponse?.Message ?? "Unknown response",
                        ErrorCode = edgeResponse?.ErrorCode,
                        SessionData = edgeResponse?.SessionData != null ? new QRCodePayloadData
                        {
                            SessionId = edgeResponse.SessionData.SessionId,
                            CourseCode = edgeResponse.SessionData.CourseCode,
                            StartTime = edgeResponse.SessionData.StartTime,
                            EndTime = edgeResponse.SessionData.EndTime
                        } : null
                    };
                }
                else
                {
                    // Parse error response
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<EdgeFunctionErrorResponse>(responseContent);
                        return new AttendanceProcessingResult
                        {
                            Success = false,
                            Message = errorResponse?.Message ?? "Unknown error",
                            ErrorCode = errorResponse?.ErrorCode ?? "UNKNOWN_ERROR"
                        };
                    }
                    catch
                    {
                        return new AttendanceProcessingResult
                        {
                            Success = false,
                            Message = $"HTTP {response.StatusCode}: {responseContent}",
                            ErrorCode = "HTTP_ERROR"
                        };
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error calling offline attendance edge function");
                return new AttendanceProcessingResult
                {
                    Success = false,
                    Message = "Network connection failed. Will retry when connection is restored.",
                    ErrorCode = "NETWORK_ERROR"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling offline attendance edge function");
                return new AttendanceProcessingResult
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorCode = "PROCESSING_ERROR"
                };
            }
        }

        #endregion

        #region Admin-Specific Methods (existing implementation)

        public async Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session)
        {
            try
            {
                _logger.LogDebug("Syncing offline session: {SessionId}", session.SessionId);
                
                var offlineSession = new SupabaseOfflineAttendanceSession
                {
                    SessionId = session.SessionId,
                    CourseCode = session.SessionDetails.CourseCode,
                    StartTime = session.SessionDetails.StartTime,
                    Duration = session.SessionDetails.Duration,
                    CreatedAt = session.CreatedAt,
                    OfflineRecords = JsonSerializer.Serialize(session.PendingAttendanceRecords),
                    SyncStatus = (int)session.SyncStatus
                };

                await _database.InsertAsync(offlineSession);
                
                _logger.LogInformation("Successfully synced offline session to database: {SessionId}", session.SessionId);
                
                return new SyncResult
                {
                    Success = true,
                    Message = "Offline session synced successfully",
                    Data = offlineSession,
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync offline session: {SessionId}", session.SessionId);
                return new SyncResult
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorCode = "SESSION_SYNC_ERROR"
                };
            }
        }

        public async Task SchedulePeriodicSync()
        {
            try
            {
                // Implement periodic sync logic
                var timer = new Timer(async _ => await SyncPendingRecordsAsync(), 
                    null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
                
                _logger.LogInformation("Periodic sync scheduled every 5 minutes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule periodic sync");
            }
        }

        #endregion
        
        #region Admin Direct Database Methods (bypasses edge functions)

        /// <summary>
        /// Directly insert attendance record into database (admin only)
        /// </summary>
        public async Task<bool> DirectInsertAttendanceRecordAsync(string sessionId, AttendanceRecord attendanceRecord)
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseAttendanceSession>(
                    "session_id", Supabase.Postgrest.Constants.Operator.Equals, sessionId);
                
                if (!sessions.Any())
                {
                    _logger.LogError("Session not found for direct insert: {SessionId}", sessionId);
                    return false;
                }

                var session = sessions.First();
                var existingRecords = session.GetAttendanceRecords();
                
                // Check for duplicates
                if (existingRecords.Any(r => r.MatricNumber == attendanceRecord.MatricNumber))
                {
                    _logger.LogWarning("Duplicate attendance record - student already marked: {MatricNumber}", 
                        attendanceRecord.MatricNumber);
                    return false;
                }

                existingRecords.Add(attendanceRecord);
                session.SetAttendanceRecords(existingRecords);
                
                await _database.UpdateAsync(session);
                
                _logger.LogInformation("Admin directly inserted attendance record: {MatricNumber} in {SessionId}", 
                    attendanceRecord.MatricNumber, sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to directly insert attendance record");
                return false;
            }
        }

        /// <summary>
        /// Get all offline sessions for a course (admin view)
        /// </summary>
        public async Task<List<OfflineSessionData>> GetOfflineSessionsByCourseAsync(string courseCode)
        {
            try
            {
                var offlineSessions = await _database.GetWithFilterAsync<SupabaseOfflineAttendanceSession>(
                    "course_code", Supabase.Postgrest.Constants.Operator.Equals, courseCode);

                return offlineSessions.Select(session => new OfflineSessionData
                {
                    SessionId = session.SessionId,
                    CreatedAt = session.CreatedAt,
                    SessionDetails = new SessionData
                    {
                        SessionId = session.SessionId,
                        CourseCode = session.CourseCode,
                        StartTime = session.StartTime,
                        Duration = session.Duration,
                        EndTime = session.ExpirationTime,
                        SecurityFeatures = (SecurityFeatures)session.SecurityFeatures,
                        OfflineSyncEnabled = session.AllowOfflineSync
                    },
                    PendingAttendanceRecords = JsonSerializer.Deserialize<List<OfflineAttendanceRecordModel>>(
                        session.OfflineRecords) ?? new List<OfflineAttendanceRecordModel>(),
                    SyncStatus = (SyncStatus)session.SyncStatus
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline sessions for course: {CourseCode}", courseCode);
                return new List<OfflineSessionData>();
            }
        }

        /// <summary>
        /// Archive old offline sessions (admin maintenance)
        /// </summary>
        public async Task<int> ArchiveOldOfflineSessionsAsync(int olderThanDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                var oldSessions = await _database.GetAsync<SupabaseOfflineAttendanceSession>();
                
                var sessionsToArchive = oldSessions
                    .Where(s => s.CreatedAt < cutoffDate && s.SyncStatus == (int)SyncStatus.Synced)
                    .ToList();

                int archivedCount = 0;
                
                foreach (var session in sessionsToArchive)
                {
                    try
                    {
                        // Create archived record
                        var archivedData = new ArchivedAttendanceData
                        {
                            CourseCode = session.CourseCode,
                            ArchivedData = JsonSerializer.Serialize(session),
                            ArchivedAt = DateTime.UtcNow,
                            DataType = "offline_sessions",
                            CompressionUsed = false
                        };
                        
                        await _database.InsertAsync(archivedData);
                        await _database.DeleteAsync(session);
                        
                        archivedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to archive session: {SessionId}", session.SessionId);
                    }
                }

                _logger.LogInformation("Archived {Count} old offline sessions", archivedCount);
                return archivedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive old offline sessions");
                return 0;
            }
        }

        /// <summary>
        /// Admin method to create offline session records in Supabase when online
        /// </summary>
        public async Task<bool> CreateOfflineSessionRecordAsync(SessionData sessionData)
        {
            try
            {
                if (!sessionData.OfflineSyncEnabled)
                {
                    _logger.LogDebug("Offline sync not enabled for session: {SessionId}", sessionData.SessionId);
                    return false;
                }

                var offlineSession = new SupabaseOfflineAttendanceSession
                {
                    SessionId = sessionData.SessionId,
                    CourseCode = sessionData.CourseCode,
                    StartTime = sessionData.StartTime,
                    Duration = sessionData.Duration,
                    ExpirationTime = sessionData.EndTime,
                    CreatedAt = DateTime.UtcNow,
                    OfflineRecords = "[]", // Empty initially
                    SyncStatus = (int)SyncStatus.Pending,
                    AllowOfflineSync = sessionData.OfflineSyncEnabled,
                    SecurityFeatures = (int)sessionData.SecurityFeatures
                };

                await _database.InsertAsync(offlineSession);
                _logger.LogInformation("Admin created offline session record: {SessionId}", sessionData.SessionId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create offline session record: {SessionId}", sessionData.SessionId);
                return false;
            }
        }

        /// <summary>
        /// Admin method to sync offline records to main attendance session (bypasses edge functions)
        /// </summary>
        public async Task<BatchSyncResult> SyncOfflineRecordsToMainSessionAsync(string courseCode, string sessionId)
        {
            try
            {
                _logger.LogInformation("Admin syncing offline records to main session. Course: {CourseCode}, Session: {SessionId}", 
                    courseCode, sessionId);

                // Get offline records for the course
                var offlineRecords = await GetOfflineRecordsByCourseCodeAsync(courseCode);
                if (!offlineRecords.Any())
                {
                    return new BatchSyncResult
                    {
                        TotalRecords = 0,
                        ProcessedSuccessfully = 0,
                        Failed = 0,
                        IndividualResults = new List<SyncResult>()
                    };
                }

                // Get main attendance session
                var mainSession = await _database.GetWithFilterAsync<SupabaseAttendanceSession>(
                    "session_id", Supabase.Postgrest.Constants.Operator.Equals, sessionId);
                
                if (!mainSession.Any())
                {
                    throw new InvalidOperationException($"Main attendance session not found: {sessionId}");
                }

                var session = mainSession.First();
                var existingRecords = session.GetAttendanceRecords();
                var syncResults = new List<SyncResult>();

                // Process each offline record directly (admin bypasses edge function validation)
                foreach (var offlineRecord in offlineRecords)
                {
                    try
                    {
                        var offlineAttendanceData = offlineRecord.GetOfflineRecords();
                        
                        foreach (var record in offlineAttendanceData)
                        {
                            // Check if record already exists
                            if (!existingRecords.Any(r => r.MatricNumber == record.MatricNumber))
                            {
                                // Convert offline record to regular attendance record
                                var attendanceRecord = new AttendanceRecord
                                {
                                    MatricNumber = record.MatricNumber,
                                    HasScannedAttendance = record.HasScannedAttendance,
                                    ScanTime = record.ScanTime,
                                    IsOnlineScan = false, // It's from offline
                                    DeviceGUID = record.DeviceGUID
                                };
                                
                                existingRecords.Add(attendanceRecord);
                                syncResults.Add(new SyncResult { Success = true, Message = "Record synced directly by admin" });
                            }
                            else
                            {
                                syncResults.Add(new SyncResult { Success = false, Message = "Duplicate record skipped" });
                            }
                        }

                        // Mark offline record as synced
                        offlineRecord.SyncStatus = (int)SyncStatus.Synced;
                        await _database.UpdateAsync(offlineRecord);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process offline record: {RecordId}", offlineRecord.Id);
                        syncResults.Add(new SyncResult { Success = false, Message = ex.Message });
                    }
                }

                // Update main session with merged records (direct database update)
                session.SetAttendanceRecords(existingRecords);
                session.UpdatedAt = DateTime.UtcNow;
                await _database.UpdateAsync(session);

                var successful = syncResults.Count(r => r.Success);
                _logger.LogInformation("Admin offline sync to main session completed: {Successful}/{Total}", 
                    successful, syncResults.Count);

                return new BatchSyncResult
                {
                    TotalRecords = syncResults.Count,
                    ProcessedSuccessfully = successful,
                    Failed = syncResults.Count - successful,
                    IndividualResults = syncResults
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin failed to sync offline records to main session");
                throw;
            }
        }

        #endregion
    }

    // Additional model classes for edge function responses
    public class EdgeFunctionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public SessionDataResponse? SessionData { get; set; }
        public ProcessedAttendanceResponse? ProcessedAttendance { get; set; }
    }

    public class EdgeFunctionErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool CanRetry { get; set; }
    }

    public class SessionDataResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsOfflineMode { get; set; }
    }

    public class ProcessedAttendanceResponse
    {
        public string MatricNumber { get; set; } = string.Empty;
        public DateTime ScannedAt { get; set; }
        public bool IsOfflineRecord { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
    }
}