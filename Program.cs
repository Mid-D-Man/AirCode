using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AirCode;
using AirCode.Services;
using AirCode.Services.Auth;
using AirCode.Services.Courses;
using AirCode.Services.Permissions;
using AirCode.Services.Search;
using ZXingBlazor;
using AirCode.Services.Storage; // Add this namespace import

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

//localstorage
builder.Services.AddScoped<ILocalStorageService, BlazorLocalStorageService>();

//scanner
builder.Services.AddScoped<IScannerService, ScannerService>();

//auth registry
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<IUserStorageService, UserStorageService>();

// Add services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ISearchContextService, SearchContextService>();

await builder.Build().RunAsync();

