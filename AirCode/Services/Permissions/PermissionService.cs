using AirCode.Domain.Enums;
using AirCode.Services.Auth;

namespace AirCode.Services.Permissions
{
    public class PermissionService : IPermissionService
    {
        private readonly IAuthService _authService;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(
            IAuthService authService,
            ILogger<PermissionService> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> CanEditAttendanceAsync(string userId, string courseId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                
                return userRole switch
                {
                    "superioradmin" => true,
                    "lectureradmin" => true, // For now, allow all lecturers - implement specific checks later
                    "courserepadmin" => true, // For now, allow all course reps - implement specific checks later
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking edit attendance permission for user {UserId}, course {CourseId}", userId, courseId);
                return false;
            }
        }

        public async Task<bool> CanStartAttendanceEventAsync(string userId, string courseId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                
                // Students cannot start attendance events
                if (userRole == "student") 
                    return false;

                return userRole switch
                {
                    "superioradmin" => true,
                    "lectureradmin" => true, // For now, allow all lecturers - implement specific checks later
                    "courserepadmin" => true, // For now, allow all course reps - implement specific checks later
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking start attendance permission for user {UserId}, course {CourseId}", userId, courseId);
                return false;
            }
        }

        public async Task<bool> CanViewCourseAttendanceAsync(string userId, string courseId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                
                return userRole switch
                {
                    "superioradmin" => true,
                    "lectureradmin" => true, // For now, allow all lecturers - implement specific checks later
                    "courserepadmin" => true, // For now, allow all course reps - implement specific checks later
                    "student" => true, // For now, allow all students - implement enrollment checks later
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking view attendance permission for user {UserId}, course {CourseId}", userId, courseId);
                return false;
            }
        }

        public async Task<bool> CanEditCourseInfoAsync(string userId, string courseId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                
                return userRole switch
                {
                    "superioradmin" => true,
                    "lectureradmin" => true, // For now, allow all lecturers - implement specific checks later
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking edit course info permission for user {UserId}, course {CourseId}", userId, courseId);
                return false;
            }
        }

        public async Task<bool> CanGenerateAdminIdAsync(string userId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                return userRole == "superioradmin";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin ID generation permission for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> CanCacheAllStudentsLevel(string userId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                return userRole == "superioradmin";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin ID caching permission for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> CanCachePersonalStudentsLevel(string userId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                if (userRole == "courserepadmin" || userRole == "student")
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin ID caching permission for user {UserId}", userId);
                return false;
            }
        }

        // Enhanced permission methods
        public async Task<bool> CanAccessOfflineModeAsync(string userId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                
                // Superior admins don't have offline mode access for security
                return userRole != "superioradmin";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking offline mode permission for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> CanManageUsersAsync(string userId)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                return userRole == "superioradmin";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user management permission for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> CanViewReportsAsync(string userId, string courseId = null)
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync()) 
                    return false;

                var userRole = await _authService.GetUserRoleAsync();
                
                return userRole switch
                {
                    "superioradmin" => true,
                    "lectureradmin" when string.IsNullOrEmpty(courseId) => true,
                    "lectureradmin" => true, // For now, allow all lecturers - implement specific checks later
                    "courserepadmin" when string.IsNullOrEmpty(courseId) => false, // Course reps can only view specific course reports
                    "courserepadmin" => true, // For now, allow all course reps - implement specific checks later
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking view reports permission for user {UserId}, course {CourseId}", userId, courseId);
                return false;
            }
        }

        // Batch permission checking for performance optimization
        public async Task<Dictionary<string, bool>> CheckMultiplePermissionsAsync(string userId, 
            List<(string permission, string courseId)> permissionChecks)
        {
            var results = new Dictionary<string, bool>();
            
            try
            {
                if (!await _authService.IsAuthenticatedAsync())
                {
                    foreach (var (permission, _) in permissionChecks)
                    {
                        results[permission] = false;
                    }
                    return results;
                }

                // Batch process permissions to reduce repeated auth service calls
                var userRole = await _authService.GetUserRoleAsync();
                
                foreach (var (permission, courseId) in permissionChecks)
                {
                    results[permission] = permission switch
                    {
                        "CanEditAttendance" => await CanEditAttendanceAsync(userId, courseId),
                        "CanStartAttendanceEvent" => await CanStartAttendanceEventAsync(userId, courseId),
                        "CanViewCourseAttendance" => await CanViewCourseAttendanceAsync(userId, courseId),
                        "CanEditCourseInfo" => await CanEditCourseInfoAsync(userId, courseId),
                        _ => false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch permission checking for user {UserId}", userId);
                // Return false for all permissions on error
                foreach (var (permission, _) in permissionChecks)
                {
                    results[permission] = false;
                }
            }

            return results;
        }

        // TODO: Implement these methods when CourseService circular dependency is resolved
        // These would require CourseService to check specific course assignments and enrollments
        
        /*
        private async Task<bool> IsLecturerAssignedToCourseAsync(string courseCode)
        {
            // TODO: Implement when CourseService is available without circular dependency
            // This would check if the current lecturer is assigned to the specific course
            return true; // Placeholder - currently allowing all lecturers
        }

        private async Task<bool> IsCourseRepAuthorizedForCourseAsync(string courseCode)
        {
            // TODO: Implement when CourseService is available without circular dependency
            // This would check if the course rep's level matches the course level
            return true; // Placeholder - currently allowing all course reps
        }

        private async Task<bool> IsStudentEnrolledInCourseAsync(string courseCode)
        {
            // TODO: Implement when CourseService is available without circular dependency
            // This would check if the student is enrolled in the specific course
            return true; // Placeholder - currently allowing all students
        }

        private async Task<bool> IsStudentLevelCompatibleWithCourseAsync(string courseCode)
        {
            // TODO: Implement when CourseService is available without circular dependency
            // This would check if student's level is compatible with course level
            return true; // Placeholder
        }
        */
    }
}