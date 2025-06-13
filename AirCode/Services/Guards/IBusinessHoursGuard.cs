using Microsoft.AspNetCore.Components;

namespace AirCode.Services.Guards;

public interface IBusinessHoursGuard
{
    Task<bool> CheckBusinessHoursAsync();
    Task ValidateAccessAsync(NavigationManager navigationManager);
}