using AirCode.Utilities.HelperScripts;
using AirCode.Services.Firebase;

namespace AirCode.Services.Department;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;

public class DepartmentService : IDepartmentService
{
    private readonly IFirestoreService _firestoreService;
    private const string DEPARTMENTS_COLLECTION = "DEPARTMENTS";  
    private const string DEPARTMENTS_DOCUMENT = "Departments";

    public DepartmentService(IFirestoreService firestoreService)
    {
        _firestoreService = firestoreService;
        // Initialize Firebase connection
        _ = InitializeFirebaseAsync();
    }

    private async Task InitializeFirebaseAsync()
    {
        if (!_firestoreService.IsInitialized)
        {
            await _firestoreService.SetConnectionStateAsync(true);
            await _firestoreService.ProcessPendingOperationsAsync();
        }
    }
    public async Task<List<Department>> GetAllDepartmentsAsync()
    {
        try
        {
            if (!_firestoreService.IsInitialized)
            {
                MID_HelperFunctions.DebugMessage("Firebase is not initialized", DebugClass.Warning);
                return new List<Department>();
            }

            var isConnected = await _firestoreService.IsConnectedAsync();
            if (!isConnected)
            {
                MID_HelperFunctions.DebugMessage("Firebase is not connected", DebugClass.Warning);
                return new List<Department>();
            }

            var departmentsData = await _firestoreService.GetDocumentAsync<DepartmentsContainer>(
                DEPARTMENTS_COLLECTION, DEPARTMENTS_DOCUMENT);

            if (departmentsData?.Departments != null)
            {
                return departmentsData.Departments;
            }

            // Initialize empty collection if document doesn't exist
            await InitializeDepartmentsCollection();
            return new List<Department>();
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error retrieving departments: {ex.Message}", DebugClass.Exception);
            return new List<Department>();
        }
    }

