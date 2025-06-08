using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AirCode.Domain.Entities;
using AirCode.Domain.Enums;
using AirCode.Services.Auth;
using AirCode.Services.Storage;
using Microsoft.AspNetCore.Components.Authorization;

namespace AirCode.Services.Permissions
{
    public class PermissionService : IPermissionService
    {
        private readonly IBlazorAppLocalStorageService _storageService;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IAuthService _authService;
        
        public PermissionService(
            IBlazorAppLocalStorageService storageService,
            AuthenticationStateProvider authStateProvider,
            IAuthService authService)
        {
            _storageService = storageService;
            _authStateProvider = authStateProvider;
            _authService = authService;
        }
        
        public async Task<bool> CanEditAttendanceAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.Identity.IsAuthenticated) return false;
            
            // Superior admin can edit any attendance
            if (user.IsInRole("superioradmin")) return true;
                
            // Course rep admin can edit attendance for their assigned courses
            if (user.IsInRole("courseadmin") && await IsUserAssignedToCourseAsync(userId, courseId))
                return true;
                
            // Lecturers can edit attendance for their courses
            if (user.IsInRole("lectureradmin") && await IsUserAssignedToCourseAsync(userId, courseId))
                return true;
                
            return false;
        }
        
        public async Task<bool> CanStartAttendanceEventAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.Identity.IsAuthenticated) return false;
            
            // Students cannot start attendance events
            if (user.IsInRole("student")) return false;
                
            // Superior admin can start any attendance event
            if (user.IsInRole("superioradmin")) return true;
                
            // Check if admin is assigned to the course
            return await IsUserAssignedToCourseAsync(userId, courseId);
        }
        
        public async Task<bool> CanViewCourseAttendanceAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.Identity.IsAuthenticated) return false;
            
            // Superior admin can view any course attendance
            if (user.IsInRole("superioradmin")) return true;
                
            // Admins can view attendance for their assigned courses
            if ((user.IsInRole("lectureradmin") || user.IsInRole("courseadmin")) && 
                await IsUserAssignedToCourseAsync(userId, courseId))
                return true;
                
            // Students can view their own attendance for enrolled courses
            if (user.IsInRole("student"))
                return await IsStudentEnrolledInCourseAsync(userId, courseId);
            
            return false;
        }
        
        public async Task<bool> CanEditCourseInfoAsync(string userId, string courseId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.Identity.IsAuthenticated) return false;
            
            // Superior admin can edit any course info
            if (user.IsInRole("superioradmin")) return true;
                
            // Lecturer admin can edit their own course info
            if (user.IsInRole("lectureradmin") && await IsUserAssignedToCourseAsync(userId, courseId))
                return true;
                
            return false;
        }
        
        public async Task<bool> CanGenerateAdminIdAsync(string userId)
        {
            var user = await GetCurrentUserAsync();
            return user?.IsInRole("superioradmin") ?? false;
        }

        // Additional permission methods for enhanced security
        public async Task<bool> CanAccessOfflineModeAsync(string userId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.Identity.IsAuthenticated) return false;
            
            // Superior admins don't have offline mode access
            if (user.IsInRole("superioradmin")) return false;
            
            // Other roles can access offline mode
            return user.IsInRole("lectureradmin") || user.IsInRole("courseadmin") || user.IsInRole("student");
        }

        public async Task<bool> CanManageUsersAsync(string userId)
        {
            var user = await GetCurrentUserAsync();
            return user?.IsInRole("superioradmin") ?? false;
        }

        public async Task<bool> CanViewReportsAsync(string userId, string courseId = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.Identity.IsAuthenticated) return false;
            
            // Superior admin can view all reports
            if (user.IsInRole("superioradmin")) return true;
            
            // Lecturers can view reports for their courses
            if (user.IsInRole("lectureradmin"))
            {
                return string.IsNullOrEmpty(courseId) || await IsUserAssignedToCourseAsync(userId, courseId);
            }
            
            return false;
        }
        
        private async Task<ClaimsPrincipal> GetCurrentUserAsync()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            return authState.User;
        }
        
        private async Task<UserEntity> GetCurrentUserEntityAsync()
        {
            try
            {
                return await _storageService.GetItemAsync<UserEntity>("CurrentUser");
            }
            catch
            {
                return null;
            }
        }
        
        private async Task<bool> IsUserAssignedToCourseAsync(string userId, string courseId)
        {
            try
            {
                var userEntity = await GetCurrentUserEntityAsync();
                return userEntity?.AssignedCourseIds?.Contains(courseId) ?? false;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<bool> IsStudentEnrolledInCourseAsync(string userId, string courseId)
        {
            try
            {
                var userEntity = await GetCurrentUserEntityAsync();
                return userEntity?.AssignedCourseIds?.Contains(courseId) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}