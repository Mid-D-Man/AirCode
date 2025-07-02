
using System.Text.Json;
using AirCode.Models.Supabase;
using AirCode.Services.Courses;
using AirCode.Services.Firebase;
namespace AirCode.Services.Attendance
{
    public class FirestoreAttendanceService : IFirestoreAttendanceService
    {
        private readonly IFirestoreService _firestoreService;
        private readonly ICourseService _courseService;
        private readonly IAttendanceSessionService _attendanceSessionService;
        private readonly ILogger<FirestoreAttendanceService> _logger;

        private const string ATTENDANCE_EVENTS_COLLECTION = "ATTENDANCE_EVENTS";
        private const string ARCHIVED_ATTENDANCE_COLLECTION = "ARCHIVED_ATTENDANCE";

        public FirestoreAttendanceService(
            IFirestoreService firestoreService,
            ICourseService courseService,
            IAttendanceSessionService attendanceSessionService,
            ILogger<FirestoreAttendanceService> logger)
        {
            _firestoreService = firestoreService;
            _courseService = courseService;
            _attendanceSessionService = attendanceSessionService;
            _logger = logger;
        }

        /// <summary>
        /// Create Firebase attendance event with all enrolled students
        /// </summary>
        public async Task<bool> CreateAttendanceEventAsync(string sessionId, string courseCode, string courseName, 
            DateTime startTime, int duration, string theme)
        {
            try
            {
                // Calculate end time
                var endTime = startTime.AddMinutes(duration);

                // Get all students taking this course
                var allStudentCourses = await _courseService.GetAllStudentCoursesAsync();
                var studentsInCourse = allStudentCourses
                    .Where(sc => sc.GetEnrolledCourses().Any(cr => cr.CourseCode == courseCode))
                    .ToList();

                // Build AttendanceRecords for each student
                var attendanceRecords = new Dictionary<string, object>();
                foreach (var studentCourse in studentsInCourse)
                {
                    attendanceRecords[studentCourse.StudentMatricNumber] = new
                    {
                        MatricNumber = studentCourse.StudentMatricNumber,
                        HasScannedAttendance = false,
                        ScanTime = (DateTime?)null,
                        IsOnlineScan = false
                    };
                }

                var attendanceEventData = new
                {
                    SessionId = sessionId,
                    CourseCode = courseCode,
                    CourseName = courseName,
                    StartTime = startTime,
                    Duration = duration,
                    EndTime = endTime,
                    Theme = theme,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Active",
                    TemporalKey = "Sukablak",
                    AttendanceRecords = attendanceRecords
                };

                // Create document with course-based naming: AttendanceEvent_{CourseCode}
                var documentId = $"AttendanceEvent_{courseCode}";

                // Check if document exists
                var existingDoc = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>(
                    ATTENDANCE_EVENTS_COLLECTION, documentId);

                if (existingDoc != null)
                {
                    // Add new event as a field within existing document
                    var eventFieldName = $"Event_{sessionId}_{startTime:yyyyMMdd}";
                    await _firestoreService.AddOrUpdateFieldAsync(
                        ATTENDANCE_EVENTS_COLLECTION, documentId, eventFieldName, attendanceEventData);
                }
                else
                {
                    // Create new document with first event
                    var courseEventDocument = new Dictionary<string, object>
                    {
                        ["CourseCode"] = courseCode,
                        ["CourseName"] = courseName,
                        ["CreatedAt"] = DateTime.UtcNow,
                        ["LastEventAt"] = DateTime.UtcNow,
                        [$"Event_{sessionId}_{startTime:yyyyMMdd}"] = attendanceEventData
                    };

                    await _firestoreService.AddDocumentAsync(
                        ATTENDANCE_EVENTS_COLLECTION, courseEventDocument, documentId);
                }

                _logger.LogInformation($"Successfully created Firebase attendance event for session {sessionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating Firebase attendance event for session {sessionId}");
                return false;
            }
        }
        
        /// <summary>
        /// Update Firebase attendance records with Supabase scan data
        /// </summary>
        public async Task<bool> UpdateAttendanceRecordsAsync(string sessionId, string courseCode, 
            List<AttendanceRecord> scannedRecords)
        {
            try
            {
                var documentId = $"AttendanceEvent_{courseCode}";
                
                // Get existing document to find the correct event field
                var existingDoc = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>(
                    ATTENDANCE_EVENTS_COLLECTION, documentId);

                if (existingDoc == null)
                {
                    _logger.LogWarning($"No Firebase document found for course {courseCode}");
                    return false;
                }

                // Find the event field that matches the session ID
                var eventFieldName = existingDoc.Keys
                    .FirstOrDefault(key => key.StartsWith("Event_") && key.Contains(sessionId));

                if (string.IsNullOrEmpty(eventFieldName))
                {
                    _logger.LogWarning($"No event found for session {sessionId} in course {courseCode}");
                    return false;
                }

                // Update attendance records for each scanned student
                foreach (var record in scannedRecords)
                {
                    var attendanceRecordPath = $"{eventFieldName}.AttendanceRecords.{record.MatricNumber}";
                    
                    var updatedRecord = new
                    {
                        MatricNumber = record.MatricNumber,
                        HasScannedAttendance = record.HasScannedAttendance,
                        ScanTime = record.ScanTime,
                        IsOnlineScan = record.IsOnlineScan
                    };

                    await _firestoreService.AddOrUpdateFieldAsync(
                        ATTENDANCE_EVENTS_COLLECTION, documentId, attendanceRecordPath, updatedRecord);
                }

                _logger.LogInformation($"Successfully updated {scannedRecords.Count} attendance records for session {sessionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating attendance records for session {sessionId}");
                return false;
            }
        }

