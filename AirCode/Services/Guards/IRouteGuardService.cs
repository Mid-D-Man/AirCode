using System.Security.Claims;

namespace AirCode.Services.Guards
{
    public interface IRouteGuardService
    {
        Task<bool> CanAccessRouteAsync(string route, ClaimsPrincipal user);
        Task<bool> HasRoleAccessAsync(string[] roles, ClaimsPrincipal user);
        Task<string> GetRedirectUrlForRoleAsync(ClaimsPrincipal user);
    }
}