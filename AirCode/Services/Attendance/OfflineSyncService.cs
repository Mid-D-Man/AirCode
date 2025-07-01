using AirCode.Domain.Entities;
using AirCode.Models.QRCode;
using AirCode.Models.Supabase;
using AirCode.Services.Storage;
using AirCode.Services.SupaBase;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AttendanceRecord = AirCode.Models.Supabase.AttendanceRecord;



namespace AirCode.Services.Attendance
{
    /// <summary>
    /// offline sync service for offline handling, needs a bit of work regarding movement to firebase though
    /// so make the firestore attendance service first then use it here and handle after propper sync, when session about to start
    /// cheack for offline_attendance_records if they exist and for a the course code we attendancing move it there then delete the row
    /// </summary>
    public class OfflineSyncService : IOfflineSyncService
    {
        private readonly ISupabaseDatabase _database;
        private readonly ISupabaseEdgeFunctionService _edgeService;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly ILogger<OfflineSyncService> _logger;
        
        private const string OFFLINE_RECORDS_KEY = "offline_attendance_records";
        private const string OFFLINE_SESSIONS_KEY = "offline_session_data";
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

        #region Public Interface Methods

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
                
                foreach (var record in pendingRecords)
                {
                    var result = await ProcessOfflineAttendanceAsync(record);
                    syncResults.Add(result);
                    
                    // Update record status based on sync result
                    await UpdateOfflineRecordStatusAsync(record, result);
                }

                // Clean up successfully synced records
                await CleanupSyncedRecordsAsync();
                
                var successfulSyncs = syncResults.Count(r => r.Success);
                _logger.LogInformation("Sync completed: {Successful}/{Total} records synced successfully", 
                    successfulSyncs, syncResults.Count);
                
                return syncResults.All(r => r.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync pending offline records");
                return false;
            }
        }

