using AirCode.Models.Supabase;
using AirCode.Services.SupaBase;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AirCode.Services.Attendance
{
    public interface IAttendanceSessionService
    {
        Task<AttendanceSession> CreateSessionAsync(AttendanceSession session);
        Task<AttendanceSession?> GetSessionByIdAsync(string sessionId);
        Task<AttendanceSession> UpdateSessionAsync(AttendanceSession session);
        Task<AttendanceSession> UpdateTemporalKeyAsync(string sessionId, string newTemporalKey);
        Task<List<AttendanceSession>> GetActiveSessionsAsync();
        Task<bool> DeleteSessionAsync(string sessionId);
    }
}