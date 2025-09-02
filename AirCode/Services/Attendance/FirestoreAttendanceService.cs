
using System.Text.Json;
using AirCode.Models.Supabase;
using AirCode.Services.Courses;
using AirCode.Services.Firebase;
using AirCode.Domain.ValueObjects; 

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
/// Create Firebase attendance event without pre-populating students
/// </summary>
public async Task<bool> CreateAttendanceEventAsync(string sessionId, string courseCode, string courseName, 
    DateTime startTime, int duration, string theme)
{
    try
    {
        // Calculate end time
        var endTime = startTime.AddMinutes(duration);

        // Create minimal attendance event data without pre-populating students
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
            AttendanceRecords = new Dictionary<string, object>() // Start with empty records
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
/// <summary>
/// Optimized course representative retrieval with efficient data access pattern
/// </summary>
public async Task<CourseRepSkeletonUser> GetCourseRepByCourseAsync(string courseCode)
{
    try
    {
        // Step 1: Get course level efficiently
        var courseLevel = await GetCourseLevelAsync(courseCode);
        if (courseLevel == null)
        {
            _logger.LogWarning($"Course {courseCode} not found or level not determined");
            return null;
        }

        // Step 2: Batch retrieve both admin and student data
        var (courseRepDoc, studentDoc) = await GetCourseRepAndStudentDataAsync();
        
        if (courseRepDoc?.Ids == null || !courseRepDoc.Ids.Any())
        {
            _logger.LogWarning("No course representatives found in admin document");
            return null;
        }

        // Step 3: Find matching course rep with optimized lookup
        var matchingCourseRep = await FindCourseRepByLevelAsync(courseRepDoc.Ids, studentDoc, courseLevel);
        
        if (matchingCourseRep == null)
        {
            _logger.LogWarning($"No course representative found for level {courseLevel}");
        }

        return matchingCourseRep;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error getting course representative for course {courseCode}");
        return null;
    }
}

/// <summary>
/// Efficient course level retrieval with caching consideration
/// </summary>
private async Task<string> GetCourseLevelAsync(string courseCode)
{
    var allStudentCourses = await _courseService.GetAllStudentCoursesAsync();
    return allStudentCourses
        .SelectMany(sc => sc.GetEnrolledCourses())
        .FirstOrDefault(c => c.CourseCode == courseCode)?.Level.ToString();
}

/// <summary>
/// Batch retrieval of course rep and student data to minimize Firebase calls
/// </summary>
private async Task<(CourseRepAdminDocument courseRepDoc, StudentLevelDocument studentDoc)> GetCourseRepAndStudentDataAsync()
{
    var courseRepTask = _firestoreService.GetDocumentAsync<CourseRepAdminDocument>(
        "VALID_ADMIN_IDS", "CourseRepAdminIdsDoc");
    
    var studentTask = _firestoreService.GetDocumentAsync<StudentLevelDocument>(
        "STUDENTS_MATRICULATION_NUMBERS", "students");

    await Task.WhenAll(courseRepTask, studentTask);
    
    return (await courseRepTask, await studentTask);
}

/// <summary>
/// Optimized course rep matching with student skeleton level lookup
/// </summary>
private async Task<CourseRepSkeletonUser> FindCourseRepByLevelAsync(
    List<CourseRepAdminInfo> courseRepAdmins, 
    StudentLevelDocument studentDoc, 
    string targetLevel)
{
    // Create lookup dictionary for efficient student skeleton access
    var studentSkeletonLookup = studentDoc?.ValidStudentMatricNumbers?
        .ToDictionary(s => s.MatricNumber, s => s) ?? new Dictionary<string, StudentSkeletonUser>();

    foreach (var adminData in courseRepAdmins)
    {
        // Primary: Check if student skeleton exists and has level
        if (studentSkeletonLookup.TryGetValue(adminData.MatricNumber, out var studentSkeleton))
        {
            if (studentSkeleton.Level == targetLevel)
            {
                return new CourseRepSkeletonUser
                {
                    AdminInfo = adminData,
                    StudentInfo = studentSkeleton
                };
            }
        }
        else
        {
            // Fallback: Use matric number pattern analysis
            var derivedLevel = GetStudentLevelFromMatricNumber(adminData.MatricNumber);
            if (derivedLevel == targetLevel)
            {
                var fallbackStudentInfo = new StudentSkeletonUser
                {
                    MatricNumber = adminData.MatricNumber,
                    Level = derivedLevel,
                    IsCurrentlyInUse = false,
                    CurrentUserId = ""
                };

                return new CourseRepSkeletonUser
                {
                    AdminInfo = adminData,
                    StudentInfo = fallbackStudentInfo
                };
            }
        }
    }

    return null;
}

/// <summary>
/// Fallback level calculation from matric number pattern
/// </summary>
private string GetStudentLevelFromMatricNumber(string matricNumber)
{
    if (matricNumber.Length >= 4)
    {
        var yearPrefix = matricNumber.Substring(0, 4);
        var currentYear = DateTime.Now.Year;
        
        if (int.TryParse(yearPrefix, out var admissionYear))
        {
            var academicLevel = currentYear - admissionYear + 1;
            return Math.Max(1, Math.Min(academicLevel, 7)).ToString(); // Clamp to valid range
        }
    }
    
    return "1"; // Default level
}

/// <summary>
/// Enhanced auto-sign with course rep lookup integration - now adds to Supabase instead of Firebase
/// </summary>
public async Task<bool> AutoSignCourseRepAsync(string sessionId, string courseCode)
{
    try
    {
        // Get course rep efficiently
        var courseRep = await GetCourseRepByCourseAsync(courseCode);
        if (courseRep?.StudentInfo?.MatricNumber == null)
        {
            _logger.LogWarning($"No course representative found for auto-signing in course {courseCode}");
            return false;
        }

        // Get the Supabase session to add the attendance record
        var session = await _attendanceSessionService.GetSessionByIdAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning($"Session {sessionId} not found for course rep auto-signing");
            return false;
        }

        // Get existing attendance records
        var existingRecords = session.GetAttendanceRecords();
        
        // Check if course rep already signed
        var existingRecord = existingRecords.FirstOrDefault(r => r.MatricNumber == courseRep.StudentInfo.MatricNumber);
        if (existingRecord != null && existingRecord.HasScannedAttendance)
        {
            _logger.LogInformation($"Course rep {courseRep.StudentInfo.MatricNumber} already signed for session {sessionId}");
            return true;
        }

        // Add or update course rep attendance record
        if (existingRecord != null)
        {
            existingRecord.HasScannedAttendance = true;
            existingRecord.ScanTime = DateTime.UtcNow;
            existingRecord.IsOnlineScan = true;
        }
        else
        {
            existingRecords.Add(new AttendanceRecord
            {
                MatricNumber = courseRep.StudentInfo.MatricNumber,
                HasScannedAttendance = true,
                ScanTime = DateTime.UtcNow,
                IsOnlineScan = true,
                DeviceGUID = "AUTO_SIGN_COURSE_REP"
            });
        }

        // Update session with new attendance records
        session.SetAttendanceRecords(existingRecords);
        session.UpdatedAt = DateTime.UtcNow;
        await _attendanceSessionService.UpdateSessionAsync(session);

        _logger.LogInformation($"Course rep auto-signed: {courseRep.StudentInfo.MatricNumber} for course {courseCode}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error auto-signing course rep for course {courseCode}");
        return false;
    }
}

/// <summary>
/// Integrated attendance event creation with automatic course rep signing
/// </summary>
public async Task<bool> CreateAttendanceEventWithCourseRepAsync(string sessionId, string courseCode, 
    string courseName, DateTime startTime, int duration, string theme)
{
    try
    {
        // Create standard attendance event
        var eventCreated = await CreateAttendanceEventAsync(sessionId, courseCode, courseName, startTime, duration, theme);
        
        if (!eventCreated)
        {
            return false;
        }

        // Auto-sign course rep
        var courseRepSigned = await AutoSignCourseRepAsync(sessionId, courseCode);
        
        if (!courseRepSigned)
        {
            _logger.LogWarning($"Attendance event created but course rep auto-sign failed for course {courseCode}");
        }

        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error creating attendance event with course rep auto-sign for course {courseCode}");
        return false;
    }
}
/// <summary>
/// Manually sign student attendance by admin/instructor - now adds to Supabase instead of Firebase directly
/// </summary>
public async Task<bool> ManualSignAttendanceAsync(string sessionId, string courseCode, 
    string studentMatricNumber, bool isManualSign = true)
{
    try
    {
        // Verify student is enrolled in this course
        var allStudentCourses = await _courseService.GetAllStudentCoursesAsync();
        var isStudentEnrolled = allStudentCourses
            .Any(sc => sc.StudentMatricNumber == studentMatricNumber && 
                      sc.GetEnrolledCourses().Any(cr => cr.CourseCode == courseCode));

        if (!isStudentEnrolled)
        {
            _logger.LogWarning($"Student {studentMatricNumber} not enrolled in course {courseCode}");
            return false;
        }

        // Get the Supabase session to add the attendance record
        var session = await _attendanceSessionService.GetSessionByIdAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning($"Session {sessionId} not found for manual attendance signing");
            return false;
        }

        // Get existing attendance records
        var existingRecords = session.GetAttendanceRecords();
        
        // Check if student already signed
        var existingRecord = existingRecords.FirstOrDefault(r => r.MatricNumber == studentMatricNumber);
        if (existingRecord != null && existingRecord.HasScannedAttendance)
        {
            _logger.LogInformation($"Student {studentMatricNumber} already signed for session {sessionId}");
            return true;
        }

        // Add or update student attendance record
        if (existingRecord != null)
        {
            existingRecord.HasScannedAttendance = true;
            existingRecord.ScanTime = DateTime.UtcNow;
            existingRecord.IsOnlineScan = false; // Manual sign is considered offline
        }
        else
        {
            existingRecords.Add(new AttendanceRecord
            {
                MatricNumber = studentMatricNumber,
                HasScannedAttendance = true,
                ScanTime = DateTime.UtcNow,
                IsOnlineScan = false, // Manual sign is considered offline
                DeviceGUID = "MANUAL_SIGN_ADMIN"
            });
        }

        // Update session with new attendance records
        session.SetAttendanceRecords(existingRecords);
        session.UpdatedAt = DateTime.UtcNow;
        await _attendanceSessionService.UpdateSessionAsync(session);

        _logger.LogInformation($"Successfully manually signed attendance for student {studentMatricNumber} in session {sessionId}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error manually signing attendance for student {studentMatricNumber} in session {sessionId}");
        return false;
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
