using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Courses;
using AirCode.Components.SharedPrefabs.Cards;
using AirCode.Components.SharedPrefabs.Spinner;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageCourses : ComponentBase
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ICourseService CourseService { get; set; } = default!;

    private enum LoadingOperation
    {
        LoadingCourses,
        DeletingCourse
    }

    private LoadingOperation _loadingOperation = LoadingOperation.LoadingCourses;
    
    // Component references
    private NotificationComponent _notificationComponent = default!;

    // Data properties
    private List<Course> _courses = new();
    private Course? _selectedCourse;
    private Course? _courseToDelete;
    
    // State properties
    private bool _isLoading = true;
    private bool _isDeleting = false;
    private bool _showDeleteConfirmation = false;
    private bool _showCourseHandler = false;
    
    // Filter properties
    private string _searchTerm = string.Empty;
    private LevelType _filterLevel = LevelType.Level100;
    private SemesterType _filterSemester = SemesterType.FirstSemester;
    private string _filterDepartment = string.Empty;

    // Pagination properties
    private int _currentPage = 1;
    private int _pageSize = 10;

    protected override async Task OnInitializedAsync()
    {
        await LoadCourses();
    }

    private async Task LoadCourses()
    {
        _isLoading = true;
        _loadingOperation = LoadingOperation.LoadingCourses; 
        StateHasChanged();
        
        try
        {
            _courses = await CourseService.GetAllCoursesAsync();
            _notificationComponent?.ShowInfo("Courses loaded successfully");
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Failed to load courses: {ex.Message}");
            _courses = new List<Course>();
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private List<Course> FilteredCourses
    {
        get
        {
            var filtered = _courses.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchTerm))
            {
                filtered = filtered.Where(c => 
                    c.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    c.CourseCode.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_filterDepartment))
            {
                filtered = filtered.Where(c => c.DepartmentId == _filterDepartment);
            }

            return filtered.ToList();
        }
    }

    private void ShowAddForm()
    {
        _selectedCourse = null;
        _showCourseHandler = true;
        StateHasChanged();
    }

    private void ShowEditForm(Course course)
    {
        _selectedCourse = course;
        _showCourseHandler = true;
        StateHasChanged();
    }

    private async Task HandleCourseSaved(Course course)
    {
        await LoadCourses();
        _showCourseHandler = false;
        _selectedCourse = null;
        StateHasChanged();
    }

    private void HandleCourseHandlerClosed()
    {
        _showCourseHandler = false;
        _selectedCourse = null;
        StateHasChanged();
    }

    private void HandleNotification(string message)
    {
        if (message.Contains("successfully"))
        {
            _notificationComponent?.ShowSuccess(message);
        }
        else if (message.Contains("Error") || message.Contains("Failed"))
        {
            _notificationComponent?.ShowError(message);
        }
        else
        {
            _notificationComponent?.ShowInfo(message);
        }
    }

    private void ConfirmDelete(Course course)
    {
        _courseToDelete = course;
        _showDeleteConfirmation = true;
        StateHasChanged();
    }

    private void CancelDelete()
    {
        _courseToDelete = null;
        _showDeleteConfirmation = false;
        StateHasChanged();
    }

    private async Task DeleteCourse()
    {
        if (_courseToDelete == null || _isDeleting) return;

        _isDeleting = true;
        _loadingOperation = LoadingOperation.DeletingCourse; 
        StateHasChanged();

        try
        {
            var courseName = _courseToDelete.Name;
            var success = await CourseService.DeleteCourseAsync(_courseToDelete.CourseCode);
            
            if (success)
            {
                await LoadCourses();
                _notificationComponent?.ShowSuccess($"Course '{courseName}' deleted successfully!");
            }
            else
            {
                _notificationComponent?.ShowError("Failed to delete course. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Error deleting course: {ex.Message}");
        }
        finally
        {
            _isDeleting = false;
            CancelDelete();
        }
    }

    private async Task LoadCoursesByLevel()
    {
        _isLoading = true;
        StateHasChanged();
        
        try
        {
            _courses = await CourseService.GetCoursesByLevelAsync(_filterLevel);
            _notificationComponent?.ShowInfo($"Courses for {_filterLevel} loaded successfully");
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Failed to load courses by level: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadCoursesBySemester()
    {
        _isLoading = true;
        StateHasChanged();
        
        try
        {
            _courses = await CourseService.GetCoursesBySemesterAsync(_filterSemester);
            _notificationComponent?.ShowInfo($"Courses for {_filterSemester} loaded successfully");
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Failed to load courses by semester: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private List<Course> PaginatedCourses
    {
        get
        {
            var filtered = FilteredCourses;
            var skip = (_currentPage - 1) * _pageSize;
            return filtered.Skip(skip).Take(_pageSize).ToList();
        }
    }

    private int TotalPages => (int)Math.Ceiling((double)FilteredCourses.Count / _pageSize);

    private int StartPage
    {
        get
        {
            var start = Math.Max(1, _currentPage - 2);
            return Math.Min(start, Math.Max(1, TotalPages - 4));
        }
    }

    private int EndPage
    {
        get
        {
            var end = Math.Min(TotalPages, StartPage + 4);
            return end;
        }
    }

    private int CurrentPage => _currentPage;

    private void ChangePage(int page)
    {
        if (page >= 1 && page <= TotalPages)
        {
            _currentPage = page;
            StateHasChanged();
        }
    }

    private void ResetPagination()
    {
        _currentPage = 1;
    }

    private async Task OnLevelFilterChanged(ChangeEventArgs e)
    {
        if (Enum.TryParse<LevelType>(e.Value?.ToString(), out var level))
        {
            _filterLevel = level;
            await OnFilterChanged();
        }
    }

    private async Task ApplyFilters()
    {
        ResetPagination();
        _notificationComponent?.ShowInfo("Filters applied successfully");
        await InvokeAsync(StateHasChanged);
    }

    private async Task ResetFilters()
    {
        _searchTerm = string.Empty;
        _filterDepartment = string.Empty;
        _filterLevel = LevelType.Level100;
        _filterSemester = SemesterType.FirstSemester;
        ResetPagination();
        _notificationComponent?.ShowInfo("Filters reset successfully");
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnFilterChanged()
    {
        ResetPagination();
        await InvokeAsync(StateHasChanged);
    }
}