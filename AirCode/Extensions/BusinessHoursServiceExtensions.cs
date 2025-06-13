using AirCode.Services.Guards;
using AirCode.Services.Time;

namespace AirCode.Extensions;

public static class BusinessHoursServiceExtensions
{
    public static IServiceCollection AddBusinessHoursGuard(this IServiceCollection services)
    {
        services.AddScoped<IServerTimeService, ServerTimeService>();
        services.AddScoped<IBusinessHoursGuard, BusinessHoursGuard>();
        return services;
    }
}