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
using AirCode.Services.Auth.Offline;
using AirCode.Services.Cryptography;

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

//Auth(auth0 ish) service
builder.Services.AddScoped<IAuthService, AuthService>();


// Local storage
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();
//local crypto
builder.Services.AddScoped<ICryptographyService, CryptographyService>();
//offline crendentials
builder.Services.AddScoped<IOfflineCredentialsService, OfflineCredentialsService>();

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

//factory issue not leaving /
builder.Services.AddScoped(typeof(AccountClaimsPrincipalFactory<RemoteUserAccount>),
    typeof(CustomAccountFactory));


await builder.Build().RunAsync();