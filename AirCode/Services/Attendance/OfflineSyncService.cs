using AirCode.Domain.Entities;
using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using System.Text.Json;
using AirCode.Domain.Enums;
using AttendanceRecord = AirCode.Models.Supabase.AttendanceRecord;
using OfflineRecord = AirCode.Models.Supabase.OfflineAttendanceRecord;

namespace AirCode.Services.Attendance
{
    /// <summary>
    /// Admin-side offline sync service for managing offline attendance sessions
    /// Creates offline sessions, syncs to Firebase, handles admin database operations and stuff
    /// </summary>
    public class OfflineSyncService : IOfflineSyncService
    {
        private readonly ISupabaseDatabase _database;
        private readonly IFirestoreAttendanceService _firestoreService;
        private readonly ConnectivityService _connectivityService;
        private readonly ILogger<OfflineSyncService> _logger;

        private const int MAX_RETRY_COUNT = 3;

        public OfflineSyncService(
            ISupabaseDatabase database,
            IFirestoreAttendanceService firestoreService,
            ConnectivityService connectivityService,
            ILogger<OfflineSyncService> logger)
        {
            _database = database;
            _firestoreService = firestoreService;
            _connectivityService = connectivityService;
            _logger = logger;
        }

        #region Core Admin Sync Methods

        public async Task<bool> SyncPendingRecordsAsync()
        {
            if (!_connectivityService.IsOnline)
            {
                _logger.LogWarning("Cannot sync - offline");
                return false;
            }

            try
            {
                var pendingSessions = await GetPendingOfflineSessionsAsync();
                var allSuccessful = true;

                foreach (var session in pendingSessions)
                {
                    var result = await SyncOfflineSessionAsync(session);
                    if (!result.Success)
                    {
                        allSuccessful = false;
                    }
                }

                return allSuccessful;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync pending records");
                return false;
            }
        }

        public async Task<SyncResult> ProcessOfflineAttendanceAsync(OfflineAttendanceRecordModel recordModel)
        {
            // This method is for client use - redirect to admin methods
            _logger.LogWarning("ProcessOfflineAttendanceAsync called on admin service - use client service instead");
            return new SyncResult
            {
                Success = false,
                Message = "Use client service for individual attendance processing",
                ErrorCode = "WRONG_SERVICE"
            };
        }

        public async Task<SyncResult> SyncOfflineSessionAsync(OfflineSessionData session)
        {
            if (!_connectivityService.IsOnline)
            {
                return new SyncResult
                {
                    Success = false,
                    Message = "Cannot sync while offline",
                    ErrorCode = "OFFLINE"
                };
            }

            try
            {
                // Update Supabase offline session status
                var offlineSession = new SupabaseOfflineAttendanceSession
                {
                    SessionId = session.SessionId,
                    CourseCode = session.SessionDetails.CourseCode,
                    StartTime = session.SessionDetails.StartTime,
                    Duration = session.SessionDetails.Duration,
                    ExpirationTime = session.SessionDetails.EndTime,
                    CreatedAt = session.CreatedAt,
                    OfflineRecords = JsonSerializer.Serialize(session.PendingAttendanceRecords),
                    SyncStatus = (int)SyncStatus.Synced,
                    AllowOfflineSync = session.SessionDetails.OfflineSyncEnabled,
                    SecurityFeatures = (int)session.SessionDetails.SecurityFeatures
                };

                await _database.UpdateAsync(offlineSession);

                // Sync to Firebase if records exist
                if (session.PendingAttendanceRecords.Any())
                {
                    var offlineRecords = session.PendingAttendanceRecords
                        .Select<OfflineAttendanceRecordModel, OfflineRecord>(r => new OfflineRecord
                        {
                            MatricNumber = r.MatricNumber,
                            HasScannedAttendance = true,
                            ScanTime = r.ScannedAt,
                            DeviceGUID = r.DeviceGuid,
                            SyncStatus = (int)SyncStatus.Synced
                        }).ToList();

                    await _firestoreService.UpdateOfflineAttendanceRecordsAsync(
                        session.SessionId, session.SessionDetails.CourseCode, offlineRecords);
                }

                return new SyncResult
                {
                    Success = true,
                    Message = "Offline session synced successfully",
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
                    ErrorCode = "SYNC_ERROR"
                };
            }
        }

