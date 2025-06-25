using Microsoft.AspNetCore.Components;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Services.Courses;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AirCode.Pages.Shared
{
    public class ClientStatsBase : ComponentBase
    {
        [Inject] protected ICourseService CourseService { get; set; }
        [Inject] protected NavigationManager Navigation { get; set; }

        // Student identification - This should come from authentication context
        [Parameter] public string CurrentStudentMatric { get; set; } = "U21CYS1083";
        [Parameter] public LevelType CurrentStudentLevel { get; set; } = LevelType.Level100;

        // Data properties
        protected StudentCourse StudentCourseData { get; set; }
        protected List<Course> EnrolledCourses { get; set; } = new();
        protected List<CourseAttendanceStats> CourseAttendanceStats { get; set; } = new();
        protected List<CourseAttendanceStats> FilteredCourseStats { get; set; } = new();
        protected List<AttendanceRecord> AllAttendanceRecords { get; set; } = new();

        // Modal properties
        protected bool ShowCourseModal { get; set; } = false;
        protected Course SelectedCourseDetails { get; set; }
        protected CourseAttendanceStats SelectedCourseStats { get; set; }
        protected List<AttendanceRecord> SelectedCourseAttendanceRecords { get; set; } = new();
        protected List<AttendanceRecord> FilteredAttendanceRecords { get; set; } = new();

        // Loading and error states
        protected bool IsLoading { get; set; } = true;
        protected string ErrorMessage { get; set; }

        // Filtering properties
        protected string SelectedSemesterFilter { get; set; } = "";
        protected string SelectedStatusFilter { get; set; } = "";
        protected string SelectedMonthFilter { get; set; } = "";
        protected string SelectedAttendanceFilter { get; set; } = "";

        // Computed properties for overall stats
        protected int TotalEnrolledCourses => CourseAttendanceStats?.Count(c => !c.IsCarryOver) ?? 0;
        protected int TotalCarryOverCourses => CourseAttendanceStats?.Count(c => c.IsCarryOver) ?? 0;
        protected int TotalCreditUnits => CourseAttendanceStats?.Where(c => !c.IsCarryOver).Sum(c => c.CreditUnits) ?? 0;
        protected int CarryOverCreditUnits => CourseAttendanceStats?.Where(c => c.IsCarryOver).Sum(c => c.CreditUnits) ?? 0;
        protected int TotalClassesAttended => CourseAttendanceStats?.Sum(c => c.ClassesAttended) ?? 0;
        protected int TotalClasses => CourseAttendanceStats?.Sum(c => c.TotalClasses) ?? 0;
        protected double OverallAttendanceRate => TotalClasses > 0 
            ? Math.Round((double)TotalClassesAttended / TotalClasses * 100, 1) 
            : 0;

        protected override async Task OnInitializedAsync()
        {
            await LoadDataAsync();
        }

        protected async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
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
                
                // Load attendance data
                await LoadAttendanceDataAsync();
                
                // Calculate attendance statistics
                CalculateAttendanceStatistics();

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
            // TODO: Replace with actual attendance service call
            // This should load real attendance data from the database
            AllAttendanceRecords = await GetAttendanceRecordsFromService();
        }

        private async Task<List<AttendanceRecord>> GetAttendanceRecordsFromService()
        {
            // TODO: Implement actual service call to get attendance records
            // For now, returning empty list - replace with actual implementation
            await Task.CompletedTask;
            return new List<AttendanceRecord>();
        }

        private void CalculateAttendanceStatistics()
        {
            CourseAttendanceStats.Clear();

            foreach (var course in EnrolledCourses)
            {
                var courseRef = StudentCourseData.StudentCoursesRefs
                    .FirstOrDefault(cr => cr.CourseCode == course.CourseCode);
                
                if (courseRef == null) continue;

                var courseAttendanceRecords = AllAttendanceRecords
                    .Where(ar => ar.CourseCode == course.CourseCode)
                    .OrderBy(ar => ar.Date)
                    .ToList();

                var totalClasses = courseAttendanceRecords.Count;
                var attendedClasses = courseAttendanceRecords.Count(ar => ar.IsPresent);
                var attendancePercentage = totalClasses > 0 
                    ? Math.Round((double)attendedClasses / totalClasses * 100, 1) 
                    : 0;

                // Calculate consecutive absences
                var consecutiveAbsences = CalculateConsecutiveAbsences(courseAttendanceRecords);
                var totalAbsences = courseAttendanceRecords.Count(ar => !ar.IsPresent);
                var lastAttendanceDate = courseAttendanceRecords.LastOrDefault(ar => ar.IsPresent)?.Date;

                CourseAttendanceStats.Add(new CourseAttendanceStats
                {
                    CourseCode = course.CourseCode,
                    CourseName = course.Name,
                    DepartmentId = course.DepartmentId,
                    Level = course.Level,
                    Semester = course.Semester,
                    CreditUnits = course.CreditUnits,
                    AttendancePercentage = attendancePercentage,
                    ClassesAttended = attendedClasses,
                    TotalClasses = totalClasses,
                    TotalAbsences = totalAbsences,
                    ConsecutiveAbsences = consecutiveAbsences,
                    LastAttendanceDate = lastAttendanceDate,
                    IsCarryOver = courseRef.CourseEnrollmentStatus == CourseEnrollmentStatus.Carryover
                });
            }
        }

        private int CalculateConsecutiveAbsences(List<AttendanceRecord> records)
        {
            if (!records.Any()) return 0;

            var consecutiveCount = 0;
            var maxConsecutive = 0;

            // Start from the most recent record
            foreach (var record in records.OrderByDescending(r => r.Date))
            {
                if (!record.IsPresent)
                {
                    consecutiveCount++;
                    maxConsecutive = Math.Max(maxConsecutive, consecutiveCount);
                }
                else
                {
                    break; // Stop at first present record when going backwards
                }
            }

            return consecutiveCount; // Return current consecutive absences from most recent
        }

        protected async Task ViewCourseDetails(string courseCode)
        {
            try
            {
                SelectedCourseDetails = await CourseService.GetCourseByIdAsync(courseCode);
                SelectedCourseStats = CourseAttendanceStats.FirstOrDefault(c => c.CourseCode == courseCode);
                SelectedCourseAttendanceRecords = AllAttendanceRecords
                    .Where(ar => ar.CourseCode == courseCode)
                    .OrderByDescending(ar => ar.Date)
                    .ToList();
                
                FilteredAttendanceRecords = SelectedCourseAttendanceRecords.ToList();
                
                ShowCourseModal = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading course details: {ex.Message}";
            }
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
                                 record.Date.ToString("yyyy-MM") == SelectedMonthFilter;

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
                .Select(r => r.Date)
                .GroupBy(d => d.ToString("yyyy-MM"))
                .OrderByDescending(g => g.Key)
                .ToDictionary(g => g.Key, g => DateTime.ParseExact(g.Key, "yyyy-MM", null).ToString("MMMM yyyy"));
        }

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
    }

    // Enhanced CourseAttendanceStats class
    public class CourseAttendanceStats
    {
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string DepartmentId { get; set; }
        public LevelType Level { get; set; }
        public SemesterType Semester { get; set; }
        public byte CreditUnits { get; set; }
        public double AttendancePercentage { get; set; }
        public int ClassesAttended { get; set; }
        public int TotalClasses { get; set; }
        public int TotalAbsences { get; set; }
        public int ConsecutiveAbsences { get; set; }
        public DateTime? LastAttendanceDate { get; set; }
        public bool IsCarryOver { get; set; }
    }

    // AttendanceRecord class
    public class AttendanceRecord
    {
        public string CourseCode { get; set; }
        public string StudentMatric { get; set; }
        public DateTime Date { get; set; }
        public bool IsPresent { get; set; }
        public DateTime RecordedAt { get; set; }
        public string RecordedBy { get; set; }
    }
}