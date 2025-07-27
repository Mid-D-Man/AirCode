using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AirCode.Models.QRCode;
using AirCode.Services.Storage;
using AirCode.Services.Firebase;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AirCode.Domain.Entities;

namespace AirCode.Services.Attendance
{
    public class SessionStateService
    {
        private readonly List<SessionData> _activeSessions = new();
        private readonly Dictionary<string, SessionData> _currentSessions = new();
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly IFirestoreService _firestoreService;
        private readonly ILogger<SessionStateService> _logger;

        private const string PERSISTENT_SESSIONS_KEY = "aircode_persistent_sessions";
        private const string CURRENT_SESSIONS_KEY = "aircode_current_sessions";

        public event Action StateChanged;

        public SessionStateService(
            IBlazorAppLocalStorageService localStorage,
            IFirestoreService firestoreService,
            ILogger<SessionStateService> logger)
        {
            _localStorage = localStorage;
            _firestoreService = firestoreService;
            _logger = logger;
        }

        /// <summary>
        /// Initialize service and recover any orphaned sessions
        /// </summary>
        public async Task InitializeAsync()
        {
            await RestorePersistedSessions();
            await RecoverOrphanedSessions();
        }
//Retrives current stored sesssion
public async Task<List<SessionData>> GetStoredSessionsAsync()
{
    var persistedSessions = await GetPersistedSessionsAsync();
    return persistedSessions.Values.ToList();
}

public async Task RemoveStoredSessionAsync(string sessionId)
{
    await RemovePersistedSessionAsync(sessionId);
}
        public List<SessionData> GetActiveSessions() => _activeSessions.ToList();

        public SessionData GetCurrentSession(string courseId)
        {
            _currentSessions.TryGetValue(courseId, out var session);
            return session;
        }

        public async Task AddActiveSessionAsync(SessionData session)
        {
            _activeSessions.Add(session);
            await PersistSessionAsync(session);
            StateChanged?.Invoke();
        }

        public async Task RemoveActiveSessionAsync(string sessionId)
        {
            var removedSession = _activeSessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (removedSession != null)
            {
                _activeSessions.RemoveAll(s => s.SessionId == sessionId);
                await RemovePersistedSessionAsync(sessionId);
                StateChanged?.Invoke();
            }
        }

        public async Task UpdateActiveSessionAsync(SessionData updatedSession)
        {
            var existingSession = _activeSessions.FirstOrDefault(s => s.SessionId == updatedSession.SessionId);
            if (existingSession != null)
            {
                var index = _activeSessions.IndexOf(existingSession);
                _activeSessions[index] = updatedSession;
                await PersistSessionAsync(updatedSession);
                StateChanged?.Invoke();
            }
        }

        public async Task UpdateCurrentSessionAsync(string courseId, SessionData session)
        {
            _currentSessions[courseId] = session;
            await PersistCurrentSessionsAsync();
            StateChanged?.Invoke();
        }

        public async Task RemoveCurrentSessionAsync(string courseId)
        {
            _currentSessions.Remove(courseId);
            await PersistCurrentSessionsAsync();
            StateChanged?.Invoke();
        }

        public async Task CleanupExpiredSessionsAsync()
        {
            var expiredSessions = _activeSessions
                .Where(s => DateTime.UtcNow >= s.EndTime)
                .ToList();

            foreach (var expired in expiredSessions)
            {
                _activeSessions.Remove(expired);
                await RemovePersistedSessionAsync(expired.SessionId);
                
                // Update Firebase to mark session as expired
                await MarkFirebaseSessionExpired(expired);
            }

            if (expiredSessions.Any())
            {
                StateChanged?.Invoke();
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
            }
        }

        public bool HasActiveSession(string courseId)
        {
            return _activeSessions.Any(s => s.CourseCode == courseId && DateTime.UtcNow < s.EndTime);
        }

