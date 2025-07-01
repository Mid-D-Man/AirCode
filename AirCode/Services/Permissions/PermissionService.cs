
using AirCode.Domain.Enums;
using AirCode.Services.Auth;
using AirCode.Services.Courses;
namespace AirCode.Services.Permissions
{
    public class PermissionService : IPermissionService
    {
        private readonly IAuthService _authService;
        private readonly ICourseService _courseService;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(
            IAuthService authService,
            ICourseService courseService,
            ILogger<PermissionService> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _courseService = courseService ?? throw new ArgumentNullException(nameof(courseService));
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
                    "lectureradmin" => await IsLecturerAssignedToCourseAsync(courseId),
                    "courserepadmin" => await IsCourseRepAuthorizedForCourseAsync(courseId),
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
                    "lectureradmin" => await IsLecturerAssignedToCourseAsync(courseId),
                    "courserepadmin" => await IsCourseRepAuthorizedForCourseAsync(courseId),
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
                    "lectureradmin" => await IsLecturerAssignedToCourseAsync(courseId),
                    "courserepadmin" => await IsCourseRepAuthorizedForCourseAsync(courseId),
                    "student" => await IsStudentEnrolledInCourseAsync(courseId),
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
                    "lectureradmin" => await IsLecturerAssignedToCourseAsync(courseId),
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
                    "lectureradmin" => await IsLecturerAssignedToCourseAsync(courseId),
                    "courserepadmin" when string.IsNullOrEmpty(courseId) => false, // Course reps can only view specific course reports
                    "courserepadmin" => await IsCourseRepAuthorizedForCourseAsync(courseId),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking view reports permission for user {UserId}, course {CourseId}", userId, courseId);
                return false;
            }
        }

        // Private helper methods for authorization logic
        private async Task<bool> IsLecturerAssignedToCourseAsync(string courseCode)
        {
            try
            {
                var lecturerId = await _authService.GetLecturerIdAsync();
                if (string.IsNullOrEmpty(lecturerId)) 
                    return false;

                var course = await _courseService.GetCourseByIdAsync(courseCode);
                if (course == null) 
                    return false;

                return course.LecturerIds.Contains(lecturerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking lecturer assignment for course {CourseCode}", courseCode);
                return false;
            }
        }

        private async Task<bool> IsCourseRepAuthorizedForCourseAsync(string courseCode)
        {
            try
            {
                var matricNumber = await _authService.GetMatricNumberAsync();
                if (string.IsNullOrEmpty(matricNumber)) 
                    return false;

                // Get course details to check level
                var course = await _courseService.GetCourseByIdAsync(courseCode);
                if (course == null) 
                    return false;

                // Get student's current level from their enrollment data
                var studentCourse = await _courseService.GetStudentCoursesByMatricAsync(matricNumber);
                if (studentCourse == null) 
                    return false;

                // Course rep can only manage courses for their current level
                return studentCourse.StudentLevel == course.Level;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking course rep authorization for course {CourseCode}", courseCode);
                return false;
            }
        }

        private async Task<bool> IsStudentEnrolledInCourseAsync(string courseCode)
        {
            try
            {
                var matricNumber = await _authService.GetMatricNumberAsync();
                if (string.IsNullOrEmpty(matricNumber)) 
                    return false;

                var studentCourse = await _courseService.GetStudentCoursesByMatricAsync(matricNumber);
                if (studentCourse == null) 
                    return false;

                // Check if student is enrolled in the specific course
                var courseRef = studentCourse.StudentCoursesRefs
                    .FirstOrDefault(c => c.CourseCode == courseCode);

                return courseRef != null && 
                       courseRef.CourseEnrollmentStatus == CourseEnrollmentStatus.Enrolled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking student enrollment for course {CourseCode}", courseCode);
                return false;
            }
        }

        // Additional helper for level-based course access validation
        private async Task<bool> IsStudentLevelCompatibleWithCourseAsync(string courseCode)
        {
            try
            {
                var matricNumber = await _authService.GetMatricNumberAsync();
                if (string.IsNullOrEmpty(matricNumber)) 
                    return false;

                var course = await _courseService.GetCourseByIdAsync(courseCode);
                var studentCourse = await _courseService.GetStudentCoursesByMatricAsync(matricNumber);

                if (course == null || studentCourse == null) 
                    return false;

                // Student's level should match or be higher than course level
                return studentCourse.StudentLevel >= course.Level;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking student level compatibility for course {CourseCode}", courseCode);
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
    }
}