        public async Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecord record)
        {
            try
            {
                _logger.LogDebug("Processing offline attendance record: {RecordId}", record.Id);
                
                // Check if corresponding offline session exists in Supabase
                var offlineSession = await GetOfflineSessionFromSupabaseAsync(record.EncryptedQrPayload);
                if (offlineSession == null)
                {
                    return new SyncResult
                    {
                        Success = false,
                        Message = "No matching offline session found in database. Session may not be created yet.",
                        ErrorCode = "SESSION_NOT_FOUND",
                        RetryAttempt = record.RetryCount
                    };
                }

                // Create attendance record from offline data
                var attendanceRecord = new AttendanceRecord
                {
                    MatricNumber = await GetMatricNumberFromDeviceAsync(record.DeviceId),
                    HasScannedAttendance = true,
                    IsOnlineScan = false,
                    ScanTime = record.ScannedAt,
                    DeviceGUID = record.DeviceId
                };

                // Create edge function request
                var edgeRequest = await CreateOfflineEdgeFunctionRequestAsync(record.EncryptedQrPayload, attendanceRecord);
                
                // Process through edge function
                var result = await _edgeService.ProcessAttendanceWithPayloadAsync(edgeRequest);
                
                return new SyncResult
                {
                    Success = result.Success,
                    Message = result.Message,
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
                    ErrorCode = "PROCESSING_ERROR",
                    RetryAttempt = record.RetryCount
                };
            }
        }

        public async Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session)
        {
            try
            {
                _logger.LogDebug("Syncing offline session: {SessionId}", session.SessionId);
                
                // Create offline attendance session record in Supabase
                var offlineSession = new OfflineAttendanceSession
                {
                    SessionId = session.SessionId,
                    CourseCode = session.SessionDetails.CourseId,
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

        #region Admin-Specific Methods

        /// <summary>
        /// Admin method to create offline session records in Supabase when online
        /// </summary>
        public async Task<bool> CreateOfflineSessionRecordAsync(ActiveSessionData sessionData)
        {
            try
            {
                if (!sessionData.OfflineSyncEnabled)
                {
                    _logger.LogDebug("Offline sync not enabled for session: {SessionId}", sessionData.SessionId);
                    return false;
                }

                var offlineSession = new OfflineAttendanceSession
                {
                    SessionId = sessionData.SessionId,
                    CourseCode = sessionData.CourseId,
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
                _logger.LogInformation("Created offline session record: {SessionId}", sessionData.SessionId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create offline session record: {SessionId}", sessionData.SessionId);
                return false;
            }
        }

        /// <summary>
        /// Admin method to sync offline records to main attendance session
        /// </summary>
        public async Task<BatchSyncResult> SyncOfflineRecordsToMainSessionAsync(string courseCode, string sessionId)
        {
            try
            {
                _logger.LogInformation("Syncing offline records to main session. Course: {CourseCode}, Session: {SessionId}", 
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
                var mainSession = await _database.GetWithFilterAsync<AttendanceSession>(
                    "session_id", Supabase.Postgrest.Constants.Operator.Equals, sessionId);
                
                if (!mainSession.Any())
                {
                    throw new InvalidOperationException($"Main attendance session not found: {sessionId}");
                }

                var session = mainSession.First();
                var existingRecords = session.GetAttendanceRecords();
                var syncResults = new List<SyncResult>();

                // Process each offline record
                foreach (var offlineRecord in offlineRecords)
                {
                    try
                    {
                        var attendanceData = JsonSerializer.Deserialize<List<AttendanceRecord>>(offlineRecord.OfflineRecords);
                        
                        foreach (var record in attendanceData ?? new List<AttendanceRecord>())
                        {
                            // Check if record already exists
                            if (!existingRecords.Any(r => r.MatricNumber == record.MatricNumber))
                            {
                                existingRecords.Add(record);
                                syncResults.Add(new SyncResult { Success = true, Message = "Record synced" });
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

                // Update main session with merged records
                session.SetAttendanceRecords(existingRecords);
                await _database.UpdateAsync(session);

                var successful = syncResults.Count(r => r.Success);
                _logger.LogInformation("Offline sync to main session completed: {Successful}/{Total}", 
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
                _logger.LogError(ex, "Failed to sync offline records to main session");
                throw;
            }
        }

        #endregion

        #region Client-Specific Methods

        /// <summary>
        /// Store offline attendance record locally on client device
        /// </summary>
        public async Task<bool> StoreOfflineAttendanceAsync(string encryptedQRPayload, string deviceId)
        {
            try
            {
                var offlineRecord = new OfflineAttendanceRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    EncryptedQrPayload = encryptedQRPayload,
                    ScannedAt = DateTime.UtcNow,
                    DeviceId = deviceId,
                    Status = SyncStatus.Pending,
                    RetryCount = 0
                };

                var existingRecords = await GetPendingOfflineRecordsAsync();
                existingRecords.Add(offlineRecord);
                
                await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, existingRecords);
                
                _logger.LogInformation("Stored offline attendance record: {RecordId}", offlineRecord.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store offline attendance record");
                return false;
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

        #endregion

        #region Private Helper Methods

        private async Task<List<OfflineAttendanceRecord>> GetPendingOfflineRecordsAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<List<OfflineAttendanceRecord>>(OFFLINE_RECORDS_KEY) 
                       ?? new List<OfflineAttendanceRecord>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending offline records");
                return new List<OfflineAttendanceRecord>();
            }
        }

        private async Task<OfflineAttendanceSession?> GetOfflineSessionFromSupabaseAsync(string encryptedQRPayload)
        {
            try
            {
                // Extract session ID from QR payload (implement your QR decoder logic)
                var sessionId = await ExtractSessionIdFromQRPayloadAsync(encryptedQRPayload);
                if (string.IsNullOrEmpty(sessionId))
                    return null;

                var sessions = await _database.GetWithFilterAsync<OfflineAttendanceSession>(
                    "session_id", Supabase.Postgrest.Constants.Operator.Equals, sessionId);
                
                return sessions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline session from Supabase");
                return null;
            }
        }

        private async Task<List<OfflineAttendanceSession>> GetOfflineRecordsByCourseCodeAsync(string courseCode)
        {
            try
            {
                return await _database.GetWithFilterAsync<OfflineAttendanceSession>(
                    "course_code", Supabase.Postgrest.Constants.Operator.Equals, courseCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline records by course code");
                return new List<OfflineAttendanceSession>();
            }
        }

        private async Task<EdgeFunctionRequest> CreateOfflineEdgeFunctionRequestAsync(string encryptedQRPayload, AttendanceRecord attendanceRecord)
        {
            // Implement QR payload decoding and edge function request creation
            // This should use your existing QR decoder utilities
            var payloadData = await DecodeQRPayloadAsync(encryptedQRPayload);
            
            return new EdgeFunctionRequest
            {
                QrCodePayload = payloadData,
                AttendanceData = attendanceRecord,
                PayloadSignature = await GeneratePayloadSignatureAsync(payloadData)
            };
        }

        private async Task<string> GetMatricNumberFromDeviceAsync(string deviceId)
        {
            // Implement device-to-matricnumber mapping
            // This should retrieve the matric number associated with the device
            return await _localStorage.GetItemAsync<string>($"matric_number_{deviceId}") ?? string.Empty;
        }

        private async Task UpdateOfflineRecordStatusAsync(OfflineAttendanceRecord record, SyncResult result)
        {
            try
            {
                if (result.Success)
                {
                    record.Status = SyncStatus.Synced;
                    record.ErrorDetails = string.Empty;
                }
                else
                {
                    record.RetryCount++;
                    record.ErrorDetails = result.Message;
                    record.Status = record.RetryCount >= MAX_RETRY_COUNT ? SyncStatus.Failed : SyncStatus.Pending;
                }

                var allRecords = await GetPendingOfflineRecordsAsync();
                var existingRecord = allRecords.FirstOrDefault(r => r.Id == record.Id);
                if (existingRecord != null)
                {
                    var index = allRecords.IndexOf(existingRecord);
                    allRecords[index] = record;
                    await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, allRecords);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update offline record status: {RecordId}", record.Id);
            }
        }

        private async Task CleanupSyncedRecordsAsync()
        {
            try
            {
                var allRecords = await GetPendingOfflineRecordsAsync();
                var pendingRecords = allRecords.Where(r => r.Status != SyncStatus.Synced).ToList();
                
                await _localStorage.SetItemAsync(OFFLINE_RECORDS_KEY, pendingRecords);
                
                var cleanedCount = allRecords.Count - pendingRecords.Count;
                if (cleanedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} synced offline records", cleanedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup synced records");
            }
        }

        // Placeholder methods for QR decoding - implement based on your existing utilities
        private async Task<string> ExtractSessionIdFromQRPayloadAsync(string encryptedQRPayload)
        {
            // Implement using your existing QR decoder
            throw new NotImplementedException("Implement QR session ID extraction");
        }

        private async Task<QRCodePayloadData> DecodeQRPayloadAsync(string encryptedQRPayload)
        {
            // Implement using your existing QR decoder
            throw new NotImplementedException("Implement QR payload decoding");
        }

        private async Task<string> GeneratePayloadSignatureAsync(QRCodePayloadData payloadData)
        {
            // Implement HMAC signature generation
            throw new NotImplementedException("Implement payload signature generation");
        }

        #endregion
    }

    // Additional model for offline attendance sessions table
    [Supabase.Postgrest.Attributes.Table("offline_attendance_sessions")]
    public class OfflineAttendanceSession : Supabase.Postgrest.Models.BaseModel
    {
        [Supabase.Postgrest.Attributes.PrimaryKey("id")]
        public long Id { get; set; }
        
        [Supabase.Postgrest.Attributes.Column("session_id")]
        public string SessionId { get; set; } = string.Empty;
        
        [Supabase.Postgrest.Attributes.Column("course_code")]
        public string CourseCode { get; set; } = string.Empty;
        
        [Supabase.Postgrest.Attributes.Column("start_time")]
        public DateTime StartTime { get; set; }
        
        [Supabase.Postgrest.Attributes.Column("duration")]
        public int Duration { get; set; }
        
        [Supabase.Postgrest.Attributes.Column("expiration_time")]
        public DateTime ExpirationTime { get; set; }
        
        [Supabase.Postgrest.Attributes.Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Supabase.Postgrest.Attributes.Column("offline_records")]
        public string OfflineRecords { get; set; } = "[]";
        
        [Supabase.Postgrest.Attributes.Column("sync_status")]
        public int SyncStatus { get; set; } = 0;
        
        [Supabase.Postgrest.Attributes.Column("allow_offline_sync")]
        public bool AllowOfflineSync { get; set; } = true;
        
        [Supabase.Postgrest.Attributes.Column("security_features")]
        public int SecurityFeatures { get; set; } = 0;
    }
}