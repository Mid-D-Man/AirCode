using AirCode.Domain.Entities;
using AirCode.Domain.Enums;

namespace AirCode.Services.Academic
{
    /// <summary>
    /// Service for za managing academic session lifecycle and transition logic
    /// </summary>
    public interface IAcademicSessionService
    {
        #region Session Management
        /// <summary>
        /// Check and process any session/semester transitions that occurred while user was offline
        /// </summary>
        Task<SessionTransitionResult> ProcessPendingTransitionsAsync(string userId);
        
        /// <summary>
        /// Get the current active academic session
        /// </summary>
        Task<AcademicSession> GetCurrentSessionAsync();
        
        /// <summary>
        /// Get the next upcoming academic session
        /// </summary>
        Task<AcademicSession> GetNextSessionAsync();
        
        /// <summary>
        /// Get all archived sessions
        /// </summary>
        Task<List<AcademicSession>> GetArchivedSessionsAsync();
        
        /// <summary>
        /// Refresh session cache and check for transitions
        /// </summary>
        Task RefreshSessionCacheAsync();
        #endregion

        #region Transition Logic
        /// <summary>
        /// Check if any sessions have ended and need processing
        /// </summary>
        Task<List<SessionEndEvent>> CheckForEndedSessionsAsync();
        
        /// <summary>
        /// Check if any semesters have ended and need processing
        /// </summary>
        Task<List<SemesterEndEvent>> CheckForEndedSemestersAsync();
        
        /// <summary>
        /// Process session end logic (archiving, data cleanup, notifications)
        /// </summary>
        Task ProcessSessionEndAsync(AcademicSession endedSession, string triggeredBy);
        
        /// <summary>
        /// Process semester end logic (grade finalization, reports, etc.)
        /// </summary>
        Task ProcessSemesterEndAsync(Semester endedSemester, string triggeredBy);
        
        /// <summary>
        /// Process session start logic (activation, setup, notifications)
        /// </summary>
        Task ProcessSessionStartAsync(AcademicSession newSession, string triggeredBy);
        
        /// <summary>
        /// Process semester start logic (enrollment opening, course setup, etc.)
        /// </summary>
        Task ProcessSemesterStartAsync(Semester newSemester, string triggeredBy);
        #endregion

        #region Validation & Edge Cases
        /// <summary>
        /// Validate if a session should trigger end logic (not deleted, not test, etc.)
        /// </summary>
        Task<bool> ShouldProcessSessionEndAsync(AcademicSession session);
        
        /// <summary>
        /// Check if session transitions have already been processed
        /// </summary>
        Task<bool> HasTransitionBeenProcessedAsync(string sessionId, TransitionType type);
        
        /// <summary>
        /// Mark a transition as processed to prevent duplicate processing
        /// </summary>
        Task MarkTransitionProcessedAsync(string sessionId, TransitionType type, string processedBy);
        
        /// <summary>
        /// Handle overlapping sessions (when new session starts before old one officially ends)
        /// </summary>
        Task<SessionOverlapResult> ResolveSessionOverlapAsync();
        
        /// <summary>
        /// Clean up orphaned or invalid session data
        /// </summary>
        Task CleanupInvalidSessionsAsync();
        #endregion

        #region Utilities
        /// <summary>
        /// Get the last user login time to determine how far back to check for transitions
        /// </summary>
        Task<DateTime?> GetLastLoginTimeAsync(string userId);
        
        /// <summary>
        /// Set the user's last login time
        /// </summary>
        Task SetLastLoginTimeAsync(string userId, DateTime loginTime);
        
        /// <summary>
        /// Check if the system is in a valid state for processing transitions
        /// </summary>
        Task<SystemValidationResult> ValidateSystemStateAsync();
        
        /// <summary>
        /// Get session health status and any issues
        /// </summary>
        Task<SessionHealthReport> GetSessionHealthReportAsync();
        #endregion

        #region Events
        /// <summary>
        /// Event fired when a session ends
        /// </summary>
        event Func<SessionEndEvent, Task> SessionEnded;
        
        /// <summary>
        /// Event fired when a semester ends
        /// </summary>
        event Func<SemesterEndEvent, Task> SemesterEnded;
        
        /// <summary>
        /// Event fired when a session starts
        /// </summary>
        event Func<SessionStartEvent, Task> SessionStarted;
        
        /// <summary>
        /// Event fired when a semester starts
        /// </summary>
        event Func<SemesterStartEvent, Task> SemesterStarted;
        #endregion
    }

}