        public async Task SchedulePeriodicSync()
        {
            var timer = new Timer(async _ =>
            {
                if (_connectivityService.IsOnline)
                {
                    await SyncPendingRecordsAsync();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));

            _logger.LogInformation("Scheduled periodic admin sync every 10 minutes");
        }

        #endregion

        #region Client Methods (Not Implemented - Use Client Service)

        public async Task<bool> StoreOfflineAttendanceAsync(string encryptedQRPayload, string deviceId = null)
        {
            _logger.LogWarning("Client method called on admin service");
            throw new InvalidOperationException("Use OfflineAttendanceClientService for client operations");
        }

        public async Task<bool> HasPendingOfflineRecordsAsync()
        {
            _logger.LogWarning("Client method called on admin service");
            throw new InvalidOperationException("Use OfflineAttendanceClientService for client operations");
        }

        public async Task<int> GetPendingOfflineRecordsCountAsync()
        {
            _logger.LogWarning("Client method called on admin service");
            throw new InvalidOperationException("Use OfflineAttendanceClientService for client operations");
        }

        public async Task<bool> StoreMatricNumberAsync(string matricNumber)
        {
            _logger.LogWarning("Client method called on admin service");
            throw new InvalidOperationException("Use OfflineAttendanceClientService for client operations");
        }

        public async Task<bool> ClearAllOfflineRecordsAsync()
        {
            _logger.LogWarning("Client method called on admin service");
            throw new InvalidOperationException("Use OfflineAttendanceClientService for client operations");
        }

        #endregion

        #region Admin Direct Database Methods

        public async Task<bool> CreateOfflineSessionRecordAsync(SessionData sessionData)
        {
            if (!_connectivityService.IsOnline)
            {
                _logger.LogWarning("Cannot create offline session - offline");
                return false;
            }

            try
            {
                if (!sessionData.OfflineSyncEnabled)
                {
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
                    OfflineRecords = "[]",
                    SyncStatus = (int)SyncStatus.Pending,
                    AllowOfflineSync = true,
                    SecurityFeatures = (int)sessionData.SecurityFeatures
                };

                await _database.InsertAsync(offlineSession);
                _logger.LogInformation("Created offline session record: {SessionId}", sessionData.SessionId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create offline session record");
                return false;
            }
        }

        public async Task<BatchSyncResult> SyncOfflineRecordsToMainSessionAsync(string courseCode, string sessionId)
        {
            if (!_connectivityService.IsOnline)
            {
                throw new InvalidOperationException("Cannot sync while offline");
            }

            try
            {
                var offlineRecords = await GetOfflineRecordsByCourseCodeAsync(courseCode);
                var syncResults = new List<SyncResult>();

                // Get main session
                var mainSessions = await _database.GetWithFilterAsync<SupabaseAttendanceSession>(
                    "session_id", Supabase.Postgrest.Constants.Operator.Equals, sessionId);

                if (!mainSessions.Any())
                {
                    throw new InvalidOperationException($"Main session not found: {sessionId}");
                }

                var session = mainSessions.First();
                var existingRecords = session.GetAttendanceRecords();

                foreach (var offlineRecord in offlineRecords)
                {
                    var offlineAttendanceData = offlineRecord.GetOfflineRecords();

                    foreach (var record in offlineAttendanceData)
                    {
                        if (!existingRecords.Any(r => r.MatricNumber == record.MatricNumber))
                        {
                            var attendanceRecord = new AttendanceRecord
                            {
                                MatricNumber = record.MatricNumber,
                                HasScannedAttendance = record.HasScannedAttendance,
                                ScanTime = record.ScanTime,
                                IsOnlineScan = false,
                                DeviceGUID = record.DeviceGUID
                            };

                            existingRecords.Add(attendanceRecord);
                            syncResults.Add(new SyncResult { Success = true });
                        }
                        else
                        {
                            syncResults.Add(new SyncResult { Success = false, Message = "Duplicate" });
                        }
                    }

                    offlineRecord.SyncStatus = (int)SyncStatus.Synced;
                    await _database.UpdateAsync(offlineRecord);
                }

                session.SetAttendanceRecords(existingRecords);
                session.UpdatedAt = DateTime.UtcNow;
                await _database.UpdateAsync(session);

                // Sync to Firebase
                await _firestoreService.UpdateAttendanceRecordsAsync(sessionId, courseCode, existingRecords);

                var successful = syncResults.Count(r => r.Success);

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

        public async Task<bool> DirectInsertAttendanceRecordAsync(string sessionId, AttendanceRecord attendanceRecord)
        {
            if (!_connectivityService.IsOnline)
            {
                return false;
            }

            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseAttendanceSession>(
                    "session_id", Supabase.Postgrest.Constants.Operator.Equals, sessionId);

                if (!sessions.Any()) return false;

                var session = sessions.First();
                var records = session.GetAttendanceRecords();

                if (records.Any(r => r.MatricNumber == attendanceRecord.MatricNumber))
                {
                    return false; // Duplicate
                }

                records.Add(attendanceRecord);
                session.SetAttendanceRecords(records);
                await _database.UpdateAsync(session);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to directly insert attendance record");
                return false;
            }
        }

        public async Task<List<OfflineSessionData>> GetOfflineSessionsByCourseAsync(string courseCode)
        {
            try
            {
                var offlineSessions = await _database.GetWithFilterAsync<SupabaseOfflineAttendanceSession>(
                    "course_code", Supabase.Postgrest.Constants.Operator.Equals, courseCode);

                return offlineSessions.Select(ConvertToOfflineSessionData).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline sessions");
                return new List<OfflineSessionData>();
            }
        }

        public async Task<int> ArchiveOldOfflineSessionsAsync(int olderThanDays = 30)
        {
            if (!_connectivityService.IsOnline)
            {
                return 0;
            }

            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                var oldSessions = await RetrieveOfflineSessionsAsync();

                var sessionsToArchive = oldSessions
                    .Where(s => s.CreatedAt < cutoffDate && s.SyncStatus == (int)SyncStatus.Synced)
                    .ToList();

                int archivedCount = 0;

                foreach (var session in sessionsToArchive)
                {
                    try
                    {
                        var archivedData = new ArchivedAttendanceData
                        {
                            CourseCode = session.CourseCode,
                            ArchivedData = JsonSerializer.Serialize(session),
                            ArchivedAt = DateTime.UtcNow,
                            DataType = "offline_sessions"
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

                return archivedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive old sessions");
                return 0;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<OfflineSessionData>> GetPendingOfflineSessionsAsync()
        {
            try
            {
                var allSessions = await RetrieveOfflineSessionsAsync();
                var pendingSessions = allSessions.Where(s => s.SyncStatus == (int)SyncStatus.Pending).ToList();

                return pendingSessions.Select(ConvertToOfflineSessionData).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending offline sessions");
                return new List<OfflineSessionData>();
            }
        }

        private async Task<List<SupabaseOfflineAttendanceSession>> GetOfflineRecordsByCourseCodeAsync(string courseCode)
        {
            try
            {
                return await _database.GetWithFilterAsync<SupabaseOfflineAttendanceSession>(
                    "course_code", Supabase.Postgrest.Constants.Operator.Equals, courseCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline records for course: {CourseCode}", courseCode);
                return new List<SupabaseOfflineAttendanceSession>();
            }
        }

        private static OfflineSessionData ConvertToOfflineSessionData(SupabaseOfflineAttendanceSession session)
        {
            return new OfflineSessionData
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
                    SecurityFeatures = (AdvancedSecurityFeatures)session.SecurityFeatures,
                    OfflineSyncEnabled = session.AllowOfflineSync
                },
                PendingAttendanceRecords = JsonSerializer.Deserialize<List<OfflineAttendanceRecordModel>>(
                    session.OfflineRecords) ?? new List<OfflineAttendanceRecordModel>(),
                SyncStatus = (SyncStatus)session.SyncStatus
            };
        }

        #endregion
        #region Helpers
        public async Task<List<SupabaseOfflineAttendanceSession>> RetrieveOfflineSessionsAsync()
        {
            try
            {
                // Ensure the database is initialized and retrieve all offline attendance sessions
                var offlineSessions = await _database.GetAllAsync<SupabaseOfflineAttendanceSession>();
                return offlineSessions.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve offline attendance sessions");
                return new List<SupabaseOfflineAttendanceSession>();
            }
        }
        public async Task<List<SupabaseOfflineAttendanceSession>> RetrieveSessionsByCourseCodeAsync(string courseCode)
        {
            try
            {
                // Retrieve sessions filtered by course code
                var filteredSessions = await _database.GetWithFilterAsync<SupabaseOfflineAttendanceSession>(
                    "course_code", Supabase.Postgrest.Constants.Operator.Equals, courseCode);
                return filteredSessions.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve sessions for course code: {CourseCode}", courseCode);
                return new List<SupabaseOfflineAttendanceSession>();
            }
        }
        
        #endregion
        
    }
}