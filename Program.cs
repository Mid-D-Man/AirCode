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
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.AdditionalProviderParameters.Add("audience", builder.Configuration["Auth0:Audience"]);
});

// Add auth state provider
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<RoleAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<RoleAuthStateProvider>());
// Add JWT token validation
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Add HTTP client factory
builder.Services.AddHttpClient("AirCodeAPI", client => 
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Add scoped HTTP client for authorized requests
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("AirCodeAPI"));


//localstorage
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();
builder.Services.AddScoped<IOfflineCredentialService, OfflineCredentialService>();
//scanner
builder.Services.AddScoped<IZxingScannerService, ZxingScannerService>();

//auth registry
builder.Services.AddScoped<IUserStorageService, UserStorageService>();

//visual elements
builder.Services.AddScoped<ISvgIconService, SvgIconService>();


// Add services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ISearchContextService, SearchContextService>();

// Firebase Services
builder.Services.AddScoped<AirCode.Services.Firebase.IFirestoreService, AirCode.Services.Firebase.FirestoreService>();

// Register Auth0 settings
var auth0Settings = new Auth0Settings();
builder.Services.AddSingleton(auth0Settings);

// Register Auth0 service
builder.Services.AddScoped<IAuth0Service, Auth0Service>();

// Add HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });


await builder.Build().RunAsync();

