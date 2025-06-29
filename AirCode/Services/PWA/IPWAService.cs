// PWA Service for Blazor WASM Integration
// Add to Services/PWA/

using Microsoft.JSInterop;
using System.Text.Json;

namespace AirCode.Services.PWA
{
    public interface IPWAService
    {
        Task<bool> IsPWAEnabledAsync();
        Task EnablePWAAsync();
        Task DisablePWAAsync();
        Task<bool> CheckForUpdatesAsync();
        Task<bool> ApplyUpdateAsync();
        Task<bool> ClearCacheAsync();
        Task<PWAInfo> GetPWAInfoAsync();
        Task SubscribeToUpdatesAsync(Func<Task> onUpdateAvailable, Func<Task> onUpdateInstalled);
        event EventHandler<PWAUpdateEventArgs>? UpdateAvailable;
        event EventHandler? UpdateInstalled;
    }

    // Data models
    public class PWAInfo
    {
        public bool Enabled { get; set; }
        public string? Version { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PWAUpdateEventArgs : EventArgs
    {
        public bool HasUpdate { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Configuration class for appsettings.json
    public class PWAConfiguration
    {
        public const string SectionName = "PWA";
        
        public bool Enabled { get; set; } = true;
        public bool AutoUpdate { get; set; } = false;
        public int UpdateCheckIntervalMinutes { get; set; } = 60;
        public bool ShowUpdateNotifications { get; set; } = true;
        public string Version { get; set; } = "1.0.0";
    }
}
