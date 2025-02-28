using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Aircode;
using Aircode.Services;
using Blazored.LocalStorage;
using Syncfusion.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

//localstorage
builder.Services.AddBlazoredLocalStorage();

// Register Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();
builder.Services.AddScoped<IScannerService, ScannerService>();

//auth registry
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<IUserStorageService, UserStorageService>();

await builder.Build().RunAsync();

//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