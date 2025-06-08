using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Firebase;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;

namespace AirCode.Pages.Admin.Superior;

public partial class ManageDepartments : ComponentBase
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IFirestoreService FirestoreService { get; set; } = default!;

    private const string DEPARTMENTS_COLLECTION = "DEPARTMENTS";  
    private const string DEPARTMENTS_DOCUMENT = "Departments";

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
    private bool _isFirebaseConnected = false;

    protected override async Task OnInitializedAsync()
    {
        await CheckFirebaseConnection();
        await LoadDepartments();
    }

    private async Task CheckFirebaseConnection()
    {
        try
        {
            if (!FirestoreService.IsInitialized)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Firebase is not initialized!");
                return;
            }

            _isFirebaseConnected = await FirestoreService.IsConnectedAsync();
            if (!_isFirebaseConnected)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Firebase connection failed!");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Firebase connection error: {ex.Message}");
        }
    }

    private async Task LoadDepartments()
    {
        _isLoading = true;
        try
        {
            if (!_isFirebaseConnected)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Firebase is not connected!");
                return;
            }

            // Try to get the departments document from Firebase
            var departmentsData = await FirestoreService.GetDocumentAsync<DepartmentsContainer>(
                DEPARTMENTS_COLLECTION, DEPARTMENTS_DOCUMENT);

            if (departmentsData != null && departmentsData.Departments != null)
            {
                _departments = departmentsData.Departments;
            }
            else
            {
                // Document doesn't exist, initialize with empty list
                _departments = new List<Department>();
                
                // Create the document structure in Firebase
                var initialContainer = new DepartmentsContainer 
                { 
                    Departments = _departments,
                    LastModified = DateTime.UtcNow,
                    ModifiedBy = "System"
                };

                // Try to create the initial document
                var result = await FirestoreService.AddDocumentAsync(
                    DEPARTMENTS_COLLECTION, initialContainer, DEPARTMENTS_DOCUMENT);
                
                if (string.IsNullOrEmpty(result))
                {
                    await JSRuntime.InvokeVoidAsync("alert", 
                        "Warning: Could not initialize departments collection in Firebase");
                }
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error loading departments: {ex.Message}");
            // Fallback to empty list if Firebase fails
            _departments = new List<Department>();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveDepartmentsToFirebase()
    {
        try
        {
            var departmentsContainer = new DepartmentsContainer
            {
                Departments = _departments,
                LastModified = DateTime.UtcNow,
                ModifiedBy = "Admin" // You might want to get this from user context
            };

            var success = await FirestoreService.UpdateDocumentAsync(
                DEPARTMENTS_COLLECTION, DEPARTMENTS_DOCUMENT, departmentsContainer);

            if (!success)
            {
                throw new Exception("Failed to update Firebase document");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error saving to Firebase: {ex.Message}");
            throw; // Re-throw to handle in calling method
        }
    }

    private async Task HandleValidSubmit()
    {
        try
        {
            // Check if department ID already exists
            if (_departments.Any(d => d.DepartmentId == _newDepartment.DepartmentId))
            {
                await JSRuntime.InvokeVoidAsync("alert", "Department ID already exists!");
                return;
            }
            
            _newDepartment.SetModificationDetails(
                Guid.NewGuid().ToString(), 
                DateTime.UtcNow, 
                "Admin"); // You might want to get this from user context

            // Add to local list
            _departments.Add(_newDepartment);
            
            // Save to Firebase
            await SaveDepartmentsToFirebase();
            
            // Reset form
            _newDepartment = new Department();
            await JSRuntime.InvokeVoidAsync("alert", "Department added successfully!");
        }
        catch (Exception ex)
        {
            // Remove from local list if Firebase save failed
            if (_departments.Contains(_newDepartment))
            {
                _departments.Remove(_newDepartment);
            }
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
            
            // Remove from local list
            _departments.Remove(_departmentToDelete);
            
            // Save to Firebase
            await SaveDepartmentsToFirebase();
            
            // Clear selection if deleted department was selected
            if (_selectedDepartment?.DepartmentId == _departmentToDelete.DepartmentId)
            {
                _selectedDepartment = null;
            }
            
            await JSRuntime.InvokeVoidAsync("alert", "Department deleted successfully!");
        }
        catch (Exception ex)
        {
            // Add back to local list if Firebase save failed
            if (_departmentToDelete != null && !_departments.Contains(_departmentToDelete))
            {
                _departments.Add(_departmentToDelete);
            }
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
            
            // Check if level already exists
            if (_selectedDepartment.Levels.Any(l => l.LevelType == _newLevelType))
            {
                await JSRuntime.InvokeVoidAsync("alert", "This level already exists in the department!");
                return;
            }
            
            var newLevel = new Level
            {
                LevelType = _newLevelType,
                DepartmentId = _selectedDepartment.DepartmentId,
                CourseIds = new List<string>()
            };
            
            // Add to local object
            _selectedDepartment.AddLevel(newLevel);
            
            // Save to Firebase
            await SaveDepartmentsToFirebase();
            
            await JSRuntime.InvokeVoidAsync("alert", "Level added successfully!");
        }
        catch (Exception ex)
        {
            // Remove from local object if Firebase save failed
            if (_selectedDepartment != null)
            {
                _selectedDepartment.RemoveLevel(_newLevelType);
            }
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
        Level? removedLevel = null;
        try
        {
            if (_selectedDepartment == null) return;
            
            // Store reference to removed level for rollback
            removedLevel = _selectedDepartment.Levels.FirstOrDefault(l => l.LevelType == _levelTypeToRemove);
            
            // Remove from local object
            _selectedDepartment.RemoveLevel(_levelTypeToRemove);
            
            // Save to Firebase
            await SaveDepartmentsToFirebase();
            
            await JSRuntime.InvokeVoidAsync("alert", "Level removed successfully!");
        }
        catch (Exception ex)
        {
            // Add back to local object if Firebase save failed
            if (_selectedDepartment != null && removedLevel != null)
            {
                _selectedDepartment.AddLevel(removedLevel);
            }
            await JSRuntime.InvokeVoidAsync("alert", $"Error removing level: {ex.Message}");
        }
        finally
        {
            CancelRemoveLevel();
        }
    }

    private async Task AddCourseToLevel(LevelType levelType)
    {
        try
        {
            if (_selectedDepartment == null || string.IsNullOrWhiteSpace(_newCourseId)) return;
            
            var level = _selectedDepartment.Levels.FirstOrDefault(l => l.LevelType == levelType);
            if (level == null) return;
            
            // Check if course ID already exists in this level
            if (level.CourseIds.Contains(_newCourseId))
            {
                await JSRuntime.InvokeVoidAsync("alert", "This course ID already exists in this level!");
                return;
            }
            
            // Add to local object
            level.AddCourseId(_newCourseId);
            
            // Save to Firebase
            await SaveDepartmentsToFirebase();
            
            _newCourseId = string.Empty;
            await JSRuntime.InvokeVoidAsync("alert", "Course added successfully!");
        }
        catch (Exception ex)
        {
            // Remove from local object if Firebase save failed
            var level = _selectedDepartment?.Levels.FirstOrDefault(l => l.LevelType == levelType);
            if (level != null && level.CourseIds.Contains(_newCourseId))
            {
                level.RemoveCourseId(_newCourseId);
            }
            await JSRuntime.InvokeVoidAsync("alert", $"Error adding course: {ex.Message}");
        }
    }

    private async Task RemoveCourseFromLevel(LevelType levelType, string courseId)
    {
        try
        {
            if (_selectedDepartment == null) return;
            
            var level = _selectedDepartment.Levels.FirstOrDefault(l => l.LevelType == levelType);
            if (level == null) return;
            
            // Remove from local object
            level.RemoveCourseId(courseId);
            
            // Save to Firebase
            await SaveDepartmentsToFirebase();
            
            await JSRuntime.InvokeVoidAsync("alert", "Course removed successfully!");
        }
        catch (Exception ex)
        {
            // Add back to local object if Firebase save failed
            var level = _selectedDepartment?.Levels.FirstOrDefault(l => l.LevelType == levelType);
            if (level != null && !level.CourseIds.Contains(courseId))
            {
                level.AddCourseId(courseId);
            }
            await JSRuntime.InvokeVoidAsync("alert", $"Error removing course: {ex.Message}");
        }
    }
}

// Helper class to match Firebase document structure
public class DepartmentsContainer
{
    public List<Department> Departments { get; set; } = new();
    public DateTime LastModified { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}