        /// <summary>
        /// Update Firebase attendance records with offline scan data
        /// </summary>
        public async Task<bool> UpdateOfflineAttendanceRecordsAsync(string sessionId, string courseCode, 
            List<OfflineAttendanceRecord> offlineRecords)
        {
            try
            {
                var documentId = $"AttendanceEvent_{courseCode}";
                
                var existingDoc = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>(
                    ATTENDANCE_EVENTS_COLLECTION, documentId);

                if (existingDoc == null)
                {
                    _logger.LogWarning($"No Firebase document found for course {courseCode}");
                    return false;
                }

                var eventFieldName = existingDoc.Keys
                    .FirstOrDefault(key => key.StartsWith("Event_") && key.Contains(sessionId));

                if (string.IsNullOrEmpty(eventFieldName))
                {
                    _logger.LogWarning($"No event found for session {sessionId} in course {courseCode}");
                    return false;
                }

                // Update attendance records for each offline scanned student
                foreach (var record in offlineRecords)
                {
                    var attendanceRecordPath = $"{eventFieldName}.AttendanceRecords.{record.MatricNumber}";
                    
                    var updatedRecord = new
                    {
                        MatricNumber = record.MatricNumber,
                        HasScannedAttendance = record.HasScannedAttendance,
                        ScanTime = record.ScanTime,
                        IsOnlineScan = false // Always false for offline records
                    };

                    await _firestoreService.AddOrUpdateFieldAsync(
                        ATTENDANCE_EVENTS_COLLECTION, documentId, attendanceRecordPath, updatedRecord);
                }

                _logger.LogInformation($"Successfully updated {offlineRecords.Count} offline attendance records for session {sessionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating offline attendance records for session {sessionId}");
                return false;
            }
        }

        /// <summary>
        /// Complete attendance session and mark as finished
        /// </summary>
        public async Task<bool> CompleteAttendanceSessionAsync(string sessionId, string courseCode)
        {
            try
            {
                var documentId = $"AttendanceEvent_{courseCode}";
                
                var existingDoc = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>(
                    ATTENDANCE_EVENTS_COLLECTION, documentId);

                if (existingDoc == null)
                {
                    _logger.LogWarning($"No Firebase document found for course {courseCode}");
                    return false;
                }

                var eventFieldName = existingDoc.Keys
                    .FirstOrDefault(key => key.StartsWith("Event_") && key.Contains(sessionId));

                if (string.IsNullOrEmpty(eventFieldName))
                {
                    _logger.LogWarning($"No event found for session {sessionId} in course {courseCode}");
                    return false;
                }

                // Update status to completed
                await _firestoreService.AddOrUpdateFieldAsync(
                    ATTENDANCE_EVENTS_COLLECTION, documentId, $"{eventFieldName}.Status", "Completed");

                // Update completion time
                await _firestoreService.AddOrUpdateFieldAsync(
                    ATTENDANCE_EVENTS_COLLECTION, documentId, $"{eventFieldName}.CompletedAt", DateTime.UtcNow);

                _logger.LogInformation($"Successfully completed attendance session {sessionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error completing attendance session {sessionId}");
                return false;
            }
        }

