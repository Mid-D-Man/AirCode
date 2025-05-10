using AirCode.Utilities.HelperScripts;

namespace AirCode.Services.Department;



using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;

    public class DepartmentService : IDepartmentService
    {
        private const string COLLECTION_KEY = "DEPARTMENTS";
        
        public async Task<List<Department>> GetAllDepartmentsAsync()
        {
            // TODO: Implement Firebase retrieval
            // This would fetch all departments from Firebase
            return new List<Department>();
        }

        //ok this why we cant add level need to actually implemt this
        public async Task<Department> GetDepartmentByIdAsync(string departmentId)
        {
            // TODO: Implement Firebase retrieval for specific department
            return null;
        }

        public async Task<bool> AddDepartmentAsync(Department department)
        {
            try
            {
                // TODO: Implement Firebase add operation
                // This would save the department to Firebase under DEPARTMENTS collection

                // Set security attributes
                department.SetModificationDetails(Guid.NewGuid().ToString(),DateTime.UtcNow,"");
                
              
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeleteDepartmentAsync(string departmentId)
        {
            try
            {
                // TODO: Implement Firebase delete operation
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> AddLevelToDepartmentAsync(string departmentId, Level level)
        {
            try
            {
                var department = await GetDepartmentByIdAsync(departmentId);
                if (department == null) return false;
                
                level.DepartmentId = departmentId;
                department.AddLevel(level);
                
                // TODO: Update department in Firebase
                return true;
            }
            catch (Exception e)
            {
                MID_HelperFunctions.DebugMessage("Error trying to add level to department : " + e,DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> RemoveLevelFromDepartmentAsync(string departmentId, LevelType levelType)
        {
            try
            {
                var department = await GetDepartmentByIdAsync(departmentId);
                if (department == null) return false;
                
                bool removed = department.RemoveLevel(levelType);
                if (!removed) return false;
                
                // TODO: Update department in Firebase
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> AddCourseIdToLevelAsync(string departmentId, LevelType levelType, string courseId)
        {
            try
            {
                var department = await GetDepartmentByIdAsync(departmentId);
                if (department == null) return false;
                
                var level = department.Levels.Find(l => l.LevelType == levelType);
                if (level == null) return false;
                
                level.AddCourseId(courseId);
                
                // TODO: Update department in Firebase
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> RemoveCourseIdFromLevelAsync(string departmentId, LevelType levelType, string courseId)
        {
            try
            {
                var department = await GetDepartmentByIdAsync(departmentId);
                if (department == null) return false;
                
                var level = department.Levels.Find(l => l.LevelType == levelType);
                if (level == null) return false;
                
                bool removed = level.RemoveCourseId(courseId);
                if (!removed) return false;
                
                // TODO: Update department in Firebase
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }














