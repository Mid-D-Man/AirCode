using AirCode.Services.Auth;
using AirCode.Services.Auth;
using AirCode.Services.Cryptography;

namespace AirCode.Extensions;

public static class ServiceExtensions
{
    /// <summary>
    /// Adds the cryptography service to the service collection
    /// </summary>
    public static IServiceCollection AddCryptographyService(this IServiceCollection services)
    {
        return services.AddScoped<ICryptographyService, CryptographyService>();
    }
        
    /// <summary>
    /// Adds the offline credentials service to the service collection
    /// </summary>
    public static IServiceCollection AddOfflineCredentialsService(this IServiceCollection services)
    {
        // Make sure to register CryptographyService first
        services.AddScoped<ICryptographyService, CryptographyService>();
        return services.AddScoped<IOfflineCredentialsService, OfflineCredentialsService>();
    }
}