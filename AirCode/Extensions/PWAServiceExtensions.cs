using AirCode.Services.PWA;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace AirCode.Extensions
{
    public static class PWAServiceExtensions
    {
        public static IServiceCollection AddPWAServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register PWA configuration with manual binding
            services.Configure<PWAConfiguration>(options =>
            {
                var pwaSection = configuration.GetSection(PWAConfiguration.SectionName);
                pwaSection.Bind(options);
            });
            
            // Register PWA service
            services.AddScoped<IPWAService, PWAService>();
            
            return services;
        }
    }
}