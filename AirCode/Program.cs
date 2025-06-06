using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AirCode;
using AirCode.Extensions;
using AirCode.Services.Auth;
using AirCode.Services.Auth.Offline;
using AirCode.Services.Cryptography;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Supabase;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure Auth0 authentication
builder.Services.AddOidcAuthentication(options =>
{
    // Bind standard Auth0 configuration values.
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    
    //  Code Flow with PKCE for improved security.
    options.ProviderOptions.ResponseType = "code";

    // Specify the intended API in the additional parameters.
    options.ProviderOptions.AdditionalProviderParameters.Add("audience", builder.Configuration["Auth0:Audience"]);

    //Our role claim namespace inserted during the whole post login flow
    options.UserOptions.RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    // Optional: Add default scopes if needed.
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    
    options.ProviderOptions.RedirectUri = "https://mid-d-man.github.io/AirCode/authentication/login-callback";
    options.ProviderOptions.PostLogoutRedirectUri = "https://mid-d-man.github.io/AirCode/";
   
  
});

// Clear default JWT claim mappings to preserve original claim names from Auth0
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Add authorization services
builder.Services.AddAuthorizationCore(options => 
{
    // Define policies for different roles
    options.AddPolicy("SuperiorAdmin", policy => 
        policy.RequireRole("superioradmin"));
        
    options.AddPolicy("LecturerAdmin", policy => 
        policy.RequireRole("lectureradmin"));
        
    options.AddPolicy("CourseAdmin", policy => 
        policy.RequireRole("courseadmin"));
        
    options.AddPolicy("Student", policy => 
        policy.RequireRole("student"));
        
    options.AddPolicy("AnyAdmin", policy => 
        policy.RequireRole("superioradmin", "lectureradmin", "courseadmin"));
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// HTTP client with Auth0 token
builder.Services.AddHttpClient("AirCodeAPI", 
        client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("AirCodeAPI"));


//Auth(auth0 ish) service
builder.Services.AddScoped<ICryptographyService, CryptographyService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOfflineCredentialsService, OfflineCredentialsService>();
//add custom configs here 


// Add all supabase services
builder.Services.AddSupabaseServices();

// Supabase Client Configuration
builder.Services.AddScoped<Supabase.Client>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var supabaseUrl = configuration["Supabase:Url"] ?? throw new ArgumentNullException("Supabase:Url");
    var supabaseKey = configuration["Supabase:AnonKey"] ?? throw new ArgumentNullException("Supabase:AnonKey");
    
    var options = new Supabase.SupabaseOptions
    {
        AutoConnectRealtime = false,  // Disable realtime for WebAssembly
        AutoRefreshToken = true,
        SessionHandler = new DefaultSupabaseSessionHandler()
    };
    
    return new Supabase.Client(supabaseUrl, supabaseKey, options);
});
//factory issue not leaving /
builder.Services.AddScoped(typeof(AccountClaimsPrincipalFactory<RemoteUserAccount>),
    typeof(CustomAccountFactory));

await builder.Build().RunAsync();