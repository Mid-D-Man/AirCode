using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using static Supabase.Postgrest.Constants;

namespace AirCode.Services.Attendance
{/// <summary>
 /// for supabase attendance session,delete online session imediately after session end and offline sessions only get
 /// deleted after semester end, but fetched from when ever new session is created or rather ends-- - - - - - hmmmmmmm think about it
 /// </summary>
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

        public async Task<SupabaseAttendanceSession> CreateSessionAsync(SupabaseAttendanceSession session)
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

        public async Task<SupabaseOfflineAttendanceSession> CreateOfflineSessionAsync(SupabaseOfflineAttendanceSession supabaseOfflineSession)
        {
            try
            {
                supabaseOfflineSession.CreatedAt = DateTime.UtcNow;
                
                var result = await _database.InsertAsync(supabaseOfflineSession);
                _logger.LogInformation("Created offline attendance session: {SessionId}", supabaseOfflineSession.SessionId);
                
                return result.FirstOrDefault() ?? supabaseOfflineSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create offline attendance session: {SessionId}", supabaseOfflineSession.SessionId);
                throw;
            }
        }

        public async Task<SupabaseAttendanceSession?> GetSessionByIdAsync(string sessionId)
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseAttendanceSession>(
                    "session_id", Operator.Equals, sessionId);
                
                return sessions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get attendance session: {SessionId}", sessionId);
                return null;
            }
        }

        public async Task<SupabaseOfflineAttendanceSession?> GetOfflineSessionByIdAsync(string sessionId)
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseOfflineAttendanceSession>(
                    "session_id", Operator.Equals, sessionId);
                
                return sessions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline attendance session: {SessionId}", sessionId);
                return null;
            }
        }

        public async Task<SupabaseAttendanceSession> UpdateSessionAsync(SupabaseAttendanceSession session)
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

        public async Task<SupabaseOfflineAttendanceSession> UpdateOfflineSessionAsync(SupabaseOfflineAttendanceSession supabaseOfflineSession)
        {
            try
            {
                var result = await _database.UpdateAsync(supabaseOfflineSession);
                _logger.LogInformation("Updated offline attendance session: {SessionId}", supabaseOfflineSession.SessionId);
                
                return result.FirstOrDefault() ?? supabaseOfflineSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update offline attendance session: {SessionId}", supabaseOfflineSession.SessionId);
                throw;
            }
        }

        public async Task<SupabaseAttendanceSession> UpdateTemporalKeyAsync(string sessionId, string newTemporalKey)
        {
            try
            {
                var session = await GetSessionByIdAsync(sessionId);
                if (session == null)
                    throw new InvalidOperationException($"Session not found: {sessionId}");

                session.TemporalKey = newTemporalKey;
                session.UpdatedAt = DateTime.UtcNow;
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

        public async Task<List<SupabaseAttendanceSession>> GetActiveSessionsAsync()
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseAttendanceSession>(
                    "expiration_time", Operator.GreaterThan, DateTime.UtcNow);
                
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active sessions");
                return new List<SupabaseAttendanceSession>();
            }
        }

        public async Task<List<SupabaseOfflineAttendanceSession>> GetActiveOfflineSessionsAsync()
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseOfflineAttendanceSession>(
                    "expiration_time", Operator.GreaterThan, DateTime.UtcNow);
                
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active offline sessions");
                return new List<SupabaseOfflineAttendanceSession>();
            }
        }

        public async Task<List<SupabaseAttendanceSession>> GetAllSessionsAsync()
        {
            try
            {
                var sessions = await _database.GetAllAsync<SupabaseAttendanceSession>();
                return sessions.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all attendance sessions");
                return new List<SupabaseAttendanceSession>();
            }
        }

        public async Task<List<SupabaseOfflineAttendanceSession>> GetAllOfflineSessionsAsync()
        {
            try
            {
                var sessions = await _database.GetAllAsync<SupabaseOfflineAttendanceSession>();
                return sessions.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all offline attendance sessions");
                return new List<SupabaseOfflineAttendanceSession>();
            }
        }

        public async Task<List<SupabaseAttendanceSession>> GetSessionsByCourseCodeAsync(string courseCode)
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseAttendanceSession>(
                    "course_code", Operator.Equals, courseCode);
                
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get sessions for course: {CourseCode}", courseCode);
                return new List<SupabaseAttendanceSession>();
            }
        }

        public async Task<List<SupabaseOfflineAttendanceSession>> GetOfflineSessionsByCourseCodeAsync(string courseCode)
        {
            try
            {
                var sessions = await _database.GetWithFilterAsync<SupabaseOfflineAttendanceSession>(
                    "course_code", Operator.Equals, courseCode);
                
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline sessions for course: {CourseCode}", courseCode);
                return new List<SupabaseOfflineAttendanceSession>();
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

        public async Task<bool> DeleteOfflineSessionAsync(string sessionId)
        {
            try
            {
                var session = await GetOfflineSessionByIdAsync(sessionId);
                if (session == null)
                    return false;

                await _database.DeleteAsync(session);
                _logger.LogInformation("Deleted offline attendance session: {SessionId}", sessionId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete offline attendance session: {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<bool> ArchiveAttendanceDataAsync(string courseCode, string archivedData, string dataType)
        {
            try
            {
                var archiveRecord = new ArchivedAttendanceData
                {
                    CourseCode = courseCode,
                    ArchivedData = archivedData,
                    ArchivedAt = DateTime.UtcNow,
                    DataType = dataType,
                    CompressionUsed = false // You can implement compression logic here
                };

                await _database.InsertAsync(archiveRecord);
                _logger.LogInformation("Archived attendance data for course: {CourseCode}", courseCode);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive attendance data for course: {CourseCode}", courseCode);
                return false;
            }
        }
    }
}