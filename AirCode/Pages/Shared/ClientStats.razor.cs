using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
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

        // Student identification - placeholder for now
        protected string CurrentStudentMatric { get; set; } = "U21CYS1083";

        // Data properties
        protected List<CourseAttendanceStats> CourseAttendanceStats { get; set; } = new();
        protected StudentCourse StudentCourseData { get; set; }
        protected List<Course> EnrolledCourses { get; set; } = new();
        protected List<AttendanceRecord> AllAttendanceRecords { get; set; } = new();

        // Modal properties
        protected bool ShowCourseModal { get; set; } = false;
        protected Course SelectedCourseDetails { get; set; }
        protected List<AttendanceRecord> SelectedCourseAttendanceRecords { get; set; } = new();

        // Loading and error states
        protected bool IsLoading { get; set; } = true;
        protected string ErrorMessage { get; set; }

        // Computed properties
        protected int TotalEnrolledCourses => CourseAttendanceStats?.Where(c => !c.IsCarryOver).Count() ?? 0;
        protected int TotalCarryOverCourses => CourseAttendanceStats?.Where(c => c.IsCarryOver).Count() ?? 0;
        protected double OverallAttendanceRate => CourseAttendanceStats?.Any() == true 
            ? Math.Round(CourseAttendanceStats.Average(c => c.AttendancePercentage), 1) 
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
                    ErrorMessage = "Student course data not found. Please ensure you are properly enrolled.";
                    return;
                }

                // Load course details for enrolled courses
                await LoadEnrolledCoursesAsync();
                
                // Load attendance data (placeholder implementation)
                await LoadAttendanceDataAsync();
                
                // Calculate attendance statistics
                CalculateAttendanceStatistics();
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
                        // Log error but continue with other courses
                        Console.WriteLine($"Error loading course {courseRef.CourseCode}: {ex.Message}");
                    }
                }
            }
        }

        private async Task LoadAttendanceDataAsync()
        {
            // Placeholder implementation - in production, this would call an attendance service
            // For now, generate sample data
            AllAttendanceRecords = GenerateSampleAttendanceData();
            await Task.CompletedTask;
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
                    .ToList();

                var totalClasses = courseAttendanceRecords.Count;
                var attendedClasses = courseAttendanceRecords.Count(ar => ar.IsPresent);
                var attendancePercentage = totalClasses > 0 
                    ? Math.Round((double)attendedClasses / totalClasses * 100, 1) 
                    : 0;

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
                    IsCarryOver = courseRef.CourseEnrollmentStatus == CourseEnrollmentStatus.Carryover
                });
            }
        }

        protected async Task ViewCourseDetails(string courseCode)
        {
            try
            {
                SelectedCourseDetails = await CourseService.GetCourseByIdAsync(courseCode);
                SelectedCourseAttendanceRecords = AllAttendanceRecords
                    .Where(ar => ar.CourseCode == courseCode)
                    .OrderByDescending(ar => ar.Date)
                    .ToList();
                
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
            SelectedCourseAttendanceRecords.Clear();
            StateHasChanged();
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

        private List<AttendanceRecord> GenerateSampleAttendanceData()
        {
            var random = new Random();
            var records = new List<AttendanceRecord>();
            var startDate = DateTime.Now.AddMonths(-3);

            foreach (var course in EnrolledCourses)
            {
                // Generate 20-30 attendance records per course
                var recordCount = random.Next(20, 31);
                
                for (int i = 0; i < recordCount; i++)
                {
                    var date = startDate.AddDays(i * 3 + random.Next(0, 3));
                    var isPresent = random.NextDouble() > 0.25; // 75% attendance probability
                    
                    records.Add(new AttendanceRecord
                    {
                        CourseCode = course.CourseCode,
                        StudentMatric = CurrentStudentMatric,
                        Date = date,
                        IsPresent = isPresent,
                        RecordedAt = date.AddHours(random.Next(8, 18))
                    });
                }
            }

            return records.OrderByDescending(r => r.Date).ToList();
        }
    }

    // Supporting classes
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
        public bool IsCarryOver { get; set; }
    }

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