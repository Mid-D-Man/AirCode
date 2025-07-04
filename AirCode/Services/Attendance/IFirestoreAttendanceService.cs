
using AirCode.Models.Supabase;
namespace AirCode.Services.Attendance
{
    
    public interface IFirestoreAttendanceService
    {
        /// <summary>
        /// Create Firebase attendance event with all enrolled students
        /// </summary>
        Task<bool> CreateAttendanceEventAsync(string sessionId, string courseCode, string courseName, 
            DateTime startTime, int duration, string theme);

        /// <summary>
        /// Update Firebase attendance records with Supabase scan data
        /// </summary>
        Task<bool> UpdateAttendanceRecordsAsync(string sessionId, string courseCode, 
            List<AttendanceRecord> scannedRecords);

        /// <summary>
        /// Update Firebase attendance records with offline scan data
        /// </summary>
        Task<bool> UpdateOfflineAttendanceRecordsAsync(string sessionId, string courseCode, 
            List<OfflineAttendanceRecord> offlineRecords);

        /// <summary>
        /// Complete attendance session and mark as finished
        /// </summary>
        Task<bool> CompleteAttendanceSessionAsync(string sessionId, string courseCode);

        /// <summary>
        /// Get all attendance events for a specific course
        /// </summary>
        Task<Dictionary<string, object>> GetCourseAttendanceEventsAsync(string courseCode);

        /// <summary>
        /// Get all attendance events across all courses
        /// </summary>
        Task<List<Dictionary<string, object>>> GetAllAttendanceEventsAsync();

        /// <summary>
        /// Archive attendance records to Supabase and delete from Firebase
        /// </summary>
        Task<bool> ArchiveAndDeleteCourseAttendanceAsync(string courseCode);

        /// <summary>
        /// Archive all attendance records to Supabase and delete from Firebase
        /// </summary>
        Task<bool> ArchiveAndDeleteAllAttendanceAsync();

        /// <summary>
        /// Get attendance statistics for a course
        /// </summary>
        Task<Dictionary<string, object>> GetCourseAttendanceStatsAsync(string courseCode);
        /// <summary>
/// Auto-sign course rep when session is created
/// </summary>
Task<bool> AutoSignCourseRepAsync(string sessionId, string courseCode);
    
    // Optional: Integrated event creation with auto-sign
    Task<bool> CreateAttendanceEventWithCourseRepAsync(string sessionId, string courseCode, 
        string courseName, DateTime startTime, int duration, string theme);
}
}
