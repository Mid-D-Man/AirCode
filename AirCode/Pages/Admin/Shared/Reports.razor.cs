using Microsoft.AspNetCore.Components;
using System.Text.Json;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Models.Firebase;
using AirCode.Services.Courses;
using AirCode.Services.Attendance;
using AirCode.Services.Exports;
using Course = AirCode.Domain.Entities.Course;

namespace AirCode.Pages.Admin.Shared
{
    public partial class Reports : ComponentBase
    {
        [Inject] private ICourseService CourseService { get; set; } = default!;
        [Inject] private IFirestoreAttendanceService FirestoreAttendanceService { get; set; } = default!;
        [Inject] private IPdfExportService PdfExportService { get; set; } = default!;

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
        private const string K_SchoolName = "AirForce Institute of Technology Kaduna";
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
                showReport = false; // Ensure we're in course loading state
                StateHasChanged(); // Update UI to show "Loading Courses..."
                
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
                showReport = false; // Clear any existing report
                currentReport = null;
                reportJson = string.Empty;
                errorMessage = string.Empty;
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
            showReport = false; // Clear any existing report when course changes
            currentReport = null;
            reportJson = string.Empty;
            errorMessage = string.Empty;
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

                // Get students enrolled in the selected course
                var allStudentCourses = await CourseService.GetAllStudentCoursesAsync();
                var studentsInCourse = allStudentCourses
                    .Where(sc => sc.StudentCoursesRefs.Any(cr => 
                        cr.CourseCode == selectedCourseCode && 
                        cr.CourseEnrollmentStatus == CourseEnrollmentStatus.Enrolled))
                    .ToList();

                if (!studentsInCourse.Any())
                {
                    errorMessage = "No students found enrolled in this course";
                    return;
                }

                // Get attendance events from Firebase for the selected course
                var courseAttendanceData = await FirestoreAttendanceService.GetCourseAttendanceEventsAsync(selectedCourseCode);
                
                if (!courseAttendanceData.Any())
                {
                    errorMessage = "No attendance sessions found for this course";
                    return;
                }

                // Extract and parse attendance events
                var attendanceEvents = ExtractAttendanceEvents(courseAttendanceData);
                
                if (!attendanceEvents.Any())
                {
                    errorMessage = "No valid attendance events found for this course";
                    return;
                }

                // Generate comprehensive report
                currentReport = GenerateAttendanceReport(studentsInCourse, attendanceEvents);
                
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

   private List<FirebaseAttendanceEvent> ExtractAttendanceEvents(Dictionary<string, object> courseAttendanceData)
   {
       var events = new List<FirebaseAttendanceEvent>();
   
       foreach (var kvp in courseAttendanceData)
       {
           if (!kvp.Key.StartsWith("Event_")) continue;
   
           try
           {
               Dictionary<string, object> eventData = null;
               
               if (kvp.Value is JsonElement jsonElement)
               {
                   var rawJson = jsonElement.GetRawText();
                   eventData = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson);
               }
               else if (kvp.Value is Dictionary<string, object> dict)
               {
                   eventData = dict;
               }
               else
               {
                   var jsonString = kvp.Value?.ToString();
                   if (!string.IsNullOrEmpty(jsonString))
                   {
                       eventData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                   }
               }
   
               if (eventData != null)
               {
                   var firebaseEvent = ParseFirebaseAttendanceEventFromDict(eventData);
                   if (firebaseEvent != null)
                   {
                       events.Add(firebaseEvent);
                   }
               }
           }
           catch (Exception ex)
           {
               Console.WriteLine($"Error parsing event {kvp.Key}: {ex.Message}");
           }
       }
   
