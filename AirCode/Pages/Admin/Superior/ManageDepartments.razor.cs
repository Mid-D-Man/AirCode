using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Department;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageDepartments : ComponentBase
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDepartmentService DepartmentService { get; set; } = default!;

    private List<Department> _departments = new();
    private Department _newDepartment = new();
    private Department? _selectedDepartment;
    private Department? _departmentToDelete;
    private bool _isLoading = true;
    private bool _showDeleteConfirmation = false;
    private bool _showRemoveLevelConfirmation = false;
    private LevelType _newLevelType = LevelType.Level100;
    private LevelType _levelTypeToRemove;
    private string _newCourseId = string.Empty;

    // Course selection related fields
    private bool _showCourseSelection = false;
    private LevelType _targetLevelType;
// State Management Implementation
    private HashSet<LevelType> _expandedLevels = new();

    private void ToggleLevel(LevelType levelType)
    {
        if (_expandedLevels.Contains(levelType))
            _expandedLevels.Remove(levelType);
        else
            _expandedLevels.Add(levelType);
    }
    protected override async Task OnInitializedAsync()
    {
        await LoadDepartments();
    }

    private async Task LoadDepartments()
    {
        _isLoading = true;
        try
        {
            _departments = await DepartmentService.GetAllDepartmentsAsync();
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error loading departments: {ex.Message}");
            _departments = new List<Department>();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleValidSubmit()
    {
        try
        {
            var success = await DepartmentService.AddDepartmentAsync(_newDepartment);
            
            if (success)
            {
                // Refresh local data
                await LoadDepartments();
                
                // Reset form
                _newDepartment = new Department();
                await JSRuntime.InvokeVoidAsync("alert", "Department added successfully!");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to add department. Department ID may already exist.");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error adding department: {ex.Message}");
        }
    }

    private void SelectDepartment(Department department)
    {
        _selectedDepartment = department;
    }

    private void ConfirmDelete(Department department)
    {
        _departmentToDelete = department;
        _showDeleteConfirmation = true;
    }

    private void CancelDelete()
    {
        _departmentToDelete = null;
        _showDeleteConfirmation = false;
    }

    private async Task DeleteDepartment()
    {
        try
        {
            if (_departmentToDelete == null) return;
            
            var success = await DepartmentService.DeleteDepartmentAsync(_departmentToDelete.DepartmentId);
            
            if (success)
            {
                // Refresh local data
                await LoadDepartments();
                
                // Clear selection if deleted department was selected
                if (_selectedDepartment?.DepartmentId == _departmentToDelete.DepartmentId)
                {
                    _selectedDepartment = null;
                }
                
                await JSRuntime.InvokeVoidAsync("alert", "Department deleted successfully!");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to delete department.");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error deleting department: {ex.Message}");
        }
        finally
        {
            CancelDelete();
        }
    }

    private async Task AddLevelToDepartment()
    {
        try
        {
            if (_selectedDepartment == null) return;
            
            var newLevel = new Level
            {
                LevelType = _newLevelType,
                DepartmentId = _selectedDepartment.DepartmentId,
                CourseIds = new List<string>()
            };
            
            var success = await DepartmentService.AddLevelToDepartmentAsync(
                _selectedDepartment.DepartmentId, newLevel);
            
            if (success)
            {
                // Refresh local data to maintain consistency
                await LoadDepartments();
                
                // Re-select the department to maintain UI state
                _selectedDepartment = _departments.FirstOrDefault(d => d.DepartmentId == _selectedDepartment.DepartmentId);
                
                await JSRuntime.InvokeVoidAsync("alert", "Level added successfully!");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to add level. Level may already exist.");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error adding level: {ex.Message}");
        }
    }

    private void ConfirmRemoveLevel(LevelType levelType)
    {
        _levelTypeToRemove = levelType;
        _showRemoveLevelConfirmation = true;
    }

    private void CancelRemoveLevel()
    {
        _showRemoveLevelConfirmation = false;
    }

    private async Task RemoveLevel()
    {
        try
        {
            if (_selectedDepartment == null) return;
            
            var success = await DepartmentService.RemoveLevelFromDepartmentAsync(
                _selectedDepartment.DepartmentId, _levelTypeToRemove);
            
            if (success)
            {
                // Refresh local data to maintain consistency
                await LoadDepartments();
                
                // Re-select the department to maintain UI state
                _selectedDepartment = _departments.FirstOrDefault(d => d.DepartmentId == _selectedDepartment.DepartmentId);
                
                await JSRuntime.InvokeVoidAsync("alert", "Level removed successfully!");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to remove level.");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error removing level: {ex.Message}");
        }
        finally
        {
            CancelRemoveLevel();
        }
    }

    #region Course Selection Integration

    private void OpenCourseSelection(LevelType levelType)
    {
        _targetLevelType = levelType;
        _showCourseSelection = true;
    }

    private async Task OnCourseSelected(Course selectedCourse)
    {
        try
        {
            if (_selectedDepartment == null || selectedCourse == null) return;
            
            var success = await DepartmentService.AddCourseIdToLevelAsync(
                _selectedDepartment.DepartmentId, _targetLevelType, selectedCourse.CourseCode);
            
            if (success)
            {
                // Refresh local data to maintain consistency
                await LoadDepartments();
                
                // Re-select the department to maintain UI state
                _selectedDepartment = _departments.FirstOrDefault(d => d.DepartmentId == _selectedDepartment.DepartmentId);
                
                await JSRuntime.InvokeVoidAsync("alert", $"Course '{selectedCourse.CourseCode} - {selectedCourse.Name}' added successfully to {_targetLevelType}!");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to add course. Course may already exist in this level.");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error adding course: {ex.Message}");
        }
        finally
        {
            _showCourseSelection = false;
        }
    }

    private void OnCourseSelectionClosed()
    {
        _showCourseSelection = false;
    }

    #endregion

    // Keep the old method for backward compatibility, but it's no longer used in the UI
    private async Task AddCourseToLevel(LevelType levelType)
    {
        try
        {
            if (_selectedDepartment == null || string.IsNullOrWhiteSpace(_newCourseId)) return;
            
            var success = await DepartmentService.AddCourseIdToLevelAsync(
                _selectedDepartment.DepartmentId, levelType, _newCourseId);
            
            if (success)
            {
                // Refresh local data to maintain consistency
                await LoadDepartments();
                
                // Re-select the department to maintain UI state
                _selectedDepartment = _departments.FirstOrDefault(d => d.DepartmentId == _selectedDepartment.DepartmentId);
                
                _newCourseId = string.Empty;
                await JSRuntime.InvokeVoidAsync("alert", "Course added successfully!");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to add course. Course may already exist in this level.");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error adding course: {ex.Message}");
        }
    }

    private async Task RemoveCourseFromLevel(LevelType levelType, string courseId)
    {
        try
        {
            if (_selectedDepartment == null) return;
            
            var success = await DepartmentService.RemoveCourseIdFromLevelAsync(
                _selectedDepartment.DepartmentId, levelType, courseId);
            
            if (success)
            {
                // Refresh local data to maintain consistency
                await LoadDepartments();
                
                // Re-select the department to maintain UI state
                _selectedDepartment = _departments.FirstOrDefault(d => d.DepartmentId == _selectedDepartment.DepartmentId);
                
                await JSRuntime.InvokeVoidAsync("alert", "Course removed successfully!");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to remove course.");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error removing course: {ex.Message}");
        }
    }
}