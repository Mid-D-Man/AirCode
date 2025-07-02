using Microsoft.AspNetCore.Components;
using System.Text.Json;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Models.Supabase;
using AirCode.Services.Courses;
using AirCode.Services.Attendance;
using AirCode.Services.Firebase;
using Course = AirCode.Domain.Entities.Course;
namespace AirCode.Pages.Admin.Shared
{
    public partial class Reports : ComponentBase
    {
        [Inject] private ICourseService CourseService { get; set; } = default!;
        [Inject] private IAttendanceSessionService AttendanceSessionService { get; set; } = default!;
[Inject] private IFirestoreAttendanceService FirestoreAttendanceService { get; set; } = default!;
        // UI State
        private bool isLoading = false;
        private string errorMessage = string.Empty;
        private string reportJson = string.Empty;
        private bool showReport = false;

        // Form Data
        private LevelType selectedLevel = LevelType.Level100;
        private string selectedCourseCode = string.Empty;
        private List<Course> availableCourses = new();
        private List<Course> filteredCourses = new();

        // Report Data
        private AttendanceReport? currentReport;

        protected override async Task OnInitializedAsync()
        {
            await LoadCoursesAsync();
        }

        private async Task LoadCoursesAsync()
        {
            try
            {
                isLoading = true;
                errorMessage = string.Empty;
                availableCourses = await CourseService.GetAllCoursesAsync();
                FilterCoursesByLevel();
            }
            catch (Exception ex)
            {
                errorMessage = $"Error loading courses: {ex.Message}";
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void OnLevelChanged(ChangeEventArgs e)
        {
            if (Enum.TryParse<LevelType>(e.Value?.ToString(), out var level))
            {
                selectedLevel = level;
                selectedCourseCode = string.Empty;
                FilterCoursesByLevel();
                StateHasChanged();
            }
        }

        private void FilterCoursesByLevel()
        {
            filteredCourses = availableCourses
                .Where(c => c.Level == selectedLevel)
                .OrderBy(c => c.CourseCode)
                .ToList();
        }

        private void OnCourseChanged(ChangeEventArgs e)
        {
            selectedCourseCode = e.Value?.ToString() ?? string.Empty;
            StateHasChanged();
        }

        private async Task GenerateReportAsync()
        {
            if (string.IsNullOrEmpty(selectedCourseCode))
            {
                errorMessage = "Please select a course";
                return;
            }

            try
            {
                isLoading = true;
                errorMessage = string.Empty;
                showReport = false;

             var courseAttendanceData = await FirestoreAttendanceService
            .GetCourseAttendanceEventsAsync(selectedCourseCode);
        
        if (!courseAttendanceData.Any())
        {
            errorMessage = "No attendance sessions found for this course";
            return;
        }

        // Extract and transform Firestore events to session format
        var courseSessions = ExtractSessionsFromFirestoreData(courseAttendanceData);
        
        // Generate comprehensive report
        currentReport = GenerateAttendanceReport(studentsInCourse, courseSessions);
        
                // Convert to JSON for display
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                reportJson = JsonSerializer.Serialize(currentReport, options);
                
                showReport = true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error generating report: {ex.Message}";
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }
private List<FirestoreAttendanceSession> ExtractSessionsFromFirestoreData(
    Dictionary<string, object> firestoreData)
{
    var sessions = new List<FirestoreAttendanceSession>();
    
    foreach (var kvp in firestoreData)
    {
        if (kvp.Key.StartsWith("Event_") && kvp.Value is JsonElement eventElement)
        {
            var session = new FirestoreAttendanceSession
            {
                SessionId = eventElement.GetProperty("SessionId").GetString(),
                CourseCode = eventElement.GetProperty("CourseCode").GetString(),
                StartTime = eventElement.GetProperty("StartTime").GetDateTime(),
                Duration = eventElement.GetProperty("Duration").GetInt32(),
                AttendanceRecords = ExtractAttendanceRecords(eventElement)
            };
            sessions.Add(session);
        }
    }
    
    return sessions.OrderBy(s => s.StartTime).ToList();
}

private List<AttendanceRecord> ExtractAttendanceRecords(JsonElement eventElement)
{
    var records = new List<AttendanceRecord>();
    
    if (eventElement.TryGetProperty("AttendanceRecords", out var recordsElement))
    {
        foreach (var record in recordsElement.EnumerateObject())
        {
            var attendanceRecord = new AttendanceRecord
            {
                MatricNumber = record.Value.GetProperty("MatricNumber").GetString(),
                HasScannedAttendance = record.Value.GetProperty("HasScannedAttendance").GetBoolean(),
                ScanTime = record.Value.TryGetProperty("ScanTime", out var scanTime) && 
                          scanTime.ValueKind != JsonValueKind.Null 
                    ? scanTime.GetDateTime() 
                    : (DateTime?)null,
                IsOnlineScan = record.Value.GetProperty("IsOnlineScan").GetBoolean(),
                DeviceGUID = record.Value.TryGetProperty("DeviceGUID", out var deviceGuid) 
                    ? deviceGuid.GetString() 
                    : null
            };
            records.Add(attendanceRecord);
        }
    }
    
    return records;
}
        private AttendanceReport GenerateAttendanceReport(List<StudentCourse> studentsInCourse, List<SupabaseAttendanceSession> courseSessions)
        {
            var report = new AttendanceReport
            {
                CourseCode = selectedCourseCode,
                CourseLevel = selectedLevel,
                GeneratedAt = DateTime.UtcNow,
                TotalSessions = courseSessions.Count,
                StudentReports = new List<StudentAttendanceReport>()
            };

            foreach (var studentCourse in studentsInCourse)
            {
                var studentReport = new StudentAttendanceReport
                {
                    MatricNumber = studentCourse.StudentMatricNumber,
                    StudentLevel = studentCourse.StudentLevel,
                    SessionAttendance = new List<SessionAttendanceRecord>(),
                    TotalPresent = 0,
                    TotalAbsent = 0,
                    AttendancePercentage = 0.0
                };

                // Check each session for this student's attendance
                foreach (var session in courseSessions)
                {
                    var attendanceRecords = session.GetAttendanceRecords();
                    var studentRecord = attendanceRecords
                        .FirstOrDefault(ar => ar.MatricNumber == studentCourse.StudentMatricNumber);

                    var sessionRecord = new SessionAttendanceRecord
                    {
                        SessionId = session.SessionId,
                        SessionDate = session.StartTime,
                        Duration = session.Duration,
                        IsPresent = studentRecord?.HasScannedAttendance ?? false,
                        ScanTime = studentRecord?.ScanTime,
                        IsOnlineScan = studentRecord?.IsOnlineScan ?? false,
                        DeviceGUID = studentRecord?.DeviceGUID
                    };

                    studentReport.SessionAttendance.Add(sessionRecord);

                    if (sessionRecord.IsPresent)
                        studentReport.TotalPresent++;
                    else
                        studentReport.TotalAbsent++;
                }

                // Calculate attendance percentage
                if (courseSessions.Count > 0)
                {
                    studentReport.AttendancePercentage = 
                        Math.Round((double)studentReport.TotalPresent / courseSessions.Count * 100, 2);
                }

                report.StudentReports.Add(studentReport);
            }

            // Calculate overall statistics
            report.TotalStudentsEnrolled = studentsInCourse.Count;
            if (report.StudentReports.Any())
            {
                report.AverageAttendancePercentage = 
                    Math.Round(report.StudentReports.Average(sr => sr.AttendancePercentage), 2);
                report.StudentsWithPerfectAttendance = 
                    report.StudentReports.Count(sr => sr.AttendancePercentage == 100.0);
                report.StudentsWithPoorAttendance = 
                    report.StudentReports.Count(sr => sr.AttendancePercentage < 75.0);
            }

            return report;
        }

        private void ClearReport()
        {
            showReport = false;
            reportJson = string.Empty;
            currentReport = null;
            errorMessage = string.Empty;
            StateHasChanged();
        }

        private async Task ExportReportAsync()
        {
            if (currentReport == null) return;

            try
            {
                // For now, just copy to clipboard - PDF generation can be added later
                await Task.CompletedTask; // Placeholder for future PDF export functionality
                // TODO: Implement PDF generation using a WASM-compatible library
            }
            catch (Exception ex)
            {
                errorMessage = $"Export failed: {ex.Message}";
            }
        }
    }
    public class FirestoreAttendanceSession
{
    public string SessionId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int Duration { get; set; }
    public List<AttendanceRecord> AttendanceRecords { get; set; } = new();
    }

    // Report Data Models
    public class AttendanceReport
    {
        public string CourseCode { get; set; } = string.Empty;
        public LevelType CourseLevel { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalSessions { get; set; }
        public int TotalStudentsEnrolled { get; set; }
        public double AverageAttendancePercentage { get; set; }
        public int StudentsWithPerfectAttendance { get; set; }
        public int StudentsWithPoorAttendance { get; set; }
        public List<StudentAttendanceReport> StudentReports { get; set; } = new();
    }

    public class StudentAttendanceReport
    {
        public string MatricNumber { get; set; } = string.Empty;
        public LevelType StudentLevel { get; set; }
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public double AttendancePercentage { get; set; }
        public List<SessionAttendanceRecord> SessionAttendance { get; set; } = new();
    }

    public class SessionAttendanceRecord
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public int Duration { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? ScanTime { get; set; }
        public bool IsOnlineScan { get; set; }
        public string? DeviceGUID { get; set; }
    }
}
