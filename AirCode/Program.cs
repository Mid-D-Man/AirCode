using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Supabase;
using Blazored.LocalStorage;
using Blazored.Toast;

// AirCode Application Services
using AirCode;
using AirCode.Extensions;
using AirCode.Models.Firebase;
using AirCode.Services.Attendance;
using AirCode.Services.Auth;
using AirCode.Services.Courses;
using AirCode.Services.Cryptography;
using AirCode.Services.Department;
using AirCode.Services.Guards;
using AirCode.Services.Permissions;
using AirCode.Services.Search;
using AirCode.Services.Storage;
using AirCode.Services.Academic;
using AirCode.Services.Exports;
using AirCode.Services.Firebase;
using AirCode.Services.VisualElements;
using AirCode.Services.Config; // Add this line
using AirCode.Utilities.DataStructures;
using AirCode.Utilities.HelperScripts;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

// ============================================================================
// AirCode Blazor WebAssembly Application Configuration
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
    options.ProviderOptions.PostLogoutRedirectUri = "https://mid-d-man.github.io/AirCode/authentication/logout-callback";

    // Add logout callback URI
    options.ProviderOptions.AdditionalProviderParameters.Add("post_logout_redirect_uri", 
        "https://mid-d-man.github.io/AirCode/authentication/logout-callback");
});
// Program.cs - Add after builder.Services.AddOidcAuthentication
builder.Services.AddApiAuthorization();
// Preserve original JWT claim names from Auth0 (prevent automatic claim mapping)
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Define role-based authorization policies for AirCode's multi-tier access system
builder.Services.AddAuthorizationCore(options => 
{
    // Match Auth0 role casing exactly
    options.AddPolicy("SuperiorAdmin", policy => 
        policy.RequireRole("SuperiorAdmin"));
        
    options.AddPolicy("LecturerAdmin", policy => 
        policy.RequireRole("LecturerAdmin"));
        
    options.AddPolicy("CourseRepAdmin", policy => 
        policy.RequireRole("CourseRepAdmin"));
        
    options.AddPolicy("Student", policy => 
        policy.RequireRole("Student"));
        
    // Update composite policies accordingly
    options.AddPolicy("AnyAdmin", policy => 
        policy.RequireRole("SuperiorAdmin", "LecturerAdmin", "CourseRepAdmin"));
    
    options.AddPolicy("StandardAdmin", policy => 
        policy.RequireRole("LecturerAdmin", "CourseRepAdmin"));
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
// Core Logging 
// ============================================================================
builder.Services.AddLogging();

// ============================================================================
// CRITICAL: Add Blazored Services BEFORE other service registrations
// ============================================================================
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredToast();

// ============================================================================
// Configuration Services - Add EARLY in the registration process
// ============================================================================
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

// ============================================================================
// Core Authentication & Security Services
// ============================================================================
builder.Services.AddScoped<ICryptographyService, CryptographyService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOfflineCredentialsService, OfflineCredentialsService>();
builder.Services.AddScoped<IOfflineSyncService, OfflineSyncService>();
builder.Services.AddScoped<IOfflineAttendanceClientService, OfflineAttendanceClientService>();
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
builder.Services.AddScoped<IAcademicSessionService, AcademicSessionService>();

// Application state and utility services
builder.Services.AddScoped<ISearchContextService, SearchContextService>();
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<QRCodeDecoder>();
builder.Services.AddSingleton<ConnectivityService>();
// UI and visual component services
builder.Services.AddScoped<ISvgIconService, SvgIconService>();
builder.Services.AddScoped<IPdfExportService, PdfExportService>();
builder.Services.AddScoped<IBackdropService, BackdropService>();

// ============================================================================
// External Service Integrations    
// ============================================================================

// Firebase Firestore for real-time data synchronization
builder.Services.AddScoped<IFirestoreService, FirestoreService>();
builder.Services.AddScoped<IFirestoreNotificationService, FirestoreNotificationService>();
// Supabase integration for backend services
builder.Services.AddSupabaseServices();
builder.Services.AddBusinessHoursGuard();
builder.Services.AddScoped<IAttendanceSessionService, AttendanceSessionService>();
builder.Services.AddScoped<IFirestoreAttendanceService, FirestoreAttendanceService>();
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
// Application Startup - Build AFTER all service registrations
// ============================================================================
var app = builder.Build();

// Initialize MID_HelperFunctions after building the app
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var jsRuntime = app.Services.GetRequiredService<IJSRuntime>();
MID_HelperFunctions.Initialize(logger, jsRuntime);

// Initialize configuration and push to JavaScript
try
{
    var configService = app.Services.GetRequiredService<IConfigurationService>();
    await configService.PushConfigToJavaScriptAsync();
    logger.LogInformation("Configuration service initialized successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize configuration service");
}

await app.RunAsync();