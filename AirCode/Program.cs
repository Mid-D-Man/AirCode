using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Supabase;

// AirCode Application Services
using AirCode;
using AirCode.Extensions;
using AirCode.Services.Attendance;
using AirCode.Services.Auth;
using AirCode.Services.Auth.Offline;
using AirCode.Services.Courses;
using AirCode.Services.Cryptography;
using AirCode.Services.Department;
using AirCode.Services.Guards;
using AirCode.Services.Permissions;
using AirCode.Services.Search;
using AirCode.Services.Storage;
using AirCode.Services.VisualElements;
using AirCode.Utilities.DataStructures;
using AirCode.Utilities.HelperScripts;

// ============================================================================
// AirCode Blazor WebAssembly Application Configuration
// Educational platform with multi-role authentication and real-time features
// ============================================================================

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ============================================================================
// Component Registration
// ============================================================================
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ============================================================================
// Authentication & Authorization Configuration
// ============================================================================

// Configure Auth0 OIDC Authentication with PKCE flow for enhanced security
builder.Services.AddOidcAuthentication(options =>
{
    // Bind Auth0 configuration from appsettings
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    
    // Use Authorization Code Flow with PKCE (more secure than implicit flow)
    options.ProviderOptions.ResponseType = "code";

    // Configure API audience for token validation
    options.ProviderOptions.AdditionalProviderParameters.Add("audience", builder.Configuration["Auth0:Audience"]);

    // Map custom role claim namespace (configured in Auth0 rules/actions)
    options.UserOptions.RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    // Essential OpenID Connect scopes
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    
    // GitHub Pages deployment URIs
    options.ProviderOptions.RedirectUri = "https://mid-d-man.github.io/AirCode/authentication/login-callback";
    options.ProviderOptions.PostLogoutRedirectUri = "https://mid-d-man.github.io/AirCode/";
});

// Preserve original JWT claim names from Auth0 (prevent automatic claim mapping)
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Define role-based authorization policies for AirCode's multi-tier access system
builder.Services.AddAuthorizationCore(options => 
{
    // Highest privilege level - full system administration
    options.AddPolicy("SuperiorAdmin", policy => 
        policy.RequireRole("superioradmin"));
        
    // Academic staff management capabilities
    options.AddPolicy("LecturerAdmin", policy => 
        policy.RequireRole("lectureradmin"));
        
    // Course content and enrollment management
    options.AddPolicy("CourseRepAdmin", policy => 
        policy.RequireRole("courserepadmin"));
        
    // Standard student access level
    options.AddPolicy("Student", policy => 
        policy.RequireRole("student"));
        
    // Composite policy for any administrative role
    options.AddPolicy("AnyAdmin", policy => 
        policy.RequireRole("superioradmin", "lectureradmin", "courserepadmin"));
});

// Custom account factory for enhanced user claims processing
builder.Services.AddScoped(typeof(AccountClaimsPrincipalFactory<RemoteUserAccount>),
    typeof(CustomAccountFactory));

// ============================================================================
// HTTP Client Configuration
// ============================================================================

// Base HTTP client for general requests
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Authenticated HTTP client for AirCode API calls with automatic token attachment
builder.Services.AddHttpClient("AirCodeAPI", 
        client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Primary HTTP client service (uses authenticated client)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("AirCodeAPI"));

// ============================================================================
// Core Authentication & Security Services
// ============================================================================
builder.Services.AddScoped<ICryptographyService, CryptographyService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOfflineCredentialsService, OfflineCredentialsService>();
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();

// ============================================================================
// Business Logic Services
// ============================================================================

// User permissions and role management
builder.Services.AddScoped<IRouteGuardService, RouteGuardService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Academic structure services
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<ICourseService, CourseService>();

// Application state and utility services
builder.Services.AddScoped<ISearchContextService, SearchContextService>();
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<QRCodeDecoder>();

// UI and visual component services
builder.Services.AddScoped<ISvgIconService, SvgIconService>();

// ============================================================================
// External Service Integrations
// ============================================================================

// Firebase Firestore for real-time data synchronization
builder.Services.AddScoped<AirCode.Services.Firebase.IFirestoreService, AirCode.Services.Firebase.FirestoreService>();

// Supabase integration for backend services
builder.Services.AddSupabaseServices();

// Supabase client configuration optimized for WebAssembly
builder.Services.AddScoped<Supabase.Client>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var supabaseUrl = configuration["Supabase:Url"] ?? throw new ArgumentNullException("Supabase:Url");
    var supabaseKey = configuration["Supabase:AnonKey"] ?? throw new ArgumentNullException("Supabase:AnonKey");
    
    var options = new Supabase.SupabaseOptions
    {
        AutoConnectRealtime = false,  // Disabled for WebAssembly performance
        AutoRefreshToken = true,      // Maintain session continuity
        SessionHandler = new DefaultSupabaseSessionHandler()
    };
    
    return new Supabase.Client(supabaseUrl, supabaseKey, options);
});

// ============================================================================
// Application Startup
// ============================================================================
await builder.Build().RunAsync();