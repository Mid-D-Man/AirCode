using AirCode.Services.Time;
using Microsoft.AspNetCore.Components;

namespace AirCode.Services.Guards;


public class BusinessHoursGuard : IBusinessHoursGuard
{
    //imple buss guard as last last thing not now though test out time service to
    private readonly IServerTimeService _serverTimeService;
    private readonly string[] _exemptRoutes = { "outside-business-hours", "error", "loading" };

    public BusinessHoursGuard(IServerTimeService serverTimeService)
    {
        _serverTimeService = serverTimeService;
    }

    public async Task<bool> CheckBusinessHoursAsync()
    {
        try
        {
            await _serverTimeService.GetCurrentServerTimeAsync();
            return _serverTimeService.IsBusinessHours();
        }
        catch
        {
            return true; // Fail-safe: allow access on error
        }
    }

    public async Task ValidateAccessAsync(NavigationManager navigationManager)
    {
        var currentPath = new Uri(navigationManager.Uri).AbsolutePath;
            
        if (IsExemptRoute(currentPath))
            return;

        var isBusinessHours = await CheckBusinessHoursAsync();
            
        if (!isBusinessHours)
        {
            navigationManager.NavigateTo("outside-business-hours");
        }
    }

    private bool IsExemptRoute(string path)
    {
        return _exemptRoutes.Any(route => path.StartsWith(route, StringComparison.OrdinalIgnoreCase));
    }
}

