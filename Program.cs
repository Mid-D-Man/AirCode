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

// Configure Auth0 authentication
builder.Services.AddOidcAuthentication(options =>
{
    // Bind standard Auth0 configuration values.
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    
    // Use Code Flow with PKCE for improved security.
    options.ProviderOptions.ResponseType = "code";

    // Specify the intended API in the additional parameters.
    options.ProviderOptions.AdditionalProviderParameters.Add("audience", builder.Configuration["Auth0:Audience"]);

    // Tell the user options which claim holds the roles.
    // This should match the namespaced claim from your Auth0 action.
    options.UserOptions.RoleClaim = "https://air-code/roles";

    // Optional: Add default scopes if needed.
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
  
});


// HTTP clients setup
// Base client without auth
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// HTTP client with Auth0 token
builder.Services.AddHttpClient("AirCodeAPI", 
        client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("AirCodeAPI"));

// Clear default JWT claim mappings to preserve original claim names from Auth0
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Add authorization services
builder.Services.AddAuthorizationCore();

// Add this line to your existing services registration in Program.cs
builder.Services.AddScoped<IAuthService, AuthService>();


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

await builder.Build().RunAsync();