       // FIXED: Sequential ordering by StartTime (creation date)
       return events.OrderBy(e => e.StartTime).ToList();
   }

private FirebaseAttendanceEvent? ParseFirebaseAttendanceEventFromDict(Dictionary<string, object> eventData)
{
    try
    {
        var attendanceEvent = new FirebaseAttendanceEvent();

        // Parse SessionId
        if (eventData.TryGetValue("SessionId", out var sessionIdObj))
            attendanceEvent.SessionId = sessionIdObj?.ToString() ?? string.Empty;

        // Parse StartTime
        if (eventData.TryGetValue("StartTime", out var startTimeObj))
        {
            if (DateTime.TryParse(startTimeObj?.ToString(), out var startTime))
                attendanceEvent.StartTime = startTime;
        }

        // Parse Duration
        if (eventData.TryGetValue("Duration", out var durationObj))
        {
            if (int.TryParse(durationObj?.ToString(), out var duration))
                attendanceEvent.Duration = duration;
        }

        // Parse Theme
        if (eventData.TryGetValue("Theme", out var themeObj))
            attendanceEvent.Theme = themeObj?.ToString() ?? string.Empty;

        // Parse AttendanceRecords
        if (eventData.TryGetValue("AttendanceRecords", out var recordsObj))
        {
            attendanceEvent.AttendanceRecords = new Dictionary<string, FirebaseAttendanceRecord>();

            Dictionary<string, object> recordsDict = null;

            if (recordsObj is JsonElement recordsElement)
            {
                recordsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(recordsElement.GetRawText());
            }
            else if (recordsObj is Dictionary<string, object> dict)
            {
                recordsDict = dict;
            }

            if (recordsDict != null)
            {
                foreach (var recordKvp in recordsDict)
                {
                    var record = ParseFirebaseAttendanceRecordFromObject(recordKvp.Value);
                    if (record != null)
                    {
                        attendanceEvent.AttendanceRecords[recordKvp.Key] = record;
                    }
                }
            }
        }

        // Validate required fields
        if (string.IsNullOrEmpty(attendanceEvent.SessionId) || attendanceEvent.StartTime == default)
        {
            Console.WriteLine($"Invalid event data - missing SessionId or StartTime");
            return null;
        }

        return attendanceEvent;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing Firebase attendance event: {ex.Message}");
        return null;
    }
}

private FirebaseAttendanceRecord? ParseFirebaseAttendanceRecordFromObject(object recordObj)
{
    try
    {
        Dictionary<string, object> recordData = null;

        if (recordObj is JsonElement jsonElement)
        {
            recordData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
        }
        else if (recordObj is Dictionary<string, object> dict)
        {
            recordData = dict;
        }

        if (recordData == null) 
        {
            Console.WriteLine("Record data is null");
            return null;
        }

        Console.WriteLine($"Parsing record with keys: {string.Join(", ", recordData.Keys)}");

        var record = new FirebaseAttendanceRecord();

        if (recordData.TryGetValue("MatricNumber", out var matricObj))
        {
            record.MatricNumber = matricObj?.ToString() ?? string.Empty;
            Console.WriteLine($"MatricNumber: {record.MatricNumber}");
        }

        if (recordData.TryGetValue("HasScannedAttendance", out var hasScannedObj))
        {
            if (bool.TryParse(hasScannedObj?.ToString(), out var hasScanned))
            {
                record.HasScannedAttendance = hasScanned;
                Console.WriteLine($"HasScannedAttendance: {record.HasScannedAttendance}");
            }
        }

        if (recordData.TryGetValue("ScanTime", out var scanTimeObj) && scanTimeObj != null)
        {
            if (DateTime.TryParse(scanTimeObj.ToString(), out var scanTime))
            {
                record.ScanTime = scanTime;
                Console.WriteLine($"ScanTime: {record.ScanTime}");
            }
        }

        if (recordData.TryGetValue("IsOnlineScan", out var isOnlineObj))
        {
            if (bool.TryParse(isOnlineObj?.ToString(), out var isOnline))
            {
                record.IsOnlineScan = isOnline;
                Console.WriteLine($"IsOnlineScan: {record.IsOnlineScan}");
            }
        }

        if (recordData.TryGetValue("DeviceGUID", out var deviceObj))
        {
            record.DeviceGUID = deviceObj?.ToString();
            Console.WriteLine($"DeviceGUID: {record.DeviceGUID}");
        }

        return record;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing Firebase attendance record: {ex.Message}");
        return null;
    }
}

        

        private FirebaseAttendanceRecord? ParseFirebaseAttendanceRecord(JsonElement recordElement)
        {
            try
            {
                var record = new FirebaseAttendanceRecord();

                if (recordElement.TryGetProperty("MatricNumber", out var matricNumber))
                    record.MatricNumber = matricNumber.GetString() ?? string.Empty;

                if (recordElement.TryGetProperty("HasScannedAttendance", out var hasScanned))
                    record.HasScannedAttendance = hasScanned.GetBoolean();

                if (recordElement.TryGetProperty("ScanTime", out var scanTime) && 
                    scanTime.ValueKind != JsonValueKind.Null)
                    record.ScanTime = scanTime.GetDateTime();

                if (recordElement.TryGetProperty("IsOnlineScan", out var isOnline))
                    record.IsOnlineScan = isOnline.GetBoolean();

                if (recordElement.TryGetProperty("DeviceGUID", out var deviceGuid))
                    record.DeviceGUID = deviceGuid.GetString();

                return record;
            }
            catch
            {
                return null;
            }
        }

