using AirCode.Models.PWA;
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
    private CancellationTokenSource? _cancellationTokenSource;

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
            _cancellationTokenSource = new CancellationTokenSource();
            // Add longer delay to ensure scripts are fully loaded
            await Task.Delay(500, _cancellationTokenSource.Token);
            await InitializePWA();
        }
    }

    private async Task InitializePWA()
    {
        try
        {
            if (_cancellationTokenSource?.IsCancellationRequested == true) return;
            
            _dotNetRef = DotNetObjectReference.Create(this);
            
            // Enhanced retry logic with exponential backoff
            var maxRetries = 15;
            var retryCount = 0;
            var baseDelay = 200;
            
            while (retryCount < maxRetries && _cancellationTokenSource?.IsCancellationRequested == false)
            {
                try
                {
                    // Check if the PWA object exists and is properly initialized
                    var pwaAvailable = await JSRuntime.InvokeAsync<bool>("eval", 
                        "typeof window.AirCodePWA === 'object' && window.AirCodePWA !== null && typeof window.AirCodePWA.isInstalled === 'function'",
                        _cancellationTokenSource.Token);
                    
                    if (pwaAvailable)
                    {
                        _airCodePWA = await JSRuntime.InvokeAsync<IJSObjectReference>("eval", 
                            "window.AirCodePWA", _cancellationTokenSource.Token);
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PWA Component] Retry {retryCount + 1}: {ex.Message}");
                }
                
                retryCount++;
                var delay = baseDelay * (int)Math.Pow(2, Math.Min(retryCount - 1, 4)); // Max 3.2 second delay
                await Task.Delay(delay, _cancellationTokenSource.Token);
            }
            
            if (_airCodePWA == null)
            {
                SetStatusWithTimeout("PWA not available - running in fallback mode");
                Console.WriteLine("[PWA Component] PWA manager not available after retries");
                return;
            }
            
            // Setup connectivity monitoring with error handling
            try
            {
                await JSRuntime.InvokeVoidAsync("setupConnectivityMonitoring", 
                    _cancellationTokenSource.Token, _dotNetRef);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PWA Component] Connectivity monitoring setup failed: {ex.Message}");
            }
            
            await UpdateStatus();
            SetStatusWithTimeout("PWA ready");
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // Component was disposed, ignore
        }
        catch (Exception ex)
        {
            SetStatusWithTimeout($"PWA init failed: {ex.Message}");
            Console.WriteLine($"[PWA Component] Initialization error: {ex}");
        }
    }

    private async Task UpdateStatus()
    {
        if (_airCodePWA != null && _cancellationTokenSource?.IsCancellationRequested == false)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    _cancellationTokenSource.Token, timeoutCts.Token);

                _status.IsInstallable = await _airCodePWA.InvokeAsync<bool>("canInstall", combinedCts.Token);
                _status.IsInstalled = await _airCodePWA.InvokeAsync<bool>("isInstalled", combinedCts.Token);
                _status.UpdateAvailable = await _airCodePWA.InvokeAsync<bool>("hasUpdate", combinedCts.Token);
                _status.IsOnline = await JSRuntime.InvokeAsync<bool>("eval", "navigator.onLine", combinedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout or cancellation, use fallback
                Console.WriteLine("[PWA Component] Status update timed out, using fallback");
                try
                {
                    _status.IsOnline = await JSRuntime.InvokeAsync<bool>("eval", "navigator.onLine");
                }
                catch
                {
                    _status.IsOnline = true; // Default fallback
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PWA Component] Status update error: {ex.Message}");
                // Fallback to basic online status
                try
                {
                    _status.IsOnline = await JSRuntime.InvokeAsync<bool>("eval", "navigator.onLine");
                }
                catch
                {
                    _status.IsOnline = true; // Default fallback
                }
            }
        }
    }

    // User actions with better error handling and timeouts
    private async Task InstallApp()
    {
        if (_airCodePWA != null)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var success = await _airCodePWA.InvokeAsync<bool>("install", timeoutCts.Token);
                SetStatusWithTimeout(success ? "Installing..." : "Install cancelled");
            }
            catch (OperationCanceledException)
            {
                SetStatusWithTimeout("Install timed out");
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
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _airCodePWA.InvokeVoidAsync("applyUpdate", timeoutCts.Token);
                SetStatusWithTimeout("Updating...");
            }
            catch (OperationCanceledException)
            {
                SetStatusWithTimeout("Update timed out");
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

    // Event handlers - now protected against disposal
    [JSInvokable] 
    public async Task OnInstallPromptReady()
    {
        if (_cancellationTokenSource?.IsCancellationRequested == true) return;
        
        _status.IsInstallable = true;
        SetStatusWithTimeout("App can be installed!");
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable] 
    public async Task OnUpdateAvailable()
    {
        if (_cancellationTokenSource?.IsCancellationRequested == true) return;
        
        _status.UpdateAvailable = true;
        SetStatusWithTimeout("Update available!");
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable] 
    public async Task OnAppInstalled()
    {
        if (_cancellationTokenSource?.IsCancellationRequested == true) return;
        
        _status.IsInstalled = true;
        _status.IsInstallable = false;
        SetStatusWithTimeout("App installed successfully!");
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable] 
    public async Task OnConnectivityChanged(bool isOnline)
    {
        if (_cancellationTokenSource?.IsCancellationRequested == true) return;
        
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
        if (_cancellationTokenSource?.IsCancellationRequested == true) return;
        
        _statusMessage = message;
        InvokeAsync(StateHasChanged);
    
        _statusTimer?.Dispose();
        _statusTimer = new Timer(ClearStatusCallback, null, 3500, Timeout.Infinite);
    }

    private void ClearStatusCallback(object? state)
    {
        if (_cancellationTokenSource?.IsCancellationRequested == true) return;
        
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
        _cancellationTokenSource?.Cancel();
        _statusTimer?.Dispose();
        
        if (_airCodePWA != null) 
        {
            try
            {
                await _airCodePWA.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PWA Component] Error disposing PWA object: {ex.Message}");
            }
        }
        
        _dotNetRef?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
