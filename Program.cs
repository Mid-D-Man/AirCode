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

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });


//localstorage
builder.Services.AddScoped<IBlazorAppLocalStorageService, BlazorAppLocalStorageService>();

//scanner
builder.Services.AddScoped<IZxingScannerService, ZxingScannerService>();

//auth registry
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<IUserStorageService, UserStorageService>();

//visual elements
builder.Services.AddScoped<ISvgIconService, SvgIconService>();


// Add services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ISearchContextService, SearchContextService>();

// Firebase Services
builder.Services.AddScoped<AirCode.Services.Firebase.IFirestoreService, AirCode.Services.Firebase.FirestoreService>();

await builder.Build().RunAsync();