      private AttendanceReport GenerateAttendanceReport(List<StudentCourse> studentsInCourse, List<FirebaseAttendanceEvent> attendanceEvents)
{
    var report = new AttendanceReport
    {
        CourseCode = selectedCourseCode,
        CourseLevel = selectedLevel,
        GeneratedAt = DateTime.UtcNow,
        TotalSessions = attendanceEvents.Count,
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

        // Process attendance events in sequential order
        foreach (var attendanceEvent in attendanceEvents)
        {
            var studentRecord = attendanceEvent.AttendanceRecords
                .GetValueOrDefault(studentCourse.StudentMatricNumber);

            bool isPresent = studentRecord?.HasScannedAttendance ?? false;
            bool hasRecord = studentRecord != null;

            var sessionRecord = new SessionAttendanceRecord
            {
                SessionId = attendanceEvent.SessionId,
                SessionDate = attendanceEvent.StartTime,
                Duration = attendanceEvent.Duration,
                IsPresent = isPresent,
                ScanTime = studentRecord?.ScanTime,
                IsOnlineScan = studentRecord?.IsOnlineScan ?? false,
                DeviceGUID = studentRecord?.DeviceGUID
            };

            studentReport.SessionAttendance.Add(sessionRecord);

            // FIXED: Proper attendance counting
            if (hasRecord)
            {
                if (isPresent)
                {
                    studentReport.TotalPresent++;
                }
                else
                {
                    studentReport.TotalAbsent++;
                }
            }
            else
            {
                // No record = counted as absent for enrolled students
                studentReport.TotalAbsent++;
            }
        }

        // FIXED: Calculate percentage based on total sessions
        var totalSessionsForCalculation = attendanceEvents.Count;
        if (totalSessionsForCalculation > 0)
        {
            studentReport.AttendancePercentage = 
                Math.Round((double)studentReport.TotalPresent / totalSessionsForCalculation * 100, 2);
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

       #region Helper Methods
       private string GetLevelClass(LevelType level)
       {
           return level switch
           {
               LevelType.Level100 => "level-100",
               LevelType.Level200 => "level-200", 
               LevelType.Level300 => "level-300",
               LevelType.Level400 => "level-400",
               _ => "level-default"
           };
       }
       private void ClearError()
       {
           errorMessage = string.Empty;
           StateHasChanged();
       }
       private void ClearReport()
       {
           showReport = false;
           reportJson = string.Empty;
           currentReport = null;
           errorMessage = string.Empty;
           selectedCourseCode = string.Empty; // Reset course selection
           StateHasChanged();
       }
       private string GetAttendancePerformanceClass(double percentage)
       {
           return percentage switch
           {
               >= 90 => "excellent",
               >= 75 => "good", 
               >= 50 => "poor",
               _ => "critical"
           };
       }
       // Method to get attendance status text
       private string GetAttendanceStatusText(double percentage)
       {
           return percentage switch
           {
               >= 90 => "Excellent",
               >= 75 => "Good",
               >= 50 => "Poor", 
               _ => "Critical"
           };
       }

       // Method to get attendance status icon
       private string GetAttendanceStatusIcon(double percentage)
       {
           return percentage switch
           {
               >= 90 => "bi-star-fill",
               >= 75 => "bi-check-circle",
               >= 50 => "bi-exclamation-triangle",
               _ => "bi-x-octagon"
           };
       }

       // Method to format percentage for display
       private string FormatPercentage(double percentage)
       {
           return percentage.ToString("F1") + "%";
       }

       // Method to get summary card color class
       private string GetSummaryCardClass(string cardType)
       {
           return cardType.ToLower() switch
           {
               "students" => "primary",
               "sessions" => "info", 
               "average" => "success",
               "perfect" => "warning",
               "poor" => "danger",
               _ => "primary"
           };
       }
        private async Task ExportReportAsync()
        {
            if (currentReport == null) return;
    
            try 
            {
                isLoading = true;
                StateHasChanged(); // Show exporting state

                var success = await PdfExportService.GenerateAttendanceReportPdfAsync(currentReport,K_SchoolName);
                if (!success) errorMessage = "PDF export failed.";
            }
            catch (Exception ex) 
            {
                errorMessage = $"Export failed: {ex.Message}";
            }
            finally 
            {
                isLoading = false;
                StateHasChanged();
            }
        }
        
        #endregion
    }

}
