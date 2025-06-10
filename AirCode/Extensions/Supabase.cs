using Microsoft.Extensions.DependencyInjection;
using AirCode.Services.SupaBase;

namespace AirCode.Extensions
{
    public static class SupabaseServiceExtensions
    {
        public static IServiceCollection AddSupabaseServices(this IServiceCollection services)
        {
            // Register Supabase services
            services.AddScoped<ISupabaseDatabase, SupabaseDatabase>();
            services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
               services.AddScoped<ISupabaseEdgeFunctionService, SupabaseEdgeFunctionService>();
            services.AddScoped<ICatService, CatService>();
            
            return services;
        }
    }
}