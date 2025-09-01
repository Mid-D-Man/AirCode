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
            notificationComponent?.ShowError($"Error loading departments: {ex.Message}");
            _departments = new List<Department>();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleValidSubmit()
    {
        _isAddingDepartment = true;
        try
        {
            var success = await DepartmentService.AddDepartmentAsync(_newDepartment);
            
            if (success)
            {
                // Refresh local data
                await LoadDepartments();
                
                // Reset form
                _newDepartment = new Department();
                notificationComponent?.ShowSuccess("Department added successfully!");
            }
            else
            {
                notificationComponent?.ShowError("Failed to add department. Department ID may already exist.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error adding department: {ex.Message}");
        }
        finally
        {
            _isAddingDepartment = false;
        }
    }

    private void SelectDepartment(Department department)
    {
        _selectedDepartment = department;
        _expandedLevels.Clear(); // Reset expansion state
    }

    private async Task DeleteDepartment()
    {
        _isDeletingDepartment = true;
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
                    _expandedLevels.Clear();
                }
                
                notificationComponent?.ShowSuccess("Department deleted successfully!");
            }
            else
            {
                notificationComponent?.ShowError("Failed to delete department.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error deleting department: {ex.Message}");
        }
        finally
        {
            _isDeletingDepartment = false;
            CancelDeleteDepartment();
        }
    }

    private async Task AddLevelToDepartment()
    {
        _isAddingLevel = true;
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
                
                notificationComponent?.ShowSuccess("Level added successfully!");
            }
            else
            {
                notificationComponent?.ShowError("Failed to add level. Level may already exist.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error adding level: {ex.Message}");
        }
        finally
        {
            _isAddingLevel = false;
        }
    }

    private async Task RemoveLevel()
    {
        _isRemovingLevel = true;
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
                
                // Remove from expanded levels if it was expanded
                _expandedLevels.Remove(_levelTypeToRemove);
                
                notificationComponent?.ShowSuccess("Level removed successfully!");
            }
            else
            {
                notificationComponent?.ShowError("Failed to remove level.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error removing level: {ex.Message}");
        }
        finally
        {
            _isRemovingLevel = false;
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
                
                notificationComponent?.ShowSuccess($"Course '{selectedCourse.CourseCode} - {selectedCourse.Name}' added successfully to {_targetLevelType}!");
            }
            else
            {
                notificationComponent?.ShowError("Failed to add course. Course may already exist in this level.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error adding course: {ex.Message}");
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

    private async Task RemoveCourseFromLevel()
    {
        _isRemovingCourse = true;
        try
        {
            if (_selectedDepartment == null) return;
            
            var success = await DepartmentService.RemoveCourseIdFromLevelAsync(
                _selectedDepartment.DepartmentId, _levelTypeToRemoveCourse, _courseToRemove);
            
            if (success)
            {
                // Refresh local data to maintain consistency
                await LoadDepartments();
                
                // Re-select the department to maintain UI state
                _selectedDepartment = _departments.FirstOrDefault(d => d.DepartmentId == _selectedDepartment.DepartmentId);
                
                notificationComponent?.ShowSuccess("Course removed successfully!");
            }
            else
            {
                notificationComponent?.ShowError("Failed to remove course.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error removing course: {ex.Message}");
        }
        finally
        {
            _isRemovingCourse = false;
            CancelRemoveCourse();
        }
    }
}