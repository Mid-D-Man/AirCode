using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AirCode.Components.SharedPrefabs.Others;

public partial class PWAComponent : ComponentBase
{
    [Parameter] public bool ShowControls { get; set; } = true;
    [Parameter] public EventCallback<PWAStatus> OnStatusChanged { get; set; }
    [Parameter] public EventCallback<bool> OnOfflineStatusChanged { get; set; }

    private IJSObjectReference? _pwaManager;
    private DotNetObjectReference<PWAComponent>? _dotNetRef;

    // State Properties
    private bool IsInstalled { get; set; }
    private bool IsInstallable { get; set; }
    private bool UpdateAvailable { get; set; }
    private bool IsOffline { get; set; }
    private bool IsCurrentRouteOfflineAccessible { get; set; }
    private string StatusMessage { get; set; } = string.Empty;
    private List<OfflineRoute> OfflineRoutes { get; set; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await InitializePWA();
    }

    private async Task InitializePWA()
    {
        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _pwaManager = await JSRuntime.InvokeAsync<IJSObjectReference>("getPWAManager");

            await InitializeOfflineRoutes();
            await InitializeConnectivityService();
            await SetupPWAMonitoring();
            await UpdatePWAStatus();

            StatusMessage = "PWA initialized successfully";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"PWA initialization failed: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task InitializeConnectivityService()
    {
        try
        {
            await ConnectivityService.InitializeAsync();
            ConnectivityService.ConnectivityChanged += OnConnectivityStatusChanged;
            
            var status = await ConnectivityService.GetConnectivityStatusAsync();
            IsOffline = !status.IsOnline;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize connectivity service: {ex.Message}");
        }
    }

    private async Task SetupPWAMonitoring()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("setupPWAStatusMonitoring", _dotNetRef);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to setup PWA monitoring: {ex.Message}");
        }
    }

    private async Task InitializeOfflineRoutes()
    {
        OfflineRoutes = new List<OfflineRoute>
        {
            new() { Url = "/", DisplayName = "Dashboard" },
            new() { Url = "/Admin/OfflineAttendanceEven", DisplayName = "Offline Attendance" },
            new() { Url = "/Client/OfflineScan", DisplayName = "Offline Scan" }
        };

        var currentPath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        IsCurrentRouteOfflineAccessible = OfflineRoutes.Any(r =>
            string.Equals(r.Url.TrimStart('/'), currentPath, StringComparison.OrdinalIgnoreCase) ||
            currentPath.StartsWith(r.Url.TrimStart('/') + "/", StringComparison.OrdinalIgnoreCase)
        );
    }

    private async Task UpdatePWAStatus()
    {
        if (_pwaManager == null) return;

        try
        {
            var status = await _pwaManager.InvokeAsync<PWAStatus>("getInstallationStatus");
            IsInstalled = status.IsInstalled;
            IsInstallable = status.IsInstallable;
            UpdateAvailable = status.UpdateAvailable;

            await OnStatusChanged.InvokeAsync(status);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to get PWA status: {ex.Message}";
        }
    }

    private void OnConnectivityStatusChanged(ConnectivityStatus status)
    {
        InvokeAsync(async () =>
        {
            var wasOffline = IsOffline;
            IsOffline = !status.IsOnline;

            if (wasOffline != IsOffline)
            {
                await OnOfflineStatusChanged.InvokeAsync(IsOffline);
                StatusMessage = IsOffline ? "Connection lost - Working offline" : "Connection restored";
                StateHasChanged();
            }
        });
    }

    private async Task ForceConnectivityCheck()
    {
        try
        {
            StatusMessage = "Checking connection...";
            StateHasChanged();

            await ConnectivityService.ForceCheckAsync();
            var status = await ConnectivityService.GetConnectivityStatusAsync();
            
            IsOffline = !status.IsOnline;
            StatusMessage = IsOffline ? "Still offline" : "Connection restored";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection check failed: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task CheckForUpdates()
    {
        if (_pwaManager == null) return;

        try
        {
            StatusMessage = "Checking for updates...";
            StateHasChanged();

            await _pwaManager.InvokeVoidAsync("checkForUpdates");
            await Task.Delay(2000);
            await UpdatePWAStatus();

            StatusMessage = UpdateAvailable ? "Update available!" : "No updates available";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update check failed: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task ApplyUpdate()
    {
        if (_pwaManager == null) return;

        try
        {
            StatusMessage = "Applying update...";
            StateHasChanged();

            await _pwaManager.InvokeVoidAsync("applyUpdate");
            StatusMessage = "Update applied. Page will reload shortly.";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update failed: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task GetAppInfo()
    {
        if (_pwaManager == null) return;

        try
        {
            var status = await _pwaManager.InvokeAsync<PWAStatus>("getInstallationStatus");
            var connectivityStatus = await ConnectivityService.GetConnectivityStatusAsync();
            
            StatusMessage = $"App - Installed: {status.IsInstalled}, SW: {status.HasServiceWorker}, " +
                          $"Offline: {!connectivityStatus.IsOnline}, Quality: {connectivityStatus.NetworkQuality}";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to get app info: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task ShowOfflineRoutes()
    {
        var routeList = string.Join(", ", OfflineRoutes.Select(r => r.DisplayName));
        StatusMessage = $"Available offline: {routeList}";
        StateHasChanged();
    }

    // JS Invokable Methods
    [JSInvokable]
    public async Task OnPWAStatusChanged(PWAStatus status)
    {
        IsInstalled = status.IsInstalled;
        IsInstallable = status.IsInstallable;
        UpdateAvailable = status.UpdateAvailable;

        await OnStatusChanged.InvokeAsync(status);
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnUpdateAvailable()
    {
        UpdateAvailable = true;
        StatusMessage = "New version available!";
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnAppInstalled()
    {
        IsInstalled = true;
        StatusMessage = "App installed successfully!";
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnVisibilityChange(bool visible)
    {
        if (visible)
            await ConnectivityService.ForceCheckAsync();
    }

    [JSInvokable]
    public async Task OnAppFocus() => await ConnectivityService.ForceCheckAsync();

    [JSInvokable]
    public async Task OnAppBlur()
    {
        // Background sync triggers if needed
    }

    [JSInvokable]
    public async Task OnAppFreeze()
    {
        // Save state for mobile freeze
    }

    [JSInvokable]
    public async Task OnAppResume() => await ConnectivityService.ForceCheckAsync();

    [JSInvokable]
    public async Task OnBeforeUnload()
    {
        // Cleanup before app closes
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        ConnectivityService.ConnectivityChanged -= OnConnectivityStatusChanged;
        
        if (_pwaManager != null)
            await _pwaManager.DisposeAsync();
        
        _dotNetRef?.Dispose();
    }

    // Supporting Classes
    public class PWAStatus
    {
        public bool IsInstallable { get; set; }
        public bool IsInstalled { get; set; }
        public bool HasServiceWorker { get; set; }
        public bool UpdateAvailable { get; set; }
    }

    public class OfflineRoute
    {
        public string Url { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}