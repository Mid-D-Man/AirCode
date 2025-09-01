using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
using AirCode.Services.Auth;
using AirCode.Services.Firebase;
using AirCode.Utilities.ObjectPooling;
using Microsoft.AspNetCore.Components;

namespace AirCode.Pages.Shared;

public partial class PersonalCourseManagement : ComponentBase, IDisposable
{
    // Object Pools
    private static readonly MID_ComponentObjectPool<List<Course>> _courseListPool = new(
        () => new List<Course>(),
        list => list.Clear(),
        maxPoolSize: 50,
        cleanupInterval: TimeSpan.FromMinutes(10)
    );

    private static readonly MID_ComponentObjectPool<Collections> _collectionsPool = new(
        () => new Collections(),
        collections => 
        {
            collections.Students.Clear();
            collections.Lecturers.Clear();
            collections.CourseReps.Clear();
        },
        maxPoolSize: 20,
        cleanupInterval: TimeSpan.FromMinutes(5)
    );

    [Inject]  private IAuthService _authService {get;set;}
    [Inject]  private IFirestoreService _fireStoreService {get;set;}
    // Component State
    private bool IsLoading = true;
    private bool IsProcessing = false;
    private string ErrorMessage = string.Empty;

    // Student Data
    [Parameter] public string CurrentMatricNumber { get; set; } = "U21CYS1083";
    [Parameter] public LevelType CurrentStudentLevel { get; set; } = LevelType.Level100;
    
    private StudentCourse? CurrentStudentCourse;
    private List<Course> AvailableCourses = new();
    private List<Course> FilteredCourses = new();

    // Pagination & Filtering
    private const int CoursesPerPage = 10;
    private readonly PaginationState _paginationState = new();
    private string SearchTerm = string.Empty;
    private SemesterType? SelectedSemester;

    // Pooled objects
    private PooledObjectWrapper<List<Course>>? _pooledEnrolledCourses;
    private PooledObjectWrapper<List<Course>>? _pooledAvailableCourses;
    private PooledObjectWrapper<Collections>? _pooledCollections;

    protected override async Task OnInitializedAsync()
    {
        await LoadStudentData();
    }

    private async Task LoadStudentData()
    {
        using var loadOperation = _courseListPool.GetPooled();
        
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            // Get pooled objects
            _pooledCollections = _collectionsPool.GetPooled();
            _pooledEnrolledCourses = _courseListPool.GetPooled();
            _pooledAvailableCourses = _courseListPool.GetPooled();

            CurrentMatricNumber = await  _authService.GetMatricNumberAsync();
            CurrentStudentLevel =  await _fireStoreService.GetStudentLevelType(CurrentMatricNumber);
            // Load student's current course data
            CurrentStudentCourse = await CourseService.GetStudentCoursesByMatricAsync(CurrentMatricNumber);
            
            // Load available courses
            var allCourses = await CourseService.GetAllCoursesAsync();
            
            // Use pooled list to avoid allocations
            var tempFilteredList = loadOperation.Object;
            tempFilteredList.AddRange(allCourses);
            
            // Copy to our main collections using pooled objects
            AvailableCourses.Clear();
            AvailableCourses.AddRange(tempFilteredList);
            
            FilterCoursesWithPagination();
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

    private void FilterCoursesWithPagination()
    {
        using var filterOperation = _courseListPool.GetPooled();
        var tempList = filterOperation.Object;

        // Apply filters
        foreach (var course in AvailableCourses)
        {
            var matchesSearch = string.IsNullOrEmpty(SearchTerm) || 
                               course.CourseCode.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                               course.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase);
            
            var matchesSemester = !SelectedSemester.HasValue || course.Semester == SelectedSemester.Value;
            
            if (matchesSearch && matchesSemester)
            {
                tempList.Add(course);
            }
        }

        // Apply pagination
        var skip = (_paginationState.CourseRepsCurrentPage - 1) * CoursesPerPage;
        FilteredCourses.Clear();
        FilteredCourses.AddRange(tempList.Skip(skip).Take(CoursesPerPage));
    }