        /// <summary>
        /// Persist active session data to local storage
        /// </summary>
        private async Task PersistSessionAsync(SessionData session)
        {
            try
            {
                var persistentData = new SessionData
                {
                    SessionId = session.SessionId,
                    CourseCode = session.CourseCode,
                    CourseName = session.CourseName,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    Duration = session.Duration,
                    CreatedAt = DateTime.UtcNow,
                    QrCodePayload = session.QrCodePayload,
                    Theme = session.Theme,
                    UseTemporalKeyRefresh = session.UseTemporalKeyRefresh,
                    SecurityFeatures = session.SecurityFeatures,
                    TemporalKey = session.TemporalKey
                };

                var existingSessions = await GetPersistedSessionsAsync();
                existingSessions[session.SessionId] = persistentData;

                await _localStorage.SetItemAsync(PERSISTENT_SESSIONS_KEY, existingSessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist session {SessionId}", session.SessionId);
            }
        }

        /// <summary>
        /// Remove persisted session data
        /// </summary>
        private async Task RemovePersistedSessionAsync(string sessionId)
        {
            try
            {
                var existingSessions = await GetPersistedSessionsAsync();
                if (existingSessions.Remove(sessionId))
                {
                    await _localStorage.SetItemAsync(PERSISTENT_SESSIONS_KEY, existingSessions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove persisted session {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Get all persisted sessions from local storage
        /// </summary>
        private async Task<Dictionary<string, SessionData>> GetPersistedSessionsAsync()
        {
            try
            {
                var sessions = await _localStorage.GetItemAsync<Dictionary<string, SessionData>>(PERSISTENT_SESSIONS_KEY);
                return sessions ?? new Dictionary<string, SessionData>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get persisted sessions");
                return new Dictionary<string, SessionData>();
            }
        }

        /// <summary>
        /// Persist current session data
        /// </summary>
        private async Task PersistCurrentSessionsAsync()
        {
            try
            {
                await _localStorage.SetItemAsync(CURRENT_SESSIONS_KEY, _currentSessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist current sessions");
            }
        }

        /// <summary>
        /// Restore sessions from local storage on app startup
        /// </summary>
        private async Task RestorePersistedSessions()
        {
            try
            {
                // Restore active sessions
                var persistedSessions = await GetPersistedSessionsAsync();
                foreach (var kvp in persistedSessions)
                {
                    var persistentData = kvp.Value;
                    var activeSession = new SessionData
                    {
                        SessionId = persistentData.SessionId,
                        CourseCode = persistentData.CourseCode,
                        CourseName = persistentData.CourseName,
                        StartTime = persistentData.StartTime,
                        EndTime = persistentData.EndTime,
                        Duration = persistentData.Duration,
                        QrCodePayload = persistentData.QrCodePayload,
                        Theme = persistentData.Theme,
                        UseTemporalKeyRefresh = persistentData.UseTemporalKeyRefresh,
                        SecurityFeatures = persistentData.SecurityFeatures,
                        TemporalKey = persistentData.TemporalKey
                    };

                    _activeSessions.Add(activeSession);
                }

                // Restore current sessions
                var currentSessions = await _localStorage.GetItemAsync<Dictionary<string, SessionData>>(CURRENT_SESSIONS_KEY);
                if (currentSessions != null)
                {
                    foreach (var kvp in currentSessions)
                    {
                        _currentSessions[kvp.Key] = kvp.Value;
                    }
                }

                _logger.LogInformation("Restored {ActiveCount} active sessions and {CurrentCount} current sessions", 
                    _activeSessions.Count, _currentSessions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore persisted sessions");
            }
        }

        /// <summary>
        /// Check for orphaned sessions and clean them up
        /// </summary>
        private async Task RecoverOrphanedSessions()
        {
            try
            {
                var persistedSessions = await GetPersistedSessionsAsync();
                var orphanedSessions = new List<string>();

                foreach (var kvp in persistedSessions)
                {
                    var sessionData = kvp.Value;
                    
                    // Check if session has expired
                    if (DateTime.UtcNow >= sessionData.EndTime)
                    {
                        orphanedSessions.Add(sessionData.SessionId);
                        
                        // Mark Firebase session as expired
                        await MarkFirebaseSessionExpired(sessionData);
                        
                        // Remove from active sessions if present
                        _activeSessions.RemoveAll(s => s.SessionId == sessionData.SessionId);
                    }
                }

                // Clean up orphaned sessions from persistence
                foreach (var sessionId in orphanedSessions)
                {
                    await RemovePersistedSessionAsync(sessionId);
                }

                if (orphanedSessions.Any())
                {
                    _logger.LogInformation("Recovered {Count} orphaned sessions", orphanedSessions.Count);
                    StateChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover orphaned sessions");
            }
        }

        /// <summary>
        /// Mark Firebase session as expired/ended
        /// </summary>
        private async Task MarkFirebaseSessionExpired(SessionData session)
        {
            try
            {
                var documentId = $"AttendanceEvent_{session.CourseCode}";
                var eventFieldName = $"Event_{session.SessionId}_{session.StartTime:yyyyMMdd}";
                
                await _firestoreService.AddOrUpdateFieldAsync(
                    "ATTENDANCE_EVENTS", 
                    documentId, 
                    $"{eventFieldName}.Status", 
                    "Expired"
                );
                
                await _firestoreService.AddOrUpdateFieldAsync(
                    "ATTENDANCE_EVENTS", 
                    documentId, 
                    $"{eventFieldName}.ActualEndTime", 
                    DateTime.UtcNow
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark Firebase session as expired: {SessionId}", session.SessionId);
            }
        }


        /// <summary>
        /// Clear all persisted data (use when sessions are properly ended)
        /// </summary>
        public async Task ClearPersistedDataAsync()
        {
            try
            {
                await _localStorage.RemoveItemAsync(PERSISTENT_SESSIONS_KEY);
                await _localStorage.RemoveItemAsync(CURRENT_SESSIONS_KEY);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear persisted data");
            }
        }

        // Legacy methods for backward compatibility
        public void AddActiveSession(SessionData session) => 
            AddActiveSessionAsync(session).ConfigureAwait(false);
        
        public void RemoveActiveSession(string sessionId) => 
            RemoveActiveSessionAsync(sessionId).ConfigureAwait(false);
        
        public void UpdateActiveSession(SessionData updatedSession) => 
            UpdateActiveSessionAsync(updatedSession).ConfigureAwait(false);
        
        public void UpdateCurrentSession(string courseId, SessionData session) => 
            UpdateCurrentSessionAsync(courseId, session).ConfigureAwait(false);
        
        public void RemoveCurrentSession(string courseId) => 
            RemoveCurrentSessionAsync(courseId).ConfigureAwait(false);
        
        public void CleanupExpiredSessions() => 
            CleanupExpiredSessionsAsync().ConfigureAwait(false);
    }

    

    
}
