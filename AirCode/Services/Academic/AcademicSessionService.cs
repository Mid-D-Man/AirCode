using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Firebase;
using AirCode.Services.Storage;
using AirCode.Services.Time;
using AirCode.Models.Supabase;
using AirCode.Services.Courses;
using AirCode.Services.SupaBase;

namespace AirCode.Services.Academic
{
    public class AcademicSessionService : IAcademicSessionService
    {
        private readonly IFirestoreService _firestoreService;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly IServerTimeService _serverTimeService;
        private readonly ILogger<AcademicSessionService> _logger;
        private readonly ISupabaseDatabase _supabaseDatabase;
        private readonly ICourseService _courseService;

        // Constants
        private const string ACADEMIC_SESSIONS_COLLECTION = "ACADEMIC_SESSIONS";
        private const string TRANSITION_LOG_COLLECTION = "TRANSITION_LOGS";
        private const string USER_LOGIN_COLLECTION = "USER_LOGINS";
        private const string SESSION_CACHE_KEY = "cached_sessions";
        private const string LAST_TRANSITION_CHECK_KEY = "last_transition_check";
        private const int MAX_CACHE_AGE_HOURS = 1;

        // Cache en
        private List<AcademicSession> _cachedSessions;
        private DateTime _lastCacheUpdate;
        private readonly object _cacheLock = new object();

        // Events
        public event Func<SessionEndEvent, Task> SessionEnded;
        public event Func<SemesterEndEvent, Task> SemesterEnded;
        public event Func<SessionStartEvent, Task> SessionStarted;
        public event Func<SemesterStartEvent, Task> SemesterStarted;

       
        public AcademicSessionService(
            IFirestoreService firestoreService,
            IBlazorAppLocalStorageService localStorage,
            IServerTimeService serverTimeService,
            ISupabaseDatabase supabaseDatabase,
            ICourseService courseService,
            ILogger<AcademicSessionService> logger)
        {
            _firestoreService = firestoreService;
            _localStorage = localStorage;
            _serverTimeService = serverTimeService;
            _supabaseDatabase = supabaseDatabase;
            _courseService = courseService;
            _logger = logger;
        }

        #region Session Management

        public async Task<SessionTransitionResult> ProcessPendingTransitionsAsync(string userId)
        {
            var result = new SessionTransitionResult
            {
                ProcessedAt = await _serverTimeService.GetCurrentServerTimeAsync(),
                ProcessedBy = userId
            };

            try
            {
                _logger.LogInformation($"Processing pending transitions for user: {userId}");

                // Validate system state first
                var systemValidation = await ValidateSystemStateAsync();
                if (!systemValidation.IsValid)
                {
                    result.Errors.AddRange(systemValidation.Issues);
                    return result;
                }

                // Get last login time to determine how far back to check
                var lastLoginTime = await GetLastLoginTimeAsync(userId);
                var checkFromDate = lastLoginTime ?? DateTime.MinValue;
                
                _logger.LogInformation($"Checking transitions since: {checkFromDate}");

                // Refresh session cache
                await RefreshSessionCacheAsync();

                // Check for ended sessions and semesters
                var endedSessions = await CheckForEndedSessionsSince(checkFromDate);
                var endedSemesters = await CheckForEndedSemestersSince(checkFromDate);

                // Check for started sessions and semesters
                var startedSessions = await CheckForStartedSessionsSince(checkFromDate);
                var startedSemesters = await CheckForStartedSemestersSince(checkFromDate);

                // Process ended sessions
                foreach (var sessionEndEvent in endedSessions)
                {
                    if (await ShouldProcessSessionEndAsync(sessionEndEvent.Session))
                    {
                        await ProcessSessionEndAsync(sessionEndEvent.Session, userId);
                        result.EndedSessions.Add(sessionEndEvent);
                        SessionEnded?.Invoke(sessionEndEvent);
                    }
                }

                // Process ended semesters
                foreach (var semesterEndEvent in endedSemesters)
                {
                    if (await ShouldProcessSemesterEndAsync(semesterEndEvent.Semester))
                    {
                        await ProcessSemesterEndAsync(semesterEndEvent.Semester, userId);
                        result.EndedSemesters.Add(semesterEndEvent);
                        SemesterEnded?.Invoke(semesterEndEvent);
                    }
                }

                // Process started sessions
                foreach (var sessionStartEvent in startedSessions)
                {
                    await ProcessSessionStartAsync(sessionStartEvent.Session, userId);
                    result.StartedSessions.Add(sessionStartEvent);
                    SessionStarted?.Invoke(sessionStartEvent);
                }

                // Process started semesters
                foreach (var semesterStartEvent in startedSemesters)
                {
                    await ProcessSemesterStartAsync(semesterStartEvent.Semester, userId);
                    result.StartedSemesters.Add(semesterStartEvent);
                    SemesterStarted?.Invoke(semesterStartEvent);
                }

                // Check for overlapping sessions and resolve
                var overlapResult = await ResolveSessionOverlapAsync();
                if (overlapResult.HasOverlap)
                {
                    result.Warnings.Add($"Session overlap detected: {overlapResult.Resolution}");
                }

                // Update last login time
                await SetLastLoginTimeAsync(userId, result.ProcessedAt);

                result.HasPendingTransitions = result.EndedSessions.Any() || result.EndedSemesters.Any() || 
                                             result.StartedSessions.Any() || result.StartedSemesters.Any();

                _logger.LogInformation($"Transition processing completed. Found {result.EndedSessions.Count} ended sessions, {result.EndedSemesters.Count} ended semesters");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending transitions");
                result.Errors.Add($"Transition processing failed: {ex.Message}");
            }

            return result;
        }

