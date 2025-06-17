using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AirCode.Services.Attendance
{
    public class SessionStateService
    {
        private readonly List<ActiveSessionData> _activeSessions = new();
        private readonly Dictionary<string, SessionData> _currentSessions = new();

        public event Action StateChanged;

        public List<ActiveSessionData> GetActiveSessions() => _activeSessions.ToList();

        public SessionData GetCurrentSession(string courseId)
        {
            _currentSessions.TryGetValue(courseId, out var session);
            return session;
        }

        public void AddActiveSession(ActiveSessionData session)
        {
            _activeSessions.Add(session);
            StateChanged?.Invoke();
        }

        public void RemoveActiveSession(string sessionId)
        {
            _activeSessions.RemoveAll(s => s.SessionId == sessionId);
            StateChanged?.Invoke();
        }

        // NEW: Add the missing UpdateActiveSession method
        public void UpdateActiveSession(ActiveSessionData updatedSession)
        {
            var existingSession = _activeSessions.FirstOrDefault(s => s.SessionId == updatedSession.SessionId);
            if (existingSession != null)
            {
                var index = _activeSessions.IndexOf(existingSession);
                _activeSessions[index] = updatedSession;
                StateChanged?.Invoke();
            }
        }

        public void UpdateCurrentSession(string courseId, SessionData session)
        {
            _currentSessions[courseId] = session;
            StateChanged?.Invoke();
        }

        public void RemoveCurrentSession(string courseId)
        {
            _currentSessions.Remove(courseId);
            StateChanged?.Invoke();
        }

        public void CleanupExpiredSessions()
        {
            var expiredSessions = _activeSessions
                .Where(s => DateTime.UtcNow >= s.EndTime)
                .ToList();

            foreach (var expired in expiredSessions)
            {
                _activeSessions.Remove(expired);
            }

            if (expiredSessions.Any())
            {
                StateChanged?.Invoke();
            }
        }

        public bool HasActiveSession(string courseId)
        {
            return _activeSessions.Any(s => s.CourseId == courseId && DateTime.UtcNow < s.EndTime);
        }
    }

    // ActiveSessionData.cs - Add these properties to your existing ActiveSessionData class

public class ActiveSessionData
{
    public string SessionId { get; set; }
    public string CourseName { get; set; }
    public string CourseId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Duration { get; set; }
    public string QrCodePayload { get; set; }
    public string Theme { get; set; }
    
    // Add these missing properties:
    public bool UseTemporalKeyRefresh { get; set; }
    public AdvancedSecurityFeatures SecurityFeatures { get; set; } = AdvancedSecurityFeatures.Default;
}

    public class SessionData
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public string CourseName { get; set; }
        public string CourseId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Date { get; set; }
        public int Duration { get; set; }
        public string LectureId { get; set; }
    }
}
