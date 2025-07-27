using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AirCode.Components.SharedPrefabs.Others;

public partial class PWAComponent : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter] public bool ShowControls { get; set; } = true;
    [Parameter] public EventCallback<bool> OnOfflineStatusChanged { get; set; }

    private IJSObjectReference? _airCodePWA;
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

    private bool ShouldShowInstallButton => 
        IsOnBasePage && 
        _status.IsInstallable && 
        !_status.IsInstalled;

    private Timer? _statusTimer;

    private bool IsOnBasePage
    {
        get
        {
            var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
            return string.IsNullOrEmpty(relativePath) || 
                   relativePath == "/" || 
                   relativePath == "AirCode" || 
                   relativePath == "AirCode/";
        }
    }
   
    private bool IsCurrentRouteOffline => _offlineRoutes.Any(r =>
        NavigationManager.ToBaseRelativePath(NavigationManager.Uri)
            .StartsWith(r.Url.TrimStart('/'), StringComparison.OrdinalIgnoreCase));
            
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) 
        {
            // Add delay to ensure scripts are loaded
            await Task.Delay(100);
            await InitializePWA();
        }
    }

    private async Task InitializePWA()
    {
        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            
            // Wait for PWA manager to be available with retry logic
            var maxRetries = 10;
            var retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    // Check if the PWA object exists and is properly initialized
                    var pwaAvailable = await JSRuntime.InvokeAsync<bool>("eval", 
                        "typeof window.AirCodePWA === 'object' && window.AirCodePWA !== null && typeof window.AirCodePWA.isInstalled === 'function'");
                    
                    if (pwaAvailable)
                    {
                        _airCodePWA = await JSRuntime.InvokeAsync<IJSObjectReference>("eval", "window.AirCodePWA");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PWA Component] Retry {retryCount + 1}: {ex.Message}");
                }
                
                retryCount++;
                await Task.Delay(200); // Wait 200ms between retries
            }
            
            if (_airCodePWA == null)
            {
                SetStatusWithTimeout("PWA not available - running in fallback mode");
                Console.WriteLine("[PWA Component] PWA manager not available after retries");
                return;
            }
            
            // Setup connectivity monitoring
            await JSRuntime.InvokeVoidAsync("setupConnectivityMonitoring", _dotNetRef);
            
            await UpdateStatus();
            SetStatusWithTimeout("PWA ready");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            SetStatusWithTimeout($"PWA init failed: {ex.Message}");
            Console.WriteLine($"[PWA Component] Initialization error: {ex}");
        }
    }

    private async Task UpdateStatus()
    {
        if (_airCodePWA != null)
        {
            try
            {
                _status.IsInstallable = await _airCodePWA.InvokeAsync<bool>("canInstall");
                _status.IsInstalled = await _airCodePWA.InvokeAsync<bool>("isInstalled");
                _status.UpdateAvailable = await _airCodePWA.InvokeAsync<bool>("hasUpdate");
                _status.IsOnline = await JSRuntime.InvokeAsync<bool>("navigator.onLine");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PWA Component] Status update error: {ex.Message}");
                // Fallback to basic online status
                _status.IsOnline = await JSRuntime.InvokeAsync<bool>("navigator.onLine");
            }
        }
    }

    // User actions
    private async Task InstallApp()
    {
        if (_airCodePWA != null)
        {
            try
            {
                var success = await _airCodePWA.InvokeAsync<bool>("install");
                SetStatusWithTimeout(success ? "Installing..." : "Install cancelled");
            }
            catch (Exception ex)
            {
                SetStatusWithTimeout($"Install failed: {ex.Message}");
                Console.WriteLine($"[PWA Component] Install error: {ex}");
            }
        }
        else
        {
            SetStatusWithTimeout("PWA not available");
        }
    }

    private async Task ApplyUpdate()
    {
        if (_airCodePWA != null)
        {
            try
            {
                await _airCodePWA.InvokeVoidAsync("applyUpdate");
                SetStatusWithTimeout("Updating...");
            }
            catch (Exception ex)
            {
                SetStatusWithTimeout($"Update failed: {ex.Message}");
                Console.WriteLine($"[PWA Component] Update error: {ex}");
            }
        }
        else
        {
            SetStatusWithTimeout("PWA not available");
        }
    }

    private async Task CheckForUpdates()
    {
        await UpdateStatus();
        SetStatusWithTimeout(_status.UpdateAvailable ? "Update available!" : "No updates");
    }

    // Event handlers
    [JSInvokable] 
    public async Task OnInstallPromptReady()
    {
        _status.IsInstallable = true;
        SetStatusWithTimeout("App can be installed!");
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable] 
    public async Task OnUpdateAvailable()
    {
        _status.UpdateAvailable = true;
        SetStatusWithTimeout("Update available!");
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable] 
    public async Task OnAppInstalled()
    {
        _status.IsInstalled = true;
        _status.IsInstallable = false;
        SetStatusWithTimeout("App installed successfully!");
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable] 
    public async Task OnConnectivityChanged(bool isOnline)
    {
        var wasOffline = !_status.IsOnline;
        _status.IsOnline = isOnline;
        
        if (wasOffline != !isOnline)
        {
            await OnOfflineStatusChanged.InvokeAsync(!isOnline);
            SetStatusWithTimeout(isOnline ? "Back online" : "Offline mode");
        }
        await InvokeAsync(StateHasChanged);
    }

    private void SetStatusWithTimeout(string message)
    {
        _statusMessage = message;
        StateHasChanged();
    
        _statusTimer?.Dispose();
        _statusTimer = new Timer(ClearStatusCallback, null, 3500, Timeout.Infinite);
    }

    private void ClearStatusCallback(object? state)
    {
        _statusMessage = string.Empty;
        InvokeAsync(StateHasChanged);
        _statusTimer?.Dispose();
    }

    private void ClearStatus()
    {
        _statusMessage = string.Empty;
        _statusTimer?.Dispose();
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _statusTimer?.Dispose();
        if (_airCodePWA != null) await _airCodePWA.DisposeAsync();
        _dotNetRef?.Dispose();
    }

    public class PWAStatus
    {
        public bool IsInstallable { get; set; }
        public bool IsInstalled { get; set; }
        public bool HasServiceWorker { get; set; }
        public bool UpdateAvailable { get; set; }
        public bool IsOnline { get; set; } = true;
        public bool IsChromiumBased { get; set; }
    }

    public class OfflineRoute
    {
        public string Url { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}