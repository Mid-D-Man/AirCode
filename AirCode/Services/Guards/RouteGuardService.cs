using System.Security.Claims;
namespace AirCode.Services.Guards
{
    public class RouteGuardService : IRouteGuardService
    {
        public async Task<bool> CanAccessRouteAsync(string route, ClaimsPrincipal user)
        {
            if (!user.Identity?.IsAuthenticated ?? true) return false;

            var routeRoleMap = new Dictionary<string, string[]>
            {
                { "Admin/SuperiorDashboard", new[] { "superioradmin" } },
                { "Admin/Dashboard", new[] { "lectureradmin", "courseadmin" } },
                { "Client/ScanAttendance", new[] { "student" } },
                { "Client/Dashboard", new[] { "student" } }
            };

            if (routeRoleMap.TryGetValue(route, out var requiredRoles))
            {
                return await HasRoleAccessAsync(requiredRoles, user);
            }

            return true; // Allow access to unprotected routes
        }

        public async Task<bool> HasRoleAccessAsync(string[] roles, ClaimsPrincipal user)
        {
            return await Task.FromResult(roles.Any(role => user.IsInRole(role)));
        }

        public async Task<string> GetRedirectUrlForRoleAsync(ClaimsPrincipal user)
        {
            var role = user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value?.ToLower();
            
            return await Task.FromResult(role switch
            {
                "superioradmin" => "Admin/SuperiorDashboard",
                "lectureradmin" => "Admin/Dashboard",
                "courseadmin" => "Admin/Dashboard",
                "student" => "Client/ScanAttendance",
                _ => "/"
            });
        }
    }
}