        public async Task<AcademicSession> GetCurrentSessionAsync()
        {
            await RefreshSessionCacheAsync();
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();
            
            return _cachedSessions?.FirstOrDefault(s => 
                IsSessionActiveAtTime(s, currentTime) && !IsTestSession(s));
        }

        public async Task<AcademicSession> GetNextSessionAsync()
        {
            await RefreshSessionCacheAsync();
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();
            
            return _cachedSessions?
                .Where(s => GetStartDate(s) > currentTime && !IsTestSession(s))
                .OrderBy(s => GetStartDate(s))
                .FirstOrDefault();
        }

        public async Task<List<AcademicSession>> GetArchivedSessionsAsync()
        {
            await RefreshSessionCacheAsync();
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();
            
            return _cachedSessions?
                .Where(s => GetEndDate(s) < currentTime && !IsTestSession(s))
                .OrderByDescending(s => GetEndDate(s))
                .ToList() ?? new List<AcademicSession>();
        }

        public async Task RefreshSessionCacheAsync()
        {
            lock (_cacheLock)
            {
                if (_cachedSessions != null && 
                    DateTime.Now.Subtract(_lastCacheUpdate).TotalHours < MAX_CACHE_AGE_HOURS)
                {
                    return; // Cache is still fresh
                }
            }

            try
            {
                var sessions = await _firestoreService.GetCollectionAsync<AcademicSession>(ACADEMIC_SESSIONS_COLLECTION);
                
                lock (_cacheLock)
                {
                    _cachedSessions = sessions ?? new List<AcademicSession>();
                    _lastCacheUpdate = DateTime.Now;
                }

                _logger.LogInformation($"Session cache refreshed. Loaded {_cachedSessions.Count} sessions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh session cache");
                
                // Try to load from local storage as fallback
                try
                {
                    var cachedData = await _localStorage.GetItemAsync<List<AcademicSession>>(SESSION_CACHE_KEY);
                    if (cachedData != null)
                    {
                        lock (_cacheLock)
                        {
                            _cachedSessions = cachedData;
                            _lastCacheUpdate = DateTime.Now;
                        }
                        _logger.LogInformation("Loaded sessions from local storage cache");
                    }
                }
                catch
                {
                    _logger.LogWarning("Could not load sessions from any source");
                }
            }
        }

        #endregion

        #region Transition Logic

        public async Task<List<SessionEndEvent>> CheckForEndedSessionsAsync()
        {
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();
            return await CheckForEndedSessionsSince(DateTime.MinValue);
        }

        private async Task<List<SessionEndEvent>> CheckForEndedSessionsSince(DateTime sinceDate)
        {
            var endedSessions = new List<SessionEndEvent>();
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();

            if (_cachedSessions == null) return endedSessions;

            foreach (var session in _cachedSessions.Where(s => !IsTestSession(s)))
            {
                var endDate = GetEndDate(session);
                
                // Session ended after sinceDate and before current time
                if (endDate > sinceDate && endDate < currentTime)
                {
                    // Check if already processed
                    if (!await HasTransitionBeenProcessedAsync(session.SessionId, TransitionType.SessionEnd))
                    {
                        var delay = currentTime.Subtract(endDate);
                        endedSessions.Add(new SessionEndEvent
                        {
                            Session = session,
                            ActualEndDate = endDate,
                            ProcessedAt = currentTime,
                            Reason = SessionEndReason.NaturalExpiry,
                            WasDelayed = delay.TotalHours > 24, // Consider delayed if more than 24 hours
                            DelayDuration = delay
                        });
                    }
                }
            }

            return endedSessions;
        }

