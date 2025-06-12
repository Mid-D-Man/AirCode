using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Services.Courses;
using Microsoft.AspNetCore.Components;
namespace AirCode.Pages.Shared;

public partial class PersonalCourseManagement : ComponentBase
{
    // Component State ... removed from layout since its general shared page for now though
    private bool IsLoading = true;
    private bool IsProcessing = false;
    private string ErrorMessage = string.Empty;
    private string SuccessMessage = string.Empty;
  
    //using matric num if user above level and course below enrol as carry over
    // Student Data
    [Parameter] public string CurrentMatricNumber { get; set; } = "U21CYS1083"; // This should come from auth context
    [Parameter] public LevelType CurrentStudentLevel { get; set; } = LevelType.Level100; // This should come from auth context
    
    private StudentCourse? CurrentStudentCourse;
    private List<Course> AvailableCourses = new();
    private List<Course> FilteredCourses = new();

    // Filtering
    private string SearchTerm = string.Empty;
    private SemesterType? SelectedSemester;
    private int DisplayedCoursesCount = 12;
   
    protected override async Task OnInitializedAsync()
    {
        await LoadStudentData();
    }

    private async Task LoadStudentData()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            // Load student's current course data
            CurrentStudentCourse = await CourseService.GetStudentCoursesByMatricAsync(CurrentMatricNumber);
            //creating if missing already handled by the get func
            /*
            // If student doesn't exist, create new student course record
            if (CurrentStudentCourse == null)
            {
                CurrentStudentCourse = new StudentCourse(
                    CurrentMatricNumber,
                    CurrentStudentLevel,
                    new List<CourseRefrence>(),
                    DateTime.UtcNow,
                    "Student"
                );
                await CourseService.AddStudentCourseAsync(CurrentStudentCourse);
            }
             */
            // Load available courses for the student's level
            //get all courses not by level cause we gas account for carryover courses and what not
            AvailableCourses = await CourseService.GetAllCoursesAsync();
            
            FilterCourses();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load course data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EnrollInCourse(string courseCode)
    {
        try
        {
            IsProcessing = true;
            
            var courseRef = new CourseRefrence(
                courseCode,
                CourseEnrollmentStatus.Enrolled,
                DateTime.UtcNow,
                DateTime.UtcNow
            );

            var success = await CourseService.AddCourseReferenceToStudentAsync(CurrentMatricNumber, courseRef);
            
            if (success)
            {
                SuccessMessage = $"Successfully enrolled in {courseCode}";
                await LoadStudentData(); // Refresh data
            }
            else
            {
                ErrorMessage = "Failed to enroll in course. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error enrolling in course: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task RemoveCourse(string courseCode)
    {
        try
        {
            IsProcessing = true;
            
            var success = await CourseService.RemoveCourseReferenceFromStudentAsync(CurrentMatricNumber, courseCode);
            
            if (success)
            {
                SuccessMessage = $"Successfully removed {courseCode}";
                await LoadStudentData(); // Refresh data
            }
            else
            {
                ErrorMessage = "Failed to remove course. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error removing course: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task UpdateCourseStatus(string courseCode, CourseEnrollmentStatus newStatus)
    {
        try
        {
            IsProcessing = true;
            
            var success = await CourseService.UpdateStudentCourseReferenceStatusAsync(CurrentMatricNumber, courseCode, newStatus);
            
            if (success)
            {
                SuccessMessage = $"Course {courseCode} status updated to {newStatus}";
                await LoadStudentData(); // Refresh data
            }
            else
            {
                ErrorMessage = "Failed to update course status. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error updating course status: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void FilterCourses()
    {
        FilteredCourses = AvailableCourses.Where(course => 
        {
            var matchesSearch = string.IsNullOrEmpty(SearchTerm) || 
                               course.CourseCode.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                               course.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase);
            
            var matchesSemester = !SelectedSemester.HasValue || course.Semester == SelectedSemester.Value;
            
            return matchesSearch && matchesSemester;
        }).ToList();
    }

    private void LoadMoreCourses()
    {
        DisplayedCoursesCount += 12;
    }

    // Helper Methods
    private bool IsStudentEnrolledInCourse(string courseCode)
    {
        return CurrentStudentCourse?.StudentCoursesRefs?.Any(cr => cr.CourseCode == courseCode) == true;
    }

    private int GetEnrolledCoursesCount()
    {
        return CurrentStudentCourse?.StudentCoursesRefs?.Count(cr => cr.CourseEnrollmentStatus == CourseEnrollmentStatus.Enrolled) ?? 0;
    }

    private int GetTotalCreditUnits()
    {
        if (CurrentStudentCourse?.StudentCoursesRefs == null) return 0;
        
        return CurrentStudentCourse.StudentCoursesRefs
            .Where(cr => cr.CourseEnrollmentStatus == CourseEnrollmentStatus.Enrolled)
            .Sum(cr => 
            {
                var course = AvailableCourses.FirstOrDefault(c => c.CourseCode == cr.CourseCode);
                return course?.CreditUnits ?? 0;
            });
    }

    private int GetCarryoverCount()
    {
        return CurrentStudentCourse?.StudentCoursesRefs?.Count(cr => cr.CourseEnrollmentStatus == CourseEnrollmentStatus.Carryover) ?? 0;
    }

    private int GetDroppedCount()
    {
        return CurrentStudentCourse?.StudentCoursesRefs?.Count(cr => cr.CourseEnrollmentStatus == CourseEnrollmentStatus.Dropped) ?? 0;
    }

    private string GetLevelDisplay(LevelType level)
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