namespace AirCode.Services.Department;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
    public interface IDepartmentService
    {
        Task<List<Department>> GetAllDepartmentsAsync();
        Task<Department> GetDepartmentByIdAsync(string departmentId);
        Task<bool> AddDepartmentAsync(Department department);
        Task<bool> DeleteDepartmentAsync(string departmentId);
        Task<bool> AddLevelToDepartmentAsync(string departmentId, Level level);
        Task<bool> RemoveLevelFromDepartmentAsync(string departmentId, LevelType levelType);
        Task<bool> AddCourseIdToLevelAsync(string departmentId, LevelType levelType, string courseId);
        Task<bool> RemoveCourseIdFromLevelAsync(string departmentId, LevelType levelType, string courseId);
    }