        public async Task<List<SemesterEndEvent>> CheckForEndedSemestersAsync()
        {
            return await CheckForEndedSemestersSince(DateTime.MinValue);
        }

        private async Task<List<SemesterEndEvent>> CheckForEndedSemestersSince(DateTime sinceDate)
        {
            var endedSemesters = new List<SemesterEndEvent>();
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();

            if (_cachedSessions == null) return endedSemesters;

            foreach (var session in _cachedSessions.Where(s => !IsTestSession(s)))
            {
                foreach (var semester in session.Semesters ?? new List<Semester>())
                {
                    // Semester ended after sinceDate and before current time
                    if (semester.EndDate > sinceDate && semester.EndDate < currentTime)
                    {
                        // Check if already processed
                        if (!await HasTransitionBeenProcessedAsync($"{session.SessionId}_{semester.SemesterId}", TransitionType.SemesterEnd))
                        {
                            var delay = currentTime.Subtract(semester.EndDate);
                            endedSemesters.Add(new SemesterEndEvent
                            {
                                Semester = semester,
                                SessionId = session.SessionId,
                                ActualEndDate = semester.EndDate,
                                ProcessedAt = currentTime,
                                Reason = SemesterEndReason.NaturalExpiry,
                                WasDelayed = delay.TotalHours > 24,
                                DelayDuration = delay
                            });
                        }
                    }
                }
            }

            return endedSemesters;
        }

        private async Task<List<SessionStartEvent>> CheckForStartedSessionsSince(DateTime sinceDate)
        {
            var startedSessions = new List<SessionStartEvent>();
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();

            if (_cachedSessions == null) return startedSessions;

            foreach (var session in _cachedSessions.Where(s => !IsTestSession(s)))
            {
                var startDate = GetStartDate(session);
                
                // Session started after sinceDate and before current time
                if (startDate > sinceDate && startDate < currentTime)
                {
                    // Check if already processed
                    if (!await HasTransitionBeenProcessedAsync(session.SessionId, TransitionType.SessionStart))
                    {
                        var delay = currentTime.Subtract(startDate);
                        startedSessions.Add(new SessionStartEvent
                        {
                            Session = session,
                            ActualStartDate = startDate,
                            ProcessedAt = currentTime,
                            Reason = SessionStartReason.ScheduledStart,
                            WasDelayed = delay.TotalHours > 24,
                            DelayDuration = delay
                        });
                    }
                }
            }

            return startedSessions;
        }

        private async Task<List<SemesterStartEvent>> CheckForStartedSemestersSince(DateTime sinceDate)
        {
            var startedSemesters = new List<SemesterStartEvent>();
            var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();

            if (_cachedSessions == null) return startedSemesters;

            foreach (var session in _cachedSessions.Where(s => !IsTestSession(s)))
            {
                foreach (var semester in session.Semesters ?? new List<Semester>())
                {
                    // Semester started after sinceDate and before current time
                    if (semester.StartDate > sinceDate && semester.StartDate < currentTime)
                    {
                        // Check if already processed
                        if (!await HasTransitionBeenProcessedAsync($"{session.SessionId}_{semester.SemesterId}", TransitionType.SemesterStart))
                        {
                            var delay = currentTime.Subtract(semester.StartDate);
                            startedSemesters.Add(new SemesterStartEvent
                            {
                                Semester = semester,
                                SessionId = session.SessionId,
                                ActualStartDate = semester.StartDate,
                                ProcessedAt = currentTime,
                                Reason = SemesterStartReason.ScheduledStart,
                                WasDelayed = delay.TotalHours > 24,
                                DelayDuration = delay
                            });
                        }
                    }
                }
            }

            return startedSemesters;
        }

       

        public async Task ProcessSemesterEndAsync(Semester endedSemester, string triggeredBy)
        {
            try
            {
                _logger.LogInformation($"Processing semester end for: {endedSemester.SemesterId}");

                // Mark transition as processed
                await MarkTransitionProcessedAsync($"{endedSemester.SessionId}_{endedSemester.SemesterId}", TransitionType.SemesterEnd, triggeredBy);

                //  Clear all student courses
                await ClearAllStudentCoursesAsync();
        
                //  Send notifications
                await SendSemesterEndNotificationsAsync(endedSemester);
        
                await NotifySemesterEndAsync(endedSemester);

                _logger.LogInformation($"Semester end processing completed for: {endedSemester.SemesterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing semester end for: {endedSemester.SemesterId}");
                throw;
            }
        }

