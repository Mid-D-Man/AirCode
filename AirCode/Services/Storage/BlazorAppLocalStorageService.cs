using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Services.Storage
{
    /// <summary>
    /// Service for handling local storage operations in Blazor applications
    ///Notice this uses prefix aircode_ for all data stored unlike the regular
    /// js runtime storage
    /// </summary>
    public class BlazorAppLocalStorageService : IBlazorAppLocalStorageService
    {
        private readonly IJSRuntime _jsRuntime;
        private const string LocalStorageKey = "AirCode_";

        public BlazorAppLocalStorageService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Get an item from local storage
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="key">The key of the item</param>
        /// <returns>The deserialized item or default if not found</returns>
        public async Task<T> GetItemAsync<T>(string key)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                string json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", fullKey);

                if (!MID_HelperFunctions.IsValidString(json))
                    return default;

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting item from local storage: {ex.Message}", DebugClass.Exception);
                return default;
            }
        }

        /// <summary>
        /// Set an item in local storage
        /// </summary>
        /// <typeparam name="T">The type to serialize</typeparam>
        /// <param name="key">The key of the item</param>
        /// <param name="value">The value to store</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SetItemAsync<T>(string key, T value)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                string json = JsonConvert.SerializeObject(value);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", fullKey, json);
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error setting item in local storage: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        /// <summary>
        /// Remove an item from local storage
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> RemoveItemAsync(string key)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", fullKey);
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing item from local storage: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        /// <summary>
        /// Clear all items from local storage with the application prefix
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> ClearAsync()
        {
            try
            {
                // This will clear only items with our prefix
                await _jsRuntime.InvokeVoidAsync("eval", GetClearScriptWithPrefix());
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error clearing local storage: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        /// <summary>
        /// Check if an item exists in local storage
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the item exists, false otherwise</returns>
        public async Task<bool> ContainsKeyAsync(string key)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                string json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", fullKey);
                return MID_HelperFunctions.IsValidString(json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error checking key in local storage: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        /// <summary>
        /// Get the size of local storage in bytes
        /// </summary>
        /// <returns>The size in bytes</returns>
        public async Task<long> GetSizeAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<long>("eval", GetSizeScript());
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting local storage size: {ex.Message}", DebugClass.Exception);
                return 0;
            }
        }

        // Helper script to get local storage size
        private string GetSizeScript()
        {
            return @"
                (function() {
                    var size = 0;
                    for (var i = 0; i < localStorage.length; i++) {
                        var key = localStorage.key(i);
                        if (key.startsWith('" + LocalStorageKey + @"')) {
                            size += localStorage.getItem(key).length * 2; // UTF-16 uses 2 bytes per character
                        }
                    }
                    return size;
                })();
            ";
        }

        // Helper script to clear local storage with prefix
        private string GetClearScriptWithPrefix()
        {
            return @"
                (function() {
                    var keysToRemove = [];
                    for (var i = 0; i < localStorage.length; i++) {
                        var key = localStorage.key(i);
                        if (key.startsWith('" + LocalStorageKey + @"')) {
                            keysToRemove.push(key);
                        }
                    }
                    for (var j = 0; j < keysToRemove.length; j++) {
                        localStorage.removeItem(keysToRemove[j]);
                    }
                })();
            ";
        }
    }
}