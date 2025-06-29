using AirCode.Services.PWA;

namespace AirCode.Extensions
{
    public static class PWAServiceExtensions
    {
        public static IServiceCollection AddPWAServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register PWA configuration
            var pwaSection = configuration.GetSection(PWAConfiguration.SectionName);
            services.Configure<PWAConfiguration>(pwaSection.Bind);
            // Register PWA service
            services.AddScoped<IPWAService, PWAService>();
            
            return services;
        }
    }
}