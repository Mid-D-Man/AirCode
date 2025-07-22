using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AirCode.Components.SharedPrefabs.Others;

public partial class PWAComponent : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter] public bool ShowControls { get; set; } = true;
    [Parameter] public EventCallback<bool> OnOfflineStatusChanged { get; set; }

    private IJSObjectReference? _pwaManager;
    private DotNetObjectReference<PWAComponent>? _dotNetRef;

    // State
    private PWAStatus _status = new();
    private string _statusMessage = string.Empty;
    private readonly List<OfflineRoute> _offlineRoutes = new()
    {
        new() { Url = "/", DisplayName = "Dashboard" },
        new() { Url = "/Admin/OfflineAttendanceEven", DisplayName = "Offline Attendance" },
        new() { Url = "/Client/OfflineScan", DisplayName = "Offline Scan" }
    };
   
    private bool IsCurrentRouteOffline => _offlineRoutes.Any(r =>
        NavigationManager.ToBaseRelativePath(NavigationManager.Uri)
            .StartsWith(r.Url.TrimStart('/'), StringComparison.OrdinalIgnoreCase));
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await InitializePWA();
    }

    private async Task InitializePWA()
    {
        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _pwaManager = await JSRuntime.InvokeAsync<IJSObjectReference>("getPWAManager");
            
            await JSRuntime.InvokeVoidAsync("setupPWAMonitoring", _dotNetRef);
            await UpdateStatus();
            
            _statusMessage = "PWA ready";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _statusMessage = $"PWA init failed: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task UpdateStatus()
    {
        if (_pwaManager != null)
        {
            _status = await _pwaManager.InvokeAsync<PWAStatus>("getStatus");
        }
    }

    // User actions
    private async Task InstallApp()
    {
        if (_pwaManager != null)
        {
            var success = await _pwaManager.InvokeAsync<bool>("installApp");
            _statusMessage = success ? "Installing..." : "Install cancelled";
            StateHasChanged();
        }
    }

    private async Task ApplyUpdate()
    {
        if (_pwaManager != null)
        {
            await _pwaManager.InvokeVoidAsync("applyUpdate");
            _statusMessage = "Updating...";
            StateHasChanged();
        }
    }

    private async Task CheckForUpdates()
    {
        if (_pwaManager != null)
        {
            await _pwaManager.InvokeVoidAsync("checkForUpdates");
            await UpdateStatus();
            _statusMessage = _status.UpdateAvailable ? "Update available!" : "No updates";
            StateHasChanged();
        }
    }

    // JS Callbacks
    [JSInvokable] public async Task OnUpdateAvailable()
    {
        _status.UpdateAvailable = true;
        _statusMessage = "Update available!";
        StateHasChanged();
    }

    [JSInvokable] public async Task OnAppInstalled()
    {
        _status.IsInstalled = true;
        _status.IsInstallable = false;
        _statusMessage = "App installed!";
        StateHasChanged();
    }

    [JSInvokable] public async Task OnConnectivityChanged(bool isOnline)
    {
        var wasOffline = !_status.IsOnline;
        _status.IsOnline = isOnline;
        
        if (wasOffline != !isOnline)
        {
            await OnOfflineStatusChanged.InvokeAsync(!isOnline);
            _statusMessage = isOnline ? "Back online" : "Offline mode";
            StateHasChanged();
        }
    }

    [JSInvokable] public async Task OnVisibilityChange(bool visible)
    {
        if (visible && _pwaManager != null)
        {
            await UpdateStatus();
        }
    }

    private void ClearStatus()
    {
        _statusMessage = string.Empty;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_pwaManager != null) await _pwaManager.DisposeAsync();
        _dotNetRef?.Dispose();
    }

    public class PWAStatus
    {
        public bool IsInstallable { get; set; }
        public bool IsInstalled { get; set; }
        public bool HasServiceWorker { get; set; }
        public bool UpdateAvailable { get; set; }
        public bool IsOnline { get; set; } = true;
    }

    public class OfflineRoute
    {
        public string Url { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}