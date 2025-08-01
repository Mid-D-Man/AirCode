using Microsoft.AspNetCore.Components;
using System.Text.Json;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Models.Firebase;
using AirCode.Services.Courses;
using AirCode.Services.Attendance;

namespace AirCode.Pages.Shared
{
    public class ClientStatsBase : ComponentBase
    {
        #region Injected Services
        [Inject] protected ICourseService CourseService { get; set; } = default!;
        [Inject] protected IFirestoreAttendanceService FirestoreAttendanceService { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        #endregion

        #region Parameters and Student Identity
        // Student identification - This should come from authentication context
        [Parameter] public string CurrentStudentMatric { get; set; } = "U21CYS1083";
        [Parameter] public LevelType CurrentStudentLevel { get; set; } = LevelType.Level100;
        #endregion

        #region Core Data Properties
        protected StudentCourse? StudentCourseData { get; set; }
        protected List<Course> EnrolledCourses { get; set; } = new();
        protected List<CourseAttendanceStats> CourseAttendanceStats { get; set; } = new();
        protected List<CourseAttendanceStats> FilteredCourseStats { get; set; } = new();
        protected Dictionary<string, List<FirebaseAttendanceEvent>> CourseAttendanceEvents { get; set; } = new();
        #endregion

        #region Modal Properties
        protected bool ShowCourseModal { get; set; } = false;
        protected Course? SelectedCourseDetails { get; set; }
        protected CourseAttendanceStats? SelectedCourseStats { get; set; }
        protected List<SessionAttendanceRecord> SelectedCourseAttendanceRecords { get; set; } = new();
        protected List<SessionAttendanceRecord> FilteredAttendanceRecords { get; set; } = new();
        #endregion

        #region UI State and Loading
        protected bool IsLoading { get; set; } = true;
        protected string ErrorMessage { get; set; } = string.Empty;
        #endregion

        #region Filtering Properties
        protected string SelectedSemesterFilter { get; set; } = "";
        protected string SelectedStatusFilter { get; set; } = "";
        protected string SelectedMonthFilter { get; set; } = "";
        protected string SelectedAttendanceFilter { get; set; } = "";
        #endregion

        #region Computed Properties for Overall Stats
        protected int TotalEnrolledCourses => CourseAttendanceStats?.Count(c => !c.IsCarryOver) ?? 0;
        protected int TotalCarryOverCourses => CourseAttendanceStats?.Count(c => c.IsCarryOver) ?? 0;
        protected int TotalCreditUnits => CourseAttendanceStats?.Where(c => !c.IsCarryOver).Sum(c => c.CreditUnits) ?? 0;
        protected int CarryOverCreditUnits => CourseAttendanceStats?.Where(c => c.IsCarryOver).Sum(c => c.CreditUnits) ?? 0;
        protected int TotalClassesAttended => CourseAttendanceStats?.Sum(c => c.ClassesAttended) ?? 0;
        protected int TotalClasses => CourseAttendanceStats?.Sum(c => c.TotalClasses) ?? 0;
        protected double OverallAttendanceRate => TotalClasses > 0 
            ? Math.Round((double)TotalClassesAttended / TotalClasses * 100, 1) 
            : 0;
        #endregion

        #region Component Lifecycle
        protected override async Task OnInitializedAsync()
        {
            await LoadDataAsync();
        }
        #endregion

        #region Data Loading Methods
        protected async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                StateHasChanged();

                // Load student course data
                StudentCourseData = await CourseService.GetStudentCoursesByMatricAsync(CurrentStudentMatric);
                
                if (StudentCourseData == null)
                {
                    ErrorMessage = "Student course data not found. Please ensure you are properly enrolled in courses.";
                    return;
                }

                // Load course details for enrolled courses
                await LoadEnrolledCoursesAsync();
                
                // Load attendance data for all enrolled courses
                await LoadAttendanceDataAsync();
                
                // Calculate attendance statistics
                await CalculateAttendanceStatisticsAsync();

                // Initialize filtered data
                FilterCourses();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        private async Task LoadEnrolledCoursesAsync()
        {
            EnrolledCourses.Clear();
            
            if (StudentCourseData?.StudentCoursesRefs?.Any() == true)
            {
                foreach (var courseRef in StudentCourseData.StudentCoursesRefs)
                {
                    try
                    {
                        var course = await CourseService.GetCourseByIdAsync(courseRef.CourseCode);
                        if (course != null)
                        {
                            EnrolledCourses.Add(course);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading course {courseRef.CourseCode}: {ex.Message}");
                    }
                }
            }
        }

        private async Task LoadAttendanceDataAsync()
        {
            CourseAttendanceEvents.Clear();

            foreach (var course in EnrolledCourses)
            {
                try
                {
                    var courseAttendanceData = await FirestoreAttendanceService.GetCourseAttendanceEventsAsync(course.CourseCode);
                    
                    if (courseAttendanceData?.Any() == true)
                    {
                        var attendanceEvents = ExtractAttendanceEvents(courseAttendanceData);
                        if (attendanceEvents.Any())
                        {
                            CourseAttendanceEvents[course.CourseCode] = attendanceEvents;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading attendance data for course {course.CourseCode}: {ex.Message}");
                }
            }
        }

        private async Task CalculateAttendanceStatisticsAsync()
        {
            CourseAttendanceStats.Clear();

            foreach (var course in EnrolledCourses)
            {
                var courseRef = StudentCourseData?.StudentCoursesRefs
                    .FirstOrDefault(cr => cr.CourseCode == course.CourseCode);
                
                if (courseRef == null) continue;

                // Get attendance events for this course
                var attendanceEvents = CourseAttendanceEvents.GetValueOrDefault(course.CourseCode, new List<FirebaseAttendanceEvent>());
                
                // Calculate student-specific attendance
                var attendanceStats = CalculateStudentAttendanceForCourse(course, courseRef, attendanceEvents);
                
                if (attendanceStats != null)
                {
                    CourseAttendanceStats.Add(attendanceStats);
                }
            }

            await Task.CompletedTask;
        }
        #endregion

        #region Attendance Calculation Methods
        private List<FirebaseAttendanceEvent> ExtractAttendanceEvents(Dictionary<string, object> courseAttendanceData)
        {
            var events = new List<FirebaseAttendanceEvent>();

            foreach (var kvp in courseAttendanceData.Where(k => k.Key.StartsWith("Event_")))
            {
                try
                {
                    Dictionary<string, object>? eventData = null;
                    
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
                        eventData = JsonSerializer.Deserialize<Dictionary<string, object>>(kvp.Value.ToString()!);
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
                    var recordsDict = ParseRecordsObject(recordsObj);

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
                Dictionary<string, object>? recordData = recordObj switch
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

        private CourseAttendanceStats? CalculateStudentAttendanceForCourse(
            Course course, 
            CourseRefrence courseRef, 
            List<FirebaseAttendanceEvent> attendanceEvents)
        {
            if (!attendanceEvents.Any()) return null;

            var studentAttendanceRecords = new List<SessionAttendanceRecord>();
            var totalPresent = 0;
            var totalAbsent = 0;

            foreach (var attendanceEvent in attendanceEvents)
            {
                var studentRecord = attendanceEvent.AttendanceRecords
                    .GetValueOrDefault(CurrentStudentMatric);

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

                studentAttendanceRecords.Add(sessionRecord);

                if (isPresent)
                    totalPresent++;
                else
                    totalAbsent++;
            }

            // Calculate attendance percentage
            var attendancePercentage = attendanceEvents.Count > 0 
                ? Math.Round((double)totalPresent / attendanceEvents.Count * 100, 2)
                : 0.0;

            // Calculate consecutive absences (from most recent sessions)
            var consecutiveAbsences = CalculateConsecutiveAbsences(studentAttendanceRecords);
            
            // Find last attendance date
            var lastAttendanceDate = studentAttendanceRecords
                .Where(r => r.IsPresent)
                .OrderByDescending(r => r.SessionDate)
                .FirstOrDefault()?.SessionDate;

            return new CourseAttendanceStats
            {
                CourseCode = course.CourseCode,
                CourseName = course.Name,
                DepartmentId = course.DepartmentId,
                Level = course.Level,
                Semester = course.Semester,
                CreditUnits = course.CreditUnits,
                AttendancePercentage = attendancePercentage,
                ClassesAttended = totalPresent,
                TotalClasses = attendanceEvents.Count,
                TotalAbsences = totalAbsent,
                ConsecutiveAbsences = consecutiveAbsences,
                LastAttendanceDate = lastAttendanceDate,
                IsCarryOver = courseRef.CourseEnrollmentStatus == CourseEnrollmentStatus.Carryover
            };
        }

        private int CalculateConsecutiveAbsences(List<SessionAttendanceRecord> records)
        {
            if (!records.Any()) return 0;

            var consecutiveCount = 0;

            // Start from the most recent record and count consecutive absences
            foreach (var record in records.OrderByDescending(r => r.SessionDate))
            {
                if (!record.IsPresent)
                {
                    consecutiveCount++;
                }
                else
                {
                    break; // Stop at first present record when going backwards
                }
            }

            return consecutiveCount;
        }
        #endregion

        #region Modal and Navigation Methods
        protected async Task ViewCourseDetails(string courseCode)
        {
            try
            {
                SelectedCourseDetails = await CourseService.GetCourseByIdAsync(courseCode);
                SelectedCourseStats = CourseAttendanceStats.FirstOrDefault(c => c.CourseCode == courseCode);
                
                // Generate attendance records for the selected course
                if (CourseAttendanceEvents.TryGetValue(courseCode, out var attendanceEvents))
                {
                    SelectedCourseAttendanceRecords = GenerateSessionAttendanceRecords(attendanceEvents);
                }
                else
                {
                    SelectedCourseAttendanceRecords = new List<SessionAttendanceRecord>();
                }
                
                FilteredAttendanceRecords = SelectedCourseAttendanceRecords.ToList();
                
                ShowCourseModal = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading course details: {ex.Message}";
            }
        }

        private List<SessionAttendanceRecord> GenerateSessionAttendanceRecords(List<FirebaseAttendanceEvent> attendanceEvents)
        {
            var records = new List<SessionAttendanceRecord>();

            foreach (var attendanceEvent in attendanceEvents)
            {
                var studentRecord = attendanceEvent.AttendanceRecords
                    .GetValueOrDefault(CurrentStudentMatric);

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

                records.Add(sessionRecord);
            }

            return records.OrderByDescending(r => r.SessionDate).ToList();
        }

        protected void CloseCourseModal()
        {
            ShowCourseModal = false;
            SelectedCourseDetails = null;
            SelectedCourseStats = null;
            SelectedCourseAttendanceRecords.Clear();
            FilteredAttendanceRecords.Clear();
            SelectedMonthFilter = "";
            SelectedAttendanceFilter = "";
            StateHasChanged();
        }
        #endregion

        #region Filtering Methods
        protected void FilterCourses()
        {
            FilteredCourseStats = CourseAttendanceStats.Where(course =>
            {
                var matchesSemester = string.IsNullOrEmpty(SelectedSemesterFilter) ||
                                    course.Semester.ToString() == SelectedSemesterFilter;

                var matchesStatus = string.IsNullOrEmpty(SelectedStatusFilter) ||
                                  (SelectedStatusFilter == "Enrolled" && !course.IsCarryOver) ||
                                  (SelectedStatusFilter == "Carryover" && course.IsCarryOver);

                return matchesSemester && matchesStatus;
            }).ToList();

            StateHasChanged();
        }

        protected void ClearFilters()
        {
            SelectedSemesterFilter = "";
            SelectedStatusFilter = "";
            FilterCourses();
        }

        protected void FilterAttendanceRecords()
        {
            FilteredAttendanceRecords = SelectedCourseAttendanceRecords.Where(record =>
            {
                var matchesMonth = string.IsNullOrEmpty(SelectedMonthFilter) ||
                                 record.SessionDate.ToString("yyyy-MM") == SelectedMonthFilter;

                var matchesAttendance = string.IsNullOrEmpty(SelectedAttendanceFilter) ||
                                      (SelectedAttendanceFilter == "Present" && record.IsPresent) ||
                                      (SelectedAttendanceFilter == "Absent" && !record.IsPresent);

                return matchesMonth && matchesAttendance;
            }).ToList();

            StateHasChanged();
        }

        protected Dictionary<string, string> GetAvailableMonths()
        {
            return SelectedCourseAttendanceRecords
                .Select(r => r.SessionDate)
                .GroupBy(d => d.ToString("yyyy-MM"))
                .OrderByDescending(g => g.Key)
                .ToDictionary(g => g.Key, g => DateTime.ParseExact(g.Key, "yyyy-MM", null).ToString("MMMM yyyy"));
        }
        #endregion

        #region Helper and Utility Methods
        protected void LoadMoreRecords()
        {
            // This would load more records if implementing pagination
            // For now, just show all records
        }

        protected string GetAttendanceClass(double percentage)
        {
            return percentage switch
            {
                >= 85 => "excellent",
                >= 75 => "good",
                >= 65 => "average",
                _ => "poor"
            };
        }

        protected string GetAttendanceLabel(double percentage)
        {
            return percentage switch
            {
                >= 85 => "Excellent",
                >= 75 => "Good",
                >= 65 => "Average",
                _ => "Poor"
            };
        }

        protected string GetLevelDisplay(LevelType level)
        {
            return level switch
            {
                LevelType.Level100 => "100L",
                LevelType.Level200 => "200L",
                LevelType.Level300 => "300L",
                LevelType.Level400 => "400L",
                LevelType.Level500 => "500L",
                LevelType.LevelExtra => "Extra",
                _ => "Unknown"
            };
        }

        protected string GetAttendancePerformanceClass(double percentage)
        {
            return percentage switch
            {
                >= 90 => "excellent",
                >= 75 => "good", 
                >= 50 => "poor",
                _ => "critical"
            };
        }

        protected void ClearError()
        {
            ErrorMessage = string.Empty;
            StateHasChanged();
        }
        #endregion
    }
}