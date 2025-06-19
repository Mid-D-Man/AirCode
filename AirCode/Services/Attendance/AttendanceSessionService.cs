using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AirCode.Services.Attendance
{

    public class AttendanceSessionService : IAttendanceSessionService
    {
        private readonly ISupabaseDatabase _database;
        private readonly ILogger<AttendanceSessionService> _logger;

        public AttendanceSessionService(
            ISupabaseDatabase database,
            ILogger<AttendanceSessionService> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task<AttendanceSession> CreateSessionAsync(AttendanceSession session)
        {
            try
            {
                session.CreatedAt = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;
                
                var result = await _database.InsertAsync(session);
                _logger.LogInformation("Created attendance session: {SessionId}", session.SessionId);
                
                return result.FirstOrDefault() ?? session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create attendance session: {SessionId}", session.SessionId);
                throw;
            }
        }

        public async Task<AttendanceSession?> GetSessionByIdAsync(string sessionId)
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<AttendanceSession>(
                    "session_id", Operator.Equals, sessionId);
                
                return sessions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get attendance session: {SessionId}", sessionId);
                return null;
            }
        }

        public async Task<AttendanceSession> UpdateSessionAsync(AttendanceSession session)
        {
            try
            {
                session.UpdatedAt = DateTime.UtcNow;
                
                var result = await _database.UpdateAsync(session);
                _logger.LogInformation("Updated attendance session: {SessionId}", session.SessionId);
                
                return result.FirstOrDefault() ?? session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update attendance session: {SessionId}", session.SessionId);
                throw;
            }
        }

        public async Task<AttendanceSession> UpdateTemporalKeyAsync(string sessionId, string newTemporalKey)
        {
            try
            {
                var session = await GetSessionByIdAsync(sessionId);
                if (session == null)
                    throw new InvalidOperationException($"Session not found: {sessionId}");

                session.TemporalKey = newTemporalKey;
                session.UpdatedAt = DateTime.UtcNow;//yap we update so we can add slight 20sec grace to qr code scan
                var result = await _database.UpdateAsync(session);
                _logger.LogInformation("Updated temporal key for session: {SessionId}", sessionId);
                
                return result.FirstOrDefault() ?? session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update temporal key for session: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<List<AttendanceSession>> GetActiveSessionsAsync()
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<AttendanceSession>(
                    "expiration_time", Operator.GreaterThan, DateTime.UtcNow);
                
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active sessions");
                return new List<AttendanceSession>();
            }
        }

        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            try
            {
                var session = await GetSessionByIdAsync(sessionId);
                if (session == null)
                    return false;

                await _database.DeleteAsync(session);
                _logger.LogInformation("Deleted attendance session: {SessionId}", sessionId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete attendance session: {SessionId}", sessionId);
                return false;
            }
        }
    }
}