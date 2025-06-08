using System.Threading.Tasks;

namespace AirCode.Services.Permissions
{
    public interface IPermissionService
    {
        Task<bool> CanEditAttendanceAsync(string userId, string courseId);
        Task<bool> CanStartAttendanceEventAsync(string userId, string courseId);
        Task<bool> CanViewCourseAttendanceAsync(string userId, string courseId);
        Task<bool> CanEditCourseInfoAsync(string userId, string courseId);
        Task<bool> CanGenerateAdminIdAsync(string userId);
    }
}