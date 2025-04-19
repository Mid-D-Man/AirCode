using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AirCode;
using AirCode.Services.Scanner;
using AirCode.Services.Auth;
using AirCode.Services.Courses;
using AirCode.Services.Permissions;
using AirCode.Services.Search;
using AirCode.Services.Storage;
using AirCode.Services.VisualElements;

using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add auth services
builder.Services.AddOidcAuthentication(options =>
{
    // Bind Auth0 configuration
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    
    // Set required options
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.AdditionalProviderParameters.Add("audience", builder.Configuration["Auth0:Audience"]);
    
    // Configure Auth0 specific settings
    options.ProviderOptions.Authority = $"https://{builder.Configuration["Auth0:Domain"]}";
    options.ProviderOptions.MetadataUrl = $"https://{builder.Configuration["Auth0:Domain"]}/.well-known/openid-configuration";
    options.ProviderOptions.ClientId = builder.Configuration["Auth0:ClientId"];
});

// Add auth state provider
builder.Services.AddAuthorizationCore();

// Add JWT token validation - clear default mappings to preserve original claim names
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Add HTTP client factory
builder.Services.AddHttpClient("AirCodeAPI", client => 
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Add scoped HTTP client for authorized requests
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("AirCodeAPI"));

// Local storage
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();
builder.Services.AddScoped<IOfflineCredentialService, OfflineCredentialService>();

// Scanner
builder.Services.AddScoped<IZxingScannerService, ZxingScannerService>();

// Auth registry
builder.Services.AddScoped<IUserStorageService, UserStorageService>();

// Visual elements
builder.Services.AddScoped<ISvgIconService, SvgIconService>();

// Services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ISearchContextService, SearchContextService>();

// Firebase Services
builder.Services.AddScoped<AirCode.Services.Firebase.IFirestoreService, AirCode.Services.Firebase.FirestoreService>();

// Register Auth0 settings
var auth0Settings = new Auth0Settings
{
    Domain = builder.Configuration["Auth0:Domain"],
    ClientId = builder.Configuration["Auth0:ClientId"],
    Audience = builder.Configuration["Auth0:Audience"],
    RedirectUri = "authentication/login-callback"
};
builder.Services.AddSingleton(auth0Settings);

// Register Auth0 service (keep for backward compatibility if needed)
builder.Services.AddScoped<IAuth0Service, Auth0Service>();

await builder.Build().RunAsync();