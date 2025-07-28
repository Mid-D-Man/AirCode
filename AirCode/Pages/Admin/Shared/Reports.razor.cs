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
                showReport = false;
                StateHasChanged();
                
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
                ClearReport();
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
            ClearReport();
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

                var courseAttendanceData = await FirestoreAttendanceService.GetCourseAttendanceEventsAsync(selectedCourseCode);
                
                if (!courseAttendanceData.Any())
                {
                    errorMessage = "No attendance sessions found for this course";
                    return;
                }

                var attendanceEvents = ExtractAttendanceEvents(courseAttendanceData);
                
                if (!attendanceEvents.Any())
                {
                    errorMessage = "No valid attendance events found for this course";
                    return;
                }

                currentReport = GenerateAttendanceReport(studentsInCourse, attendanceEvents);
                
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

            foreach (var kvp in courseAttendanceData.Where(k => k.Key.StartsWith("Event_")))
            {
                try
                {
                    Dictionary<string, object> eventData = null;
                    
                    if (kvp.Value is JsonElement jsonElement)
                    {
                        eventData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                    }
                    else if (kvp.Value is Dictionary<string, object> dict)
                    {
                        eventData = dict;
                    }
                    else if (!string.IsNullOrEmpty(kvp.Value?.ToString()))
                    {
                        eventData = JsonSerializer.Deserialize<Dictionary<string, object>>(kvp.Value.ToString());
                    }

                    if (eventData != null)
                    {
                        var firebaseEvent = ParseFirebaseAttendanceEvent(eventData);
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

            return events.OrderBy(e => e.StartTime).ToList();
        }

        private FirebaseAttendanceEvent? ParseFirebaseAttendanceEvent(Dictionary<string, object> eventData)
        {
            try
            {
                var attendanceEvent = new FirebaseAttendanceEvent
                {
                    SessionId = eventData.GetValueOrDefault("SessionId")?.ToString() ?? string.Empty,
                    Theme = eventData.GetValueOrDefault("Theme")?.ToString() ?? string.Empty,
                    AttendanceRecords = new Dictionary<string, FirebaseAttendanceRecord>()
                };

                if (eventData.TryGetValue("StartTime", out var startTimeObj) && 
                    DateTime.TryParse(startTimeObj?.ToString(), out var startTime))
                {
                    attendanceEvent.StartTime = startTime;
                }

                if (eventData.TryGetValue("Duration", out var durationObj) && 
                    int.TryParse(durationObj?.ToString(), out var duration))
                {
                    attendanceEvent.Duration = duration;
                }

                if (eventData.TryGetValue("AttendanceRecords", out var recordsObj))
                {
                    Dictionary<string, object> recordsDict = ParseRecordsObject(recordsObj);

                    if (recordsDict != null)
                    {
                        foreach (var recordKvp in recordsDict)
                        {
                            var record = ParseFirebaseAttendanceRecord(recordKvp.Value);
                            if (record != null)
                            {
                                attendanceEvent.AttendanceRecords[recordKvp.Key] = record;
                            }
                        }
                    }
                }

                return string.IsNullOrEmpty(attendanceEvent.SessionId) || attendanceEvent.StartTime == default 
                    ? null : attendanceEvent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing Firebase attendance event: {ex.Message}");
                return null;
            }
        }

        private Dictionary<string, object>? ParseRecordsObject(object recordsObj)
        {
            return recordsObj switch
            {
                JsonElement recordsElement => JsonSerializer.Deserialize<Dictionary<string, object>>(recordsElement.GetRawText()),
                Dictionary<string, object> dict => dict,
                _ => null
            };
        }

        private FirebaseAttendanceRecord? ParseFirebaseAttendanceRecord(object recordObj)
        {
            try
            {
                Dictionary<string, object> recordData = recordObj switch
                {
                    JsonElement jsonElement => JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText()),
                    Dictionary<string, object> dict => dict,
                    _ => null
                };

                if (recordData == null) return null;

                var record = new FirebaseAttendanceRecord
                {
                    MatricNumber = recordData.GetValueOrDefault("MatricNumber")?.ToString() ?? string.Empty,
                    DeviceGUID = recordData.GetValueOrDefault("DeviceGUID")?.ToString()
                };

                if (recordData.TryGetValue("HasScannedAttendance", out var hasScannedObj) && 
                    bool.TryParse(hasScannedObj?.ToString(), out var hasScanned))
                {
                    record.HasScannedAttendance = hasScanned;
                }

                if (recordData.TryGetValue("ScanTime", out var scanTimeObj) && 
                    scanTimeObj != null && 
                    DateTime.TryParse(scanTimeObj.ToString(), out var scanTime))
                {
                    record.ScanTime = scanTime;
                }

                if (recordData.TryGetValue("IsOnlineScan", out var isOnlineObj) && 
                    bool.TryParse(isOnlineObj?.ToString(), out var isOnline))
                {
                    record.IsOnlineScan = isOnline;
                }

                return record;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing Firebase attendance record: {ex.Message}");
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
                    TotalAbsent = 0
                };

                foreach (var attendanceEvent in attendanceEvents)
                {
                    var studentRecord = attendanceEvent.AttendanceRecords
                        .GetValueOrDefault(studentCourse.StudentMatricNumber);

                    // Present: HasScannedAttendance == true
                    // Absent: No record OR HasScannedAttendance == false
                    bool isPresent = studentRecord?.HasScannedAttendance == true;

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

                    if (isPresent)
                        studentReport.TotalPresent++;
                    else
                        studentReport.TotalAbsent++;
                }

                // Fix attendance percentage calculation
                studentReport.AttendancePercentage = attendanceEvents.Count > 0 
                    ? Math.Round((double)studentReport.TotalPresent / attendanceEvents.Count * 100, 2)
                    : 0.0;

                report.StudentReports.Add(studentReport);
            }

            // Calculate statistics
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

        private async Task ExportReportAsync()
        {
            if (currentReport == null) return;
    
            try 
            {
                isLoading = true;
                StateHasChanged();

                var success = await PdfExportService.GenerateAttendanceReportPdfAsync(currentReport);
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
        #endregion
    }
}