        /// <summary>
        /// Get all attendance events for a specific course
        /// </summary>
        public async Task<Dictionary<string, object>> GetCourseAttendanceEventsAsync(string courseCode)
        {
            try
            {
                var documentId = $"AttendanceEvent_{courseCode}";
                var document = await _firestoreService.GetDocumentAsync<Dictionary<string, object>>(
                    ATTENDANCE_EVENTS_COLLECTION, documentId);

                return document ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting attendance events for course {courseCode}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Get all attendance events across all courses
        /// </summary>
        public async Task<List<Dictionary<string, object>>> GetAllAttendanceEventsAsync()
        {
            try
            {
                var allEvents = await _firestoreService.GetCollectionAsync<Dictionary<string, object>>(
                    ATTENDANCE_EVENTS_COLLECTION);

                return allEvents ?? new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all attendance events");
                return new List<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Archive attendance records to Supabase and delete from Firebase
        /// </summary>
        public async Task<bool> ArchiveAndDeleteCourseAttendanceAsync(string courseCode)
        {
            try
            {
                // Get attendance data from Firebase
                var attendanceData = await GetCourseAttendanceEventsAsync(courseCode);
                
                if (!attendanceData.Any())
                {
                    _logger.LogInformation($"No attendance data found for course {courseCode}");
                    return true;
                }

                // Compress and serialize the data
                var compressedData = JsonSerializer.Serialize(attendanceData, new JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });

                // Archive to Supabase
                var archiveSuccess = await _attendanceSessionService.ArchiveAttendanceDataAsync(
                    courseCode, compressedData, "course_attendance");

                if (!archiveSuccess)
                {
                    _logger.LogError($"Failed to archive attendance data for course {courseCode}");
                    return false;
                }

                // Delete from Firebase
                var documentId = $"AttendanceEvent_{courseCode}";
                var deleteSuccess = await _firestoreService.DeleteDocumentAsync(
                    ATTENDANCE_EVENTS_COLLECTION, documentId);

                if (deleteSuccess)
                {
                    _logger.LogInformation($"Successfully archived and deleted attendance data for course {courseCode}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Failed to delete attendance data from Firebase for course {courseCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error archiving attendance data for course {courseCode}");
                return false;
            }
        }

        /// <summary>
        /// Archive all attendance records to Supabase and delete from Firebase
        /// </summary>
        public async Task<bool> ArchiveAndDeleteAllAttendanceAsync()
        {
            try
            {
                // Get all attendance events
                var allAttendanceData = await GetAllAttendanceEventsAsync();
                
                if (!allAttendanceData.Any())
                {
                    _logger.LogInformation("No attendance data found to archive");
                    return true;
                }

                // Group by course code and archive each course separately
                var courseGroups = new Dictionary<string, List<Dictionary<string, object>>>();
                
                foreach (var attendance in allAttendanceData)
                {
                    if (attendance.TryGetValue("CourseCode", out var courseCodeObj) && 
                        courseCodeObj is string courseCode)
                    {
                        if (!courseGroups.ContainsKey(courseCode))
                        {
                            courseGroups[courseCode] = new List<Dictionary<string, object>>();
                        }
                        courseGroups[courseCode].Add(attendance);
                    }
                }

                bool allSuccess = true;

                // Archive each course
                foreach (var courseGroup in courseGroups)
                {
                    var success = await ArchiveAndDeleteCourseAttendanceAsync(courseGroup.Key);
                    if (!success)
                    {
                        allSuccess = false;
                        _logger.LogError($"Failed to archive course {courseGroup.Key}");
                    }
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving all attendance data");
                return false;
            }
        }
private async Task AutoSignCourseRepAsync()
{
    try
    {
        bool success = true;
            
        if (success)
        {
            Console.WriteLine($"Course rep auto-signed successfully: {currentUserMatricNumber}");
        }
        else
        {
            Console.WriteLine($"Failed to auto-sign course rep: {currentUserMatricNumber}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error auto-signing course rep: {ex.Message}");
        // Don't throw - session should still start even if auto-sign fails
    }
}
        /// <summary>
        /// Get attendance statistics for a course
        /// </summary>
        public async Task<Dictionary<string, object>> GetCourseAttendanceStatsAsync(string courseCode)
        {
            try
            {
                var attendanceData = await GetCourseAttendanceEventsAsync(courseCode);
                var stats = new Dictionary<string, object>();

                if (!attendanceData.Any())
                {
                    return stats;
                }

                var totalSessions = 0;
                var totalStudents = 0;
                var totalScannedAttendances = 0;
                var onlineScans = 0;
                var offlineScans = 0;

                // Process each event
                foreach (var kvp in attendanceData)
                {
                    if (kvp.Key.StartsWith("Event_") && kvp.Value is JsonElement eventElement)
                    {
                        totalSessions++;
                        
                        if (eventElement.TryGetProperty("AttendanceRecords", out var recordsElement))
                        {
                            var sessionStudentCount = 0;
                            var sessionScannedCount = 0;
                            var sessionOnlineCount = 0;
                            var sessionOfflineCount = 0;

                            foreach (var record in recordsElement.EnumerateObject())
                            {
                                sessionStudentCount++;
                                
                                if (record.Value.TryGetProperty("HasScannedAttendance", out var hasScanned) && 
                                    hasScanned.GetBoolean())
                                {
                                    sessionScannedCount++;
                                    totalScannedAttendances++;
                                    
                                    if (record.Value.TryGetProperty("IsOnlineScan", out var isOnline) && 
                                        isOnline.GetBoolean())
                                    {
                                        sessionOnlineCount++;
                                        onlineScans++;
                                    }
                                    else
                                    {
                                        sessionOfflineCount++;
                                        offlineScans++;
                                    }
                                }
                            }

                            if (sessionStudentCount > totalStudents)
                            {
                                totalStudents = sessionStudentCount;
                            }
                        }
                    }
                }

                stats["CourseCode"] = courseCode;
                stats["TotalSessions"] = totalSessions;
                stats["TotalStudents"] = totalStudents;
                stats["TotalScannedAttendances"] = totalScannedAttendances;
                stats["OnlineScans"] = onlineScans;
                stats["OfflineScans"] = offlineScans;
                stats["AttendanceRate"] = totalStudents > 0 && totalSessions > 0 
                    ? Math.Round((double)totalScannedAttendances / (totalStudents * totalSessions) * 100, 2)
                    : 0.0;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting attendance statistics for course {courseCode}");
                return new Dictionary<string, object>();
            }
        }
    }

 
}
