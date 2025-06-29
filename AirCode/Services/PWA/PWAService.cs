// PWA Service for Blazor WASM Integration
// Add to Services/PWA/

using Microsoft.JSInterop;
using System.Text.Json;

namespace AirCode.Services.PWA
{
    public class PWAService : IPWAService, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<PWAService> _logger;
        private DotNetObjectReference<PWAService>? _dotNetRef;
        private bool _isInitialized;

        public event EventHandler<PWAUpdateEventArgs>? UpdateAvailable;
        public event EventHandler? UpdateInstalled;

        public PWAService(IJSRuntime jsRuntime, ILogger<PWAService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await _jsRuntime.InvokeVoidAsync("blazorPWA.onUpdateAvailable", _dotNetRef, nameof(OnUpdateAvailable));
                await _jsRuntime.InvokeVoidAsync("blazorPWA.onUpdateInstalled", _dotNetRef, nameof(OnUpdateInstalled));
                _isInitialized = true;
                _logger.LogInformation("PWA service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PWA service");
            }
        }

        public async Task<bool> IsPWAEnabledAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                return await _jsRuntime.InvokeAsync<bool>("blazorPWA.isPWAEnabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check PWA enabled status");
                return false;
            }
        }

        public async Task EnablePWAAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                await _jsRuntime.InvokeVoidAsync("blazorPWA.enablePWA");
                _logger.LogInformation("PWA enabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable PWA");
            }
        }

        public async Task DisablePWAAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                await _jsRuntime.InvokeVoidAsync("blazorPWA.disablePWA");
                _logger.LogInformation("PWA disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable PWA");
            }
        }

        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                await _jsRuntime.InvokeVoidAsync("blazorPWA.checkForUpdates");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
                return false;
            }
        }

        public async Task<bool> ApplyUpdateAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _jsRuntime.InvokeAsync<bool>("blazorPWA.applyUpdate");
                if (result)
                {
                    _logger.LogInformation("PWA update applied successfully");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply PWA update");
                return false;
            }
        }

        public async Task<bool> ClearCacheAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var result = await _jsRuntime.InvokeAsync<bool>("blazorPWA.clearCache");
                if (result)
                {
                    _logger.LogInformation("PWA cache cleared successfully");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear PWA cache");
                return false;
            }
        }

        public async Task<PWAInfo> GetPWAInfoAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var info = await _jsRuntime.InvokeAsync<PWAInfo>("blazorPWA.getInfo");
                return info ?? new PWAInfo { Enabled = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get PWA info");
                return new PWAInfo { Enabled = false };
            }
        }

        public async Task SubscribeToUpdatesAsync(Func<Task> onUpdateAvailable, Func<Task> onUpdateInstalled)
        {
            UpdateAvailable += async (sender, args) => await onUpdateAvailable();
            UpdateInstalled += async (sender, args) => await onUpdateInstalled();
            await EnsureInitializedAsync();
        }

        [JSInvokable]
        public Task OnUpdateAvailable(JsonElement data)
        {
            try
            {
                _logger.LogInformation("PWA update available");
                var args = new PWAUpdateEventArgs
                {
                    HasUpdate = true,
                    Timestamp = DateTime.UtcNow
                };
                UpdateAvailable?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling update available event");
            }
            return Task.CompletedTask;
        }

        [JSInvokable]
        public Task OnUpdateInstalled()
        {
            try
            {
                _logger.LogInformation("PWA update installed");
                UpdateInstalled?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling update installed event");
            }
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _dotNetRef?.Dispose();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing PWA service");
            }
        }
    }

}