    private async Task<bool> EnrollInCourse(string courseCode)
    {
        using var enrollOperation = _courseListPool.GetPooled();
        
        try
        {
            var courseRef = new CourseRefrence(
                courseCode,
                CourseEnrollmentStatus.Enrolled,
                DateTime.UtcNow,
                DateTime.UtcNow
            );

            var success = await CourseService.AddCourseReferenceToStudentAsync(CurrentMatricNumber, courseRef);
            
            if (success)
            {
                await LoadStudentData();
                return true;
            }
            else
            {
                ErrorMessage = "Failed to enroll in course. Please try again.";
                return false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error enrolling in course: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> RemoveCourse(string courseCode)
    {
        using var removeOperation = _courseListPool.GetPooled();
        
        try
        {
            var success = await CourseService.RemoveCourseReferenceFromStudentAsync(CurrentMatricNumber, courseCode);
            
            if (success)
            {
                await LoadStudentData();
                return true;
            }
            else
            {
                ErrorMessage = "Failed to remove course. Please try again.";
                return false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error removing course: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> UpdateCourseStatus(string courseCode, CourseEnrollmentStatus newStatus)
    {
        using var updateOperation = _courseListPool.GetPooled();
        
        try
        {
            var success = await CourseService.UpdateStudentCourseReferenceStatusAsync(CurrentMatricNumber, courseCode, newStatus);
            
            if (success)
            {
                await LoadStudentData();
                return true;
            }
            else
            {
                ErrorMessage = "Failed to update course status. Please try again.";
                return false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error updating course status: {ex.Message}";
            return false;
        }
    }

    // Pagination Methods
    private async Task NextPage()
    {
        var totalPages = GetTotalAvailablePages();
        if (_paginationState.CourseRepsCurrentPage < totalPages)
        {
            _paginationState.CourseRepsCurrentPage++;
            FilterCoursesWithPagination();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task PreviousPage()
    {
        if (_paginationState.CourseRepsCurrentPage > 1)
        {
            _paginationState.CourseRepsCurrentPage--;
            FilterCoursesWithPagination();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task GoToPage(int pageNumber)
    {
        var totalPages = GetTotalAvailablePages();
        if (pageNumber >= 1 && pageNumber <= totalPages)
        {
            _paginationState.CourseRepsCurrentPage = pageNumber;
            FilterCoursesWithPagination();
            await InvokeAsync(StateHasChanged);
        }
    }

    private int GetTotalAvailablePages()
    {
        using var countOperation = _courseListPool.GetPooled();
        var tempList = countOperation.Object;

        // Count filtered courses without creating new list
        var count = 0;
        foreach (var course in AvailableCourses)
        {
            var matchesSearch = string.IsNullOrEmpty(SearchTerm) || 
                               course.CourseCode.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                               course.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase);
            
            var matchesSemester = !SelectedSemester.HasValue || course.Semester == SelectedSemester.Value;
            
            if (matchesSearch && matchesSemester)
            {
                count++;
            }
        }

        return (int)Math.Ceiling(count / (double)CoursesPerPage);
    }

    private async Task OnSearchChanged()
    {
        _paginationState.CourseRepsCurrentPage = 1; // Reset to first page
        FilterCoursesWithPagination();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSemesterFilterChanged()
    {
        _paginationState.CourseRepsCurrentPage = 1; // Reset to first page
        FilterCoursesWithPagination();
        await InvokeAsync(StateHasChanged);
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
        
        var totalUnits = 0;
        foreach (var courseRef in CurrentStudentCourse.StudentCoursesRefs)
        {
            if (courseRef.CourseEnrollmentStatus == CourseEnrollmentStatus.Enrolled)
            {
                var course = AvailableCourses.FirstOrDefault(c => c.CourseCode == courseRef.CourseCode);
                if (course != null)
                {
                    totalUnits += course.CreditUnits;
                }
            }
        }
        
        return totalUnits;
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

    private IEnumerable<CourseRefrence> GetEnrolledCoursesForCurrentPage()
    {
        if (CurrentStudentCourse?.StudentCoursesRefs == null)
            return Enumerable.Empty<CourseRefrence>();

        var skip = (_paginationState.StudentsCurrentPage - 1) * CoursesPerPage;
        return CurrentStudentCourse.StudentCoursesRefs
            .OrderBy(c => c.CourseCode)
            .Skip(skip)
            .Take(CoursesPerPage);
    }

    private int GetTotalEnrolledPages()
    {
        var count = CurrentStudentCourse?.StudentCoursesRefs?.Count ?? 0;
        return (int)Math.Ceiling(count / (double)CoursesPerPage);
    }

    private async Task NextEnrolledPage()
    {
        var totalPages = GetTotalEnrolledPages();
        if (_paginationState.StudentsCurrentPage < totalPages)
        {
            _paginationState.StudentsCurrentPage++;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task PreviousEnrolledPage()
    {
        if (_paginationState.StudentsCurrentPage > 1)
        {
            _paginationState.StudentsCurrentPage--;
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        // Dispose pooled objects
        _pooledEnrolledCourses?.Dispose();
        _pooledAvailableCourses?.Dispose();
        _pooledCollections?.Dispose();
    }
}