        public async Task ProcessSessionStartAsync(AcademicSession newSession, string triggeredBy)
        {
            try
            {
                _logger.LogInformation($"Processing session start for: {newSession.SessionId}");

                // Mark transition as processed
                await MarkTransitionProcessedAsync(newSession.SessionId, TransitionType.SessionStart, triggeredBy);

                // Perform session start logic:
                // 1. Initialize session settings
                // 2. Set up enrollment periods
                // 3. Activate courses
                // 4. Send welcome notifications
                // 5. Initialize academic calendars

                await InitializeSessionSettingsAsync(newSession);
                await SetupEnrollmentPeriodsAsync(newSession);
                await NotifySessionStartAsync(newSession);

                _logger.LogInformation($"Session start processing completed for: {newSession.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing session start for: {newSession.SessionId}");
                throw;
            }
        }

        public async Task ProcessSemesterStartAsync(Semester newSemester, string triggeredBy)
        {
            try
            {
                _logger.LogInformation($"Processing semester start for: {newSemester.SemesterId}");

                // Mark transition as processed
                await MarkTransitionProcessedAsync($"{newSemester.SessionId}_{newSemester.SemesterId}", TransitionType.SemesterStart, triggeredBy);

                // Perform semester start logic:
                // 1. Open course enrollment
                // 2. Initialize gradebooks
                // 3. Set up attendance tracking
                // 4. Send semester start notifications

                await OpenCourseEnrollmentAsync(newSemester);
                await InitializeGradebooksAsync(newSemester);
                await NotifySemesterStartAsync(newSemester);

                _logger.LogInformation($"Semester start processing completed for: {newSemester.SemesterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing semester start for: {newSemester.SemesterId}");
                throw;
            }
        }

        
        public async Task ProcessSessionEndAsync(AcademicSession endedSession, string triggeredBy)
        {
            try
            {
                _logger.LogInformation($"Processing session end for: {endedSession.SessionId}");

                // Mark transition as processed first to prevent duplicate processing
                await MarkTransitionProcessedAsync(endedSession.SessionId, TransitionType.SessionEnd, triggeredBy);

                // Existing session end logic
                await ArchiveSessionDataAsync(endedSession);
                await GenerateSessionReportsAsync(endedSession);
        
                // ADD: Process student level promotions
                await ProcessStudentLevelPromotionsAsync(endedSession);
        
                await NotifySessionEndAsync(endedSession);

                _logger.LogInformation($"Session end processing completed for: {endedSession.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing session end for: {endedSession.SessionId}");
                throw;
            }
        }
        #endregion

        #region Validation & Edge Cases

        public async Task<bool> ShouldProcessSessionEndAsync(AcademicSession session)
        {
            // Don't process if:
            // 1. It's a test session
            // 2. It was manually deleted/terminated
            // 3. It has already been processed
            // 4. It's not a valid session

            if (IsTestSession(session))
            {
                _logger.LogDebug($"Skipping session end processing - test session: {session.SessionId}");
                return false;
            }

            if (await HasTransitionBeenProcessedAsync(session.SessionId, TransitionType.SessionEnd))
            {
                _logger.LogDebug($"Skipping session end processing - already processed: {session.SessionId}");
                return false;
            }

            if (IsDeletedOrInvalidSession(session))
            {
                _logger.LogDebug($"Skipping session end processing - deleted/invalid session: {session.SessionId}");
                return false;
            }

            return true;
        }

        private async Task<bool> ShouldProcessSemesterEndAsync(Semester semester)
        {
            // Similar validation for semesters
            if (await HasTransitionBeenProcessedAsync($"{semester.SessionId}_{semester.SemesterId}", TransitionType.SemesterEnd))
            {
                return false;
            }

            // Additional semester-specific validations
            return true;
        }

        public async Task<bool> HasTransitionBeenProcessedAsync(string sessionId, TransitionType type)
        {
            try
            {
                var transitionLog = await _firestoreService.GetDocumentAsync<TransitionLog>(
                    TRANSITION_LOG_COLLECTION, 
                    $"{sessionId}_{type}");
                
                return transitionLog != null;
            }
            catch
            {
                return false; // If we can't check, assume not processed
            }
        }