    public async Task<Department> GetDepartmentByIdAsync(string departmentId)
    {
        try
        {
            var departments = await GetAllDepartmentsAsync();
            return departments.FirstOrDefault(d => d.DepartmentId == departmentId);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error retrieving department {departmentId}: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }

    public async Task<bool> AddDepartmentAsync(Department department)
    {
        try
        {
            var departments = await GetAllDepartmentsAsync();
            
            // Validate department doesn't already exist
            if (departments.Any(d => d.DepartmentId == department.DepartmentId))
            {
                MID_HelperFunctions.DebugMessage($"Department {department.DepartmentId} already exists", DebugClass.Warning);
                return false;
            }

            // Set security attributes
            department.SetModificationDetails(
                Guid.NewGuid().ToString(), 
                DateTime.UtcNow, 
                "Admin"); // TODO: Get from user context

            departments.Add(department);
            return await SaveDepartmentsAsync(departments);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error adding department: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    public async Task<bool> DeleteDepartmentAsync(string departmentId)
    {
        try
        {
            var departments = await GetAllDepartmentsAsync();
            var departmentToRemove = departments.FirstOrDefault(d => d.DepartmentId == departmentId);
            
            if (departmentToRemove == null)
            {
                MID_HelperFunctions.DebugMessage($"Department {departmentId} not found", DebugClass.Warning);
                return false;
            }

            departments.Remove(departmentToRemove);
            return await SaveDepartmentsAsync(departments);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error deleting department {departmentId}: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    public async Task<bool> AddLevelToDepartmentAsync(string departmentId, Level level)
    {
        try
        {
            var departments = await GetAllDepartmentsAsync();
            var department = departments.FirstOrDefault(d => d.DepartmentId == departmentId);
            
            if (department == null)
            {
                MID_HelperFunctions.DebugMessage($"Department {departmentId} not found", DebugClass.Warning);
                return false;
            }

            // Validate level doesn't already exist
            if (department.Levels.Any(l => l.LevelType == level.LevelType))
            {
                MID_HelperFunctions.DebugMessage($"Level {level.LevelType} already exists in department {departmentId}", DebugClass.Warning);
                return false;
            }
            
            level.DepartmentId = departmentId;
            department.AddLevel(level);
            
            return await SaveDepartmentsAsync(departments);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error adding level to department {departmentId}: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    public async Task<bool> RemoveLevelFromDepartmentAsync(string departmentId, LevelType levelType)
    {
        try
        {
            var departments = await GetAllDepartmentsAsync();
            var department = departments.FirstOrDefault(d => d.DepartmentId == departmentId);
            
            if (department == null)
            {
                MID_HelperFunctions.DebugMessage($"Department {departmentId} not found", DebugClass.Warning);
                return false;
            }
            
            bool removed = department.RemoveLevel(levelType);
            if (!removed)
            {
                MID_HelperFunctions.DebugMessage($"Level {levelType} not found in department {departmentId}", DebugClass.Warning);
                return false;
            }
            
            return await SaveDepartmentsAsync(departments);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error removing level from department {departmentId}: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    public async Task<bool> AddCourseIdToLevelAsync(string departmentId, LevelType levelType, string courseId)
    {
        try
        {
            var departments = await GetAllDepartmentsAsync();
            var department = departments.FirstOrDefault(d => d.DepartmentId == departmentId);
            
            if (department == null)
            {
                MID_HelperFunctions.DebugMessage($"Department {departmentId} not found", DebugClass.Warning);
                return false;
            }
            
            var level = department.Levels.FirstOrDefault(l => l.LevelType == levelType);
            if (level == null)
            {
                MID_HelperFunctions.DebugMessage($"Level {levelType} not found in department {departmentId}", DebugClass.Warning);
                return false;
            }

            // Validate course doesn't already exist
            if (level.CourseIds.Contains(courseId))
            {
                MID_HelperFunctions.DebugMessage($"Course {courseId} already exists in level {levelType}", DebugClass.Warning);
                return false;
            }
            
            level.AddCourseId(courseId);
            return await SaveDepartmentsAsync(departments);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error adding course to level: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    public async Task<bool> RemoveCourseIdFromLevelAsync(string departmentId, LevelType levelType, string courseId)
    {
        try
        {
            var departments = await GetAllDepartmentsAsync();
            var department = departments.FirstOrDefault(d => d.DepartmentId == departmentId);
            
            if (department == null)
            {
                MID_HelperFunctions.DebugMessage($"Department {departmentId} not found", DebugClass.Warning);
                return false;
            }
            
            var level = department.Levels.FirstOrDefault(l => l.LevelType == levelType);
            if (level == null)
            {
                MID_HelperFunctions.DebugMessage($"Level {levelType} not found in department {departmentId}", DebugClass.Warning);
                return false;
            }
            
            bool removed = level.RemoveCourseId(courseId);
            if (!removed)
            {
                MID_HelperFunctions.DebugMessage($"Course {courseId} not found in level {levelType}", DebugClass.Warning);
                return false;
            }
            
            return await SaveDepartmentsAsync(departments);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error removing course from level: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    private async Task<bool> SaveDepartmentsAsync(List<Department> departments)
    {
        try
        {
            var departmentsContainer = new DepartmentsContainer
            {
                Departments = departments,
                LastModified = DateTime.UtcNow,
                ModifiedBy = "Admin" // TODO: Get from user context
            };

            return await _firestoreService.UpdateDocumentAsync(
                DEPARTMENTS_COLLECTION, DEPARTMENTS_DOCUMENT, departmentsContainer);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error saving departments to Firebase: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    private async Task InitializeDepartmentsCollection()
    {
        try
        {
            var initialContainer = new DepartmentsContainer 
            { 
                Departments = new List<Department>(),
                LastModified = DateTime.UtcNow,
                ModifiedBy = "System"
            };

            var result = await _firestoreService.AddDocumentAsync(
                DEPARTMENTS_COLLECTION, initialContainer, DEPARTMENTS_DOCUMENT);
                
            if (string.IsNullOrEmpty(result))
            {
                MID_HelperFunctions.DebugMessage("Failed to initialize departments collection", DebugClass.Warning);
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error initializing departments collection: {ex.Message}", DebugClass.Exception);
        }
    }
}

// Helper class to match Firebase document structure
public class DepartmentsContainer
{
    public List<Domain.Entities.Department> Departments { get; set; } = new();
    public DateTime LastModified { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}











