using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Domain.ValueObjects;
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

    // Add reference to notification component
    private NotificationComponent _notificationComponent = default!;
    private LoadingSpinner _loadingSpinner = default!;

    private List<Course> _courses = new();
    private Course? _selectedCourse;
    private Course? _courseToDelete;
    private bool _isLoading = true;
    private bool _isProcessing = false; // For form operations
    private bool _isDeleting = false; // For delete operations
    private bool _showDeleteConfirmation = false;
    private bool _showEditForm = false;
    private bool _showAddForm = false;
    private string _searchTerm = string.Empty;
    private LevelType _filterLevel = LevelType.Level100;
    private SemesterType _filterSemester = SemesterType.FirstSemester;
    private string _filterDepartment = string.Empty;

    // Form fields for new/edit course
    private string _courseCode = string.Empty;
    private string _courseName = string.Empty;
    private string _departmentId = string.Empty;
    private LevelType _level = LevelType.Level100;
    private SemesterType _semester = SemesterType.FirstSemester;
    private byte _creditUnits = 1;
    private List<string> _lecturerIds = new();
    private string _newLecturerId = string.Empty;
    
    // Schedule fields
    private List<TimeSlot> _timeSlots = new();
    private DayOfWeek _newDay = DayOfWeek.Monday;
    private TimeSpan _newStartTime = new(9, 0, 0);
    private TimeSpan _newEndTime = new(10, 0, 0);
    private string _newLocation = string.Empty;

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

    private void ShowAddForm()
    {
        ResetForm();
        _showAddForm = true;
        _showEditForm = false;
        StateHasChanged();
    }

    private void ShowEditForm(Course course)
    {
        _selectedCourse = course;
        LoadCourseToForm(course);
        _showEditForm = true;
        _showAddForm = false;
        StateHasChanged();
    }

    private void LoadCourseToForm(Course course)
    {
        _courseName = course.Name;
        _courseCode = course.CourseCode;
        _departmentId = course.DepartmentId;
        _level = course.Level;
        _semester = course.Semester;
        _creditUnits = course.CreditUnits;
        _lecturerIds = course.LecturerIds?.ToList() ?? new List<string>();
        _timeSlots = course.Schedule.TimeSlots.ToList() ?? new List<TimeSlot>();
    }

    private void ResetForm()
    {
        _courseName = string.Empty;
        _courseCode = string.Empty;
        _departmentId = string.Empty;
        _level = LevelType.Level100;
        _semester = SemesterType.FirstSemester;
        _creditUnits = 1;
        _lecturerIds = new List<string>();
        _timeSlots = new List<TimeSlot>();
        _newLecturerId = string.Empty;
        _newLocation = string.Empty;
        _selectedCourse = null;
    }

    private void CancelForm()
    {
        _showAddForm = false;
        _showEditForm = false;
        ResetForm();
        StateHasChanged();
    }

    private async Task HandleValidSubmit()
    {
        if (_isProcessing) return; // Prevent double submission

        _isProcessing = true;
        StateHasChanged();

        try
        {
            if (_showAddForm)
            {
                await AddCourse();
            }
            else if (_showEditForm)
            {
                await UpdateCourse();
            }
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Operation failed: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            StateHasChanged();
        }
    }

    private async Task AddCourse()
    {
        try
        {
            // Validate form data
            if (string.IsNullOrWhiteSpace(_courseCode) || string.IsNullOrWhiteSpace(_courseName))
            {
                _notificationComponent?.ShowWarning("Course code and name are required");
                return;
            }

            // Check if course ID already exists
            if (_courses.Any(c => c.CourseCode == _courseCode))
            {
                _notificationComponent?.ShowWarning("Course code already exists!");
                return;
            }

            var schedule = new CourseSchedule(_timeSlots);
            var course = Course.Create(
                _courseCode,
                _courseName,
                _departmentId,
                _level,
                _semester,
                _creditUnits,
                schedule,
                _lecturerIds
            );

            var success = await CourseService.AddCourseAsync(course);
            if (success)
            {
                await LoadCourses();
                CancelForm();
                _notificationComponent?.ShowSuccess($"Course '{_courseName}' added successfully!");
            }
            else
            {
                _notificationComponent?.ShowError("Failed to add course. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Error adding course: {ex.Message}");
        }
    }

    private async Task UpdateCourse()
    {
        try
        {
            if (_selectedCourse == null)
            {
                _notificationComponent?.ShowError("No course selected for update");
                return;
            }

            // Validate form data
            if (string.IsNullOrWhiteSpace(_courseCode) || string.IsNullOrWhiteSpace(_courseName))
            {
                _notificationComponent?.ShowWarning("Course code and name are required");
                return;
            }

            var schedule = new CourseSchedule(_timeSlots);
            var updatedCourse = new Course(
                _courseCode,
                _courseName,
                _departmentId,
                _level,
                _semester,
                _creditUnits,
                schedule,
                _lecturerIds,
                DateTime.UtcNow,
                "Admin"
            );

            var success = await CourseService.UpdateCourseAsync(updatedCourse);
            if (success)
            {
                await LoadCourses();
                CancelForm();
                _notificationComponent?.ShowSuccess($"Course '{_courseName}' updated successfully!");
            }
            else
            {
                _notificationComponent?.ShowError("Failed to update course. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Error updating course: {ex.Message}");
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

    private void AddLecturer()
    {
        if (string.IsNullOrWhiteSpace(_newLecturerId))
        {
            _notificationComponent?.ShowWarning("Please enter a lecturer ID");
            return;
        }

        if (_lecturerIds.Contains(_newLecturerId))
        {
            _notificationComponent?.ShowWarning("Lecturer already added to this course");
            return;
        }

        _lecturerIds.Add(_newLecturerId);
        _newLecturerId = string.Empty;
        _notificationComponent?.ShowInfo("Lecturer added to course");
        StateHasChanged();
    }

    private void RemoveLecturer(string lecturerId)
    {
        _lecturerIds.Remove(lecturerId);
        _notificationComponent?.ShowInfo("Lecturer removed from course");
        StateHasChanged();
    }

    private void AddTimeSlot()
    {
        if (string.IsNullOrWhiteSpace(_newLocation))
        {
            _notificationComponent?.ShowWarning("Please enter a location");
            return;
        }

        if (_newStartTime >= _newEndTime)
        {
            _notificationComponent?.ShowWarning("Start time must be before end time");
            return;
        }

        // Check for time conflicts
        var conflictingSlot = _timeSlots.FirstOrDefault(ts => 
            ts.Day == _newDay && 
            ((ts.StartTime <= _newStartTime && ts.EndTime > _newStartTime) ||
             (ts.StartTime < _newEndTime && ts.EndTime >= _newEndTime) ||
             (ts.StartTime >= _newStartTime && ts.EndTime <= _newEndTime)));

        if (conflictingSlot != null)
        {
            _notificationComponent?.ShowWarning($"Time conflict with existing slot on {conflictingSlot.Day}");
            return;
        }

        var timeSlot = new TimeSlot
        {
            Day = _newDay,
            StartTime = _newStartTime,
            EndTime = _newEndTime,
            Location = _newLocation
        };

        _timeSlots.Add(timeSlot);
        _newLocation = string.Empty;
        _newStartTime = new TimeSpan(9, 0, 0);
        _newEndTime = new TimeSpan(10, 0, 0);
        _notificationComponent?.ShowInfo("Time slot added to schedule");
        StateHasChanged();
    }

    private void RemoveTimeSlot(TimeSlot timeSlot)
    {
        _timeSlots.Remove(timeSlot);
        _notificationComponent?.ShowInfo("Time slot removed from schedule");
        StateHasChanged();
    }

    private async Task AssignLecturer(string courseId)
    {
        if (string.IsNullOrWhiteSpace(_newLecturerId))
        {
            _notificationComponent?.ShowWarning("Please enter a lecturer ID");
            return;
        }

        try
        {
            var success = await CourseService.AssignLecturerToCourseAsync(courseId, _newLecturerId);
            if (success)
            {
                await LoadCourses();
                _newLecturerId = string.Empty;
                _notificationComponent?.ShowSuccess("Lecturer assigned successfully!");
            }
            else
            {
                _notificationComponent?.ShowError("Failed to assign lecturer. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Error assigning lecturer: {ex.Message}");
        }
    }

    private async Task RemoveLecturerFromCourse(string courseId, string lecturerId)
    {
        try
        {
            var success = await CourseService.RemoveLecturerFromCourseAsync(courseId, lecturerId);
            if (success)
            {
                await LoadCourses();
                _notificationComponent?.ShowSuccess("Lecturer removed successfully!");
            }
            else
            {
                _notificationComponent?.ShowError("Failed to remove lecturer. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _notificationComponent?.ShowError($"Error removing lecturer: {ex.Message}");
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

    private void UpdateStartTime(ChangeEventArgs e)
    {
        if (TimeSpan.TryParse(e.Value?.ToString(), out var time))
        {
            _newStartTime = time;
        }
    }

    private void UpdateEndTime(ChangeEventArgs e)
    {
        if (TimeSpan.TryParse(e.Value?.ToString(), out var time))
        {
            _newEndTime = time;
        }
    }
}
