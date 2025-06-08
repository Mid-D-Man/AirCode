using System;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Storage;

namespace AirCode.Services.Permissions
{
    public class PermissionService : IPermissionService
    {
        private readonly IBlazorAppLocalStorageService _blazorAppLocalStorageService;
        
        public PermissionService(IBlazorAppLocalStorageService blazorAppLocalStorageService)
        {
            _blazorAppLocalStorageService = blazorAppLocalStorageService;
        }
        
        public async Task<bool> CanEditAttendanceAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserEntityAsync();
            if (user == null) return false;
            
            // Superior admin can edit any attendance
            if (user.Role == UserRole.SuperiorAdmin)
                return true;
                
            // Course rep admin can edit attendance for their assigned courses
            if (user.Role == UserRole.CourseRepAdmin && user.AssignedCourseIds.Contains(courseId))
                return true;
                
            return false;
        }
        
        public async Task<bool> CanStartAttendanceEventAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserEntityAsync();
            if (user == null) return false;
            
            // Students can't start attendance events
            if (user.Role == UserRole.Student)
                return false;
                
            // Check if user is associated with the course
            if (user.Role == UserRole.LecturerAdmin )
                return user.AssignedCourseIds.Contains(courseId);
                
            if (user.Role == UserRole.CourseRepAdmin || user.Role == UserRole.AssistantCourseRep)
                return user.AssignedCourseIds.Contains(courseId);
                
            // Superior admin can start any attendance event
            return user.Role == UserRole.SuperiorAdmin;
        }
        
        public async Task<bool> CanViewCourseAttendanceAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserEntityAsync();
            if (user == null) return false;
            
            // Superior admin can view any course attendance
            if (user.Role == UserRole.SuperiorAdmin)
                return true;
                
            // Lecturers can view attendance for their assigned courses
            if ((user.Role == UserRole.LecturerAdmin ) && 
                user.AssignedCourseIds.Contains(courseId))
                return true;
                
            // Course reps can view attendance for their assigned courses
            if ((user.Role == UserRole.CourseRepAdmin || user.Role == UserRole.AssistantCourseRep) &&
                user.AssignedCourseIds.Contains(courseId))
                return true;
                
            // Students can only view their own attendance for enrolled courses
            if (user.Role == UserRole.Student)
            {
                // This would require checking if the student is enrolled in the course
                // Simplified for now
                return user.AssignedCourseIds.Contains(courseId);
            }
            
            return false;
        }
        
        public async Task<bool> CanEditCourseInfoAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserEntityAsync();
            if (user == null) return false;
            
            // Superior admin can edit any course info
            if (user.Role == UserRole.SuperiorAdmin)
                return true;
                
            // Lecturer admin can edit their own course info
            if (user.Role == UserRole.LecturerAdmin && user.AssignedCourseIds.Contains(courseId))
                return true;
                
            return false;
        }
        
        public async Task<bool> CanGenerateAdminIdAsync(string userId)
        {
            var user = await GetCurrentUserEntityAsync();
            return user?.Role == UserRole.SuperiorAdmin;
        }
        
        private async Task<UserEntity> GetCurrentUserEntityAsync()
        {
            try
            {
                return await _blazorAppLocalStorageService.GetItemAsync<UserEntity>("CurrentUser");
            }
            catch
            {
                return null;
            }
        }
    }
}