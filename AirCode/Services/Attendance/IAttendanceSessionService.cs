using AirCode.Models.Supabase;

namespace AirCode.Services.Attendance
{
    public interface IAttendanceSessionService
    {
        // Regular attendance sessions
        Task<SupabaseAttendanceSession> CreateSessionAsync(SupabaseAttendanceSession session);
        Task<SupabaseAttendanceSession?> GetSessionByIdAsync(string sessionId);
        Task<SupabaseAttendanceSession> UpdateSessionAsync(SupabaseAttendanceSession session);
        Task<SupabaseAttendanceSession> UpdateTemporalKeyAsync(string sessionId, string newTemporalKey);
        Task<List<SupabaseAttendanceSession>> GetActiveSessionsAsync();
        Task<List<SupabaseAttendanceSession>> GetAllSessionsAsync();
        Task<List<SupabaseAttendanceSession>> GetSessionsByCourseCodeAsync(string courseCode);
        Task<bool> DeleteSessionAsync(string sessionId);

        // Offline attendance sessions
        Task<SupabaseOfflineAttendanceSession> CreateOfflineSessionAsync(SupabaseOfflineAttendanceSession supabaseOfflineSession);
        Task<SupabaseOfflineAttendanceSession?> GetOfflineSessionByIdAsync(string sessionId);
        Task<SupabaseOfflineAttendanceSession> UpdateOfflineSessionAsync(SupabaseOfflineAttendanceSession supabaseOfflineSession);
        Task<List<SupabaseOfflineAttendanceSession>> GetActiveOfflineSessionsAsync();
        Task<List<SupabaseOfflineAttendanceSession>> GetAllOfflineSessionsAsync();
        Task<List<SupabaseOfflineAttendanceSession>> GetOfflineSessionsByCourseCodeAsync(string courseCode);
        Task<bool> DeleteOfflineSessionAsync(string sessionId);

        // Archive functionality
        Task<bool> ArchiveAttendanceDataAsync(string courseCode, string archivedData, string dataType);
    }
}