        public async Task MarkTransitionProcessedAsync(string sessionId, TransitionType type, string processedBy)
        {
            try
            {
                var transitionLog = new TransitionLog
                {
                    Id = $"{sessionId}_{type}",
                    SessionId = sessionId,
                    TransitionType = type,
                    ProcessedAt = await _serverTimeService.GetCurrentServerTimeAsync(),
                    ProcessedBy = processedBy,
                    Status = "Completed"
                };

                await _firestoreService.AddDocumentAsync(
                    TRANSITION_LOG_COLLECTION, 
                    transitionLog, 
                    transitionLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to mark transition as processed: {sessionId}_{type}");
            }
        }

        public async Task<SessionOverlapResult> ResolveSessionOverlapAsync()
        {
            var result = new SessionOverlapResult();
            
            if (_cachedSessions == null || _cachedSessions.Count < 2)
            {
                return result;
            }

            // Check for overlapping sessions
            var activeSessions = _cachedSessions
                .Where(s => !IsTestSession(s))
                .Where(s => !IsDeletedOrInvalidSession(s))
                .ToList();

            foreach (var session1 in activeSessions)
            {
                foreach (var session2 in activeSessions.Where(s => s.SessionId != session1.SessionId))
                {
                    if (SessionsOverlap(session1, session2))
                    {
                        result.HasOverlap = true;
                        result.OverlappingSession = session1;
                        result.NewSession = session2;
                        result.RecommendedAction = DetermineOverlapResolution(session1, session2);
                        result.Resolution = $"Sessions {session1.SessionId} and {session2.SessionId} overlap";
                        
                        // For now, just log the overlap - implement resolution logic as needed
                        _logger.LogWarning($"Session overlap detected: {result.Resolution}");
                        break;
                    }
                }
                if (result.HasOverlap) break;
            }

            return result;
        }

        public async Task CleanupInvalidSessionsAsync()
        {
            try
            {
                if (_cachedSessions == null) return;

                var invalidSessions = _cachedSessions
                    .Where(s => IsDeletedOrInvalidSession(s))
                    .ToList();

                foreach (var invalidSession in invalidSessions)
                {
                    _logger.LogInformation($"Cleaning up invalid session: {invalidSession.SessionId}");
                    // Implement cleanup logic as needed
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }

        #endregion

        #region Utilities

        public async Task<DateTime?> GetLastLoginTimeAsync(string userId)
        {
            try
            {
                var loginRecord = await _firestoreService.GetDocumentAsync<UserLoginRecord>(
                    USER_LOGIN_COLLECTION, 
                    userId);
                
                return loginRecord?.LastLoginTime;
            }
            catch
            {
                return null;
            }
        }

        public async Task SetLastLoginTimeAsync(string userId, DateTime loginTime)
        {
            try
            {
                var loginRecord = new UserLoginRecord
                {
                    UserId = userId,
                    LastLoginTime = loginTime,
                    UpdatedAt = loginTime
                };

                await _firestoreService.AddDocumentAsync(
                    USER_LOGIN_COLLECTION, 
                    loginRecord, 
                    userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update last login time for user: {userId}");
            }
        }

        public async Task<SystemValidationResult> ValidateSystemStateAsync()
        {
            var result = new SystemValidationResult
            {
                IsValid = true,
                HealthStatus = SystemHealthStatus.Healthy
            };

            try
            {
                // Check if Firestore is connected
                if (!await _firestoreService.IsConnectedAsync())
                {
                    result.Issues.Add("Database connection is not available");
                    result.IsValid = false;
                    result.HealthStatus = SystemHealthStatus.Error;
                }

                // Check if server time is synced
                var serverTime = await _serverTimeService.GetCurrentServerTimeAsync();
                var localTime = DateTime.UtcNow;
                var timeDifference = Math.Abs((serverTime - localTime).TotalMinutes);
                
                if (timeDifference > 5) // More than 5 minutes difference
                {
                    result.Warnings.Add($"Server time sync difference: {timeDifference:F1} minutes");
                    result.HealthStatus = SystemHealthStatus.Warning;
                }

                // Validate session cache
                if (_cachedSessions == null)
                {
                    result.Warnings.Add("Session cache is not initialized");
                    result.HealthStatus = SystemHealthStatus.Warning;
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"System validation failed: {ex.Message}");
                result.IsValid = false;
                result.HealthStatus = SystemHealthStatus.Critical;
            }

            return result;
        }

        public async Task<SessionHealthReport> GetSessionHealthReportAsync()
        {
            await RefreshSessionCacheAsync();
            
            var report = new SessionHealthReport
            {
                LastChecked = DateTime.UtcNow,
                OverallHealth = SystemHealthStatus.Healthy
            };

            if (_cachedSessions != null)
            {
                var currentTime = await _serverTimeService.GetCurrentServerTimeAsync();
                
                report.TotalSessions = _cachedSessions.Count;
                report.ActiveSessions = _cachedSessions.Count(s => IsSessionActiveAtTime(s, currentTime));
                report.ArchivedSessions = _cachedSessions.Count(s => GetEndDate(s) < currentTime);
                report.PendingSessions = _cachedSessions.Count(s => GetStartDate(s) > currentTime);

                // Check for issues
                var issues = new List<SessionIssue>();
                
                // Check for overlapping sessions
                foreach (var session in _cachedSessions.Where(s => !IsTestSession(s)))
                {
                    var overlapping = _cachedSessions
                        .Where(s => s.SessionId != session.SessionId && !IsTestSession(s))
                        .Any(s => SessionsOverlap(session, s));
                    
                    if (overlapping)
                    {
                        issues.Add(new SessionIssue
                        {
                            SessionId = session.SessionId,
                            Type = IssueType.OverlappingSessions,
                            Description = "Session has date overlap with another session",
                            Severity = IssueSeverity.Warning,
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }

                report.Issues = issues;
                
                if (issues.Any(i => i.Severity == IssueSeverity.Critical))
                    report.OverallHealth = SystemHealthStatus.Critical;
                else if (issues.Any(i => i.Severity == IssueSeverity.Error))
                    report.OverallHealth = SystemHealthStatus.Error;
                else if (issues.Any(i => i.Severity == IssueSeverity.Warning))
                    report.OverallHealth = SystemHealthStatus.Warning;
            }

            return report;
        }

        #endregion

        #region Helper Methods

        private DateTime GetStartDate(AcademicSession session)
        {
            return session.Semesters?.Any() == true 
                ? session.Semesters.Min(s => s.StartDate) 
                : new DateTime(session.YearStart, 9, 1);
        }

        private DateTime GetEndDate(AcademicSession session)
        {
            return session.Semesters?.Any() == true 
                ? session.Semesters.Max(s => s.EndDate) 
                : new DateTime(session.YearEnd, 6, 30);
        }

        private bool IsSessionActiveAtTime(AcademicSession session, DateTime time)
        {
            var startDate = GetStartDate(session);
            var endDate = GetEndDate(session);
            return time >= startDate && time <= endDate;
        }

        private bool IsTestSession(AcademicSession session)
        {
            // Identify test sessions by naming convention or metadata
            return session.SessionId.Contains("_TEST_") || 
                   session.SessionId.Contains("_DEMO_") ||
                   session.ModifiedBy?.Equals("TestSystem", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool IsDeletedOrInvalidSession(AcademicSession session)
        {
            // Check for deletion markers or invalid data
            return session.SessionId.Contains("_DELETED_") ||
                   session.YearStart <= 0 ||
                   session.YearEnd <= session.YearStart;
        }

        private bool SessionsOverlap(AcademicSession session1, AcademicSession session2)
        {
            var start1 = GetStartDate(session1);
            var end1 = GetEndDate(session1);
            var start2 = GetStartDate(session2);
            var end2 = GetEndDate(session2);

            return start1 < end2 && start2 < end1;
        }

        private OverlapResolutionAction DetermineOverlapResolution(AcademicSession session1, AcademicSession session2)
        {
            // Simple logic - can be enhanced based on business rules
            var start1 = GetStartDate(session1);
            var start2 = GetStartDate(session2);
            
            return start1 < start2 ? OverlapResolutionAction.TruncateOldSession : OverlapResolutionAction.DelayNewSession;
        }
#endregion 
        // Placeholder methods for actual business
        #region Business Logic Implementation Methods

        private async Task ArchiveSessionDataAsync(AcademicSession session)
        {
            try
            {
                _logger.LogInformation($"Archiving session data for: {session.SessionId}");
                
                // Implementation placeholder:
                // 1. Move active session data to archive collection
                // 2. Update session status to archived
                // 3. Compress historical data
                // 4. Update indexes for archived data access
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Session data archived successfully: {session.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to archive session data for: {session.SessionId}");
                throw;
            }
        }

        private async Task GenerateSessionReportsAsync(AcademicSession session)
        {
            try
            {
                _logger.LogInformation($"Generating session reports for: {session.SessionId}");
                
                // Implementation placeholder:
                // 1. Generate enrollment summary report
                // 2. Create academic performance analytics
                // 3. Generate financial summary
                // 4. Create administrative reports
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Session reports generated successfully: {session.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate session reports for: {session.SessionId}");
                throw;
            }
        }

        private async Task NotifySessionEndAsync(AcademicSession session)
        {
            try
            {
                _logger.LogInformation($"Sending session end notifications for: {session.SessionId}");
                
                // Implementation placeholder:
                // 1. Notify administrators
                // 2. Send student notifications
                // 3. Alert faculty members
                // 4. Trigger external system notifications
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Session end notifications sent successfully: {session.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send session end notifications for: {session.SessionId}");
                throw;
            }
        }
        private async Task NotifySemesterEndAsync(Semester semester)
        {
            try
            {
                _logger.LogInformation($"Sending semester end notifications for: {semester.SemesterId}");
                
                // Implementation placeholder:
                // 1. Notify students of final grades
                // 2. Send faculty semester completion notifications
                // 3. Alert administrators
                // 4. Trigger registration reminders for next semester
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Semester end notifications sent successfully: {semester.SemesterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send semester end notifications for: {semester.SemesterId}");
                throw;
            }
        }

        private async Task InitializeSessionSettingsAsync(AcademicSession session)
        {
            try
            {
                _logger.LogInformation($"Initializing session settings for: {session.SessionId}");
                
                // Implementation placeholder:
                // 1. Set up academic calendar
                // 2. Initialize system parameters
                // 3. Configure enrollment settings
                // 4. Set up grade policies
                // 5. Initialize fee structures
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Session settings initialized successfully: {session.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize session settings for: {session.SessionId}");
                throw;
            }
        }

        private async Task SetupEnrollmentPeriodsAsync(AcademicSession session)
        {
            try
            {
                _logger.LogInformation($"Setting up enrollment periods for: {session.SessionId}");
                
                // Implementation placeholder:
                // 1. Configure early enrollment period
                // 2. Set regular enrollment dates
                // 3. Define late enrollment parameters
                // 4. Set up waitlist management
                // 5. Configure enrollment caps
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Enrollment periods configured successfully: {session.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to setup enrollment periods for: {session.SessionId}");
                throw;
            }
        }

        private async Task NotifySessionStartAsync(AcademicSession session)
        {
            try
            {
                _logger.LogInformation($"Sending session start notifications for: {session.SessionId}");
                
                // Implementation placeholder:
                // 1. Welcome students back
                // 2. Notify faculty of session start
                // 3. Alert administrators
                // 4. Send enrollment reminders
                // 5. Distribute academic calendar
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Session start notifications sent successfully: {session.SessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send session start notifications for: {session.SessionId}");
                throw;
            }
        }

        private async Task OpenCourseEnrollmentAsync(Semester semester)
        {
            try
            {
                _logger.LogInformation($"Opening course enrollment for: {semester.SemesterId}");
                
                // Implementation placeholder:
                // 1. Activate course catalog
                // 2. Enable student enrollment interface
                // 3. Initialize waitlist systems
                // 4. Set up prerequisite validation
                // 5. Configure enrollment notifications
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Course enrollment opened successfully: {semester.SemesterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to open course enrollment for: {semester.SemesterId}");
                throw;
            }
        }

        private async Task InitializeGradebooksAsync(Semester semester)
        {
            try
            {
                _logger.LogInformation($"Initializing gradebooks for: {semester.SemesterId}");
                
                // Implementation placeholder:
                // 1. Create gradebook templates
                // 2. Set up grading scales
                // 3. Configure assignment categories
                // 4. Initialize attendance tracking
                // 5. Set up gradebook permissions
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Gradebooks initialized successfully: {semester.SemesterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize gradebooks for: {semester.SemesterId}");
                throw;
            }
        }

        private async Task NotifySemesterStartAsync(Semester semester)
        {
            try
            {
                _logger.LogInformation($"Sending semester start notifications for: {semester.SemesterId}");
                
                // Implementation placeholder:
                // 1. Notify students of semester start
                // 2. Send faculty preparation reminders
                // 3. Alert administrators
                // 4. Distribute class schedules
                // 5. Send enrollment confirmation
                
                await Task.Delay(100); // Placeholder for actual implementation
                
                _logger.LogInformation($"Semester start notifications sent successfully: {semester.SemesterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send semester start notifications for: {semester.SemesterId}");
                throw;
            }
        }

        #endregion

       

    #region Student Level Management

/// <summary>
/// Process student level promotions at session end
/// </summary>
private async Task ProcessStudentLevelPromotionsAsync(AcademicSession endedSession)
{
    try
    {
        _logger.LogInformation($"Processing level promotions for session: {endedSession.SessionId}");
        
        // Get students eligible for promotion based on session completion
        var eligibleStudents = await GetPromotionEligibleStudentsAsync(endedSession);
        
        if (!eligibleStudents.Any())
        {
            _logger.LogInformation("No students eligible for promotion");
            return;
        }
        
        // Build promotion map: matricNumber -> newLevel
        var promotionMap = new Dictionary<string, string>();
        foreach (var student in eligibleStudents)
        {
            var currentLevel = await _firestoreService.GetStudentLevelAsync(student.MatricNumber);
            if (currentLevel != null)
            {
                var nextLevel = CalculateNextLevel(currentLevel.Level);
                promotionMap[student.MatricNumber] = nextLevel;
            }
        }
        
        // Batch update levels
        var success = await _firestoreService.BatchUpdateStudentLevelsAsync(promotionMap);
        
        if (success)
        {
            _logger.LogInformation($"Successfully promoted {promotionMap.Count} students");
            await LogPromotionActivityAsync(endedSession.SessionId, promotionMap);
        }
        else
        {
            _logger.LogError("Failed to process some student promotions");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error processing student level promotions for session: {endedSession.SessionId}");
    }
}

/// <summary>
/// Get students eligible for level promotion
/// </summary>
private async Task<List<PromotionEligibleStudent>> GetPromotionEligibleStudentsAsync(AcademicSession session)
{
    //all students eligable for uni so hmmmmmmmmm
    // Placeholder - replace with actual implementation
    return new List<PromotionEligibleStudent>();
}

/// <summary>
/// Calculate next academic level
/// </summary>
private string CalculateNextLevel(string currentLevel)
{
    return currentLevel switch
    {
        "100" => "200",
        "200" => "300", 
        "300" => "400",
        "400" => "500", // Graduate level
        _ => currentLevel // No change for unknown levels
    };
}

/// <summary>
/// Log promotion activity for audit trail
/// </summary>
private async Task LogPromotionActivityAsync(string sessionId, Dictionary<string, string> promotions)
{
    try
    {
        var promotionLog = new
        {
            SessionId = sessionId,
            ProcessedAt = await _serverTimeService.GetCurrentServerTimeAsync(),
            PromotionCount = promotions.Count,
            Promotions = promotions
        };
        
        await _firestoreService.AddDocumentAsync("PROMOTION_LOGS", promotionLog);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to log promotion activity");
    }
}

#endregion

    #region Student Course Management

private async Task ClearAllStudentCoursesAsync()
{
    try
    {
        _logger.LogInformation("Clearing all student course references");
        
        var success = await _courseService.ClearAllStudentCourseReferencesAsync();
        
        if (success)
        {
            _logger.LogInformation("Successfully cleared all student course references");
        }
        else
        {
            _logger.LogError("Failed to clear some student course references");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error clearing student courses");
        throw;
    }
}

private async Task SendSemesterEndNotificationsAsync(Semester endedSemester)
{
    try
    {
        _logger.LogInformation($"Sending semester end notifications for: {endedSemester.SemesterId}");
        
        // Get all students from the semester level
        var allStudentCourses = await _courseService.GetAllStudentCoursesAsync();
        
        var messages = new List<NotificationMessage>
        {
            new NotificationMessage
            {
                Header = "Semester Ended",
                Content = $"Semester {endedSemester.SessionId} has officially ended. Course registrations have been cleared.",
                Type = "Academic",
                Timestamp = DateTime.Now
            }
        };
//ok get student email hmmm,that might be tricky maybe, we can since only superior admin can do transition,use the user roles with students roles,yeah that will work
        var notifications = allStudentCourses.Select(async student =>
        {
            return await _supabaseDatabase.SendNotificationAsync(
                senderUserId: "SYSTEM",
                senderEmail: "system@aircode.edu",
                receiverUserId: student.StudentMatricNumber,
                receiverEmail: $"{student.StudentMatricNumber}@student.aircode.edu",
                messages: messages,
                isSystemMessage: true,
                metadata: new Dictionary<string, object>
                {
                    ["semester_id"] = endedSemester.SemesterId,
                    ["session_id"] = endedSemester.SessionId,
                    ["event_type"] = "semester_end"
                }
            );
        });

        await Task.WhenAll(notifications);
        
        _logger.LogInformation($"Semester end notifications sent to {allStudentCourses.Count} students");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending semester end notifications");
    }
}

#endregion

    #region Notifications

// Update ProcessSessionEndAsync to include session notifications
private async Task SendSessionEndNotificationsAsync(AcademicSession endedSession)
{
    try
    {
        var allStudentCourses = await _courseService.GetAllStudentCoursesAsync();
        
        var messages = new List<NotificationMessage>
        {
            new NotificationMessage
            {
                Header = "Academic Session Ended", 
                Content = $"Academic session {endedSession.SessionId} has ended. Student level promotions have been processed.",
                Type = "Academic",
                Timestamp = DateTime.Now
            }
        };

        var notifications = allStudentCourses.Select(async student =>
        {
            return await _supabaseDatabase.SendNotificationAsync(
                senderUserId: "SYSTEM",
                senderEmail: "system@aircode.edu", 
                receiverUserId: student.StudentMatricNumber,
                receiverEmail: $"{student.StudentMatricNumber}@student.aircode.edu",
                messages: messages,
                isSystemMessage: true,
                metadata: new Dictionary<string, object>
                {
                    ["session_id"] = endedSession.SessionId,
                    ["event_type"] = "session_end"
                }
            );
        });

        await Task.WhenAll(notifications);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending session end notifications");
    }
}

#endregion


}
    
}
