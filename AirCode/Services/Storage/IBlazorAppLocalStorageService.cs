using System;
using System.Threading.Tasks;

namespace AirCode.Services.Storage
{
    /// <summary>
    /// Interface for local storage service
    /// </summary>
    public interface IBlazorAppLocalStorageService
    {
        Task<T> GetItemAsync<T>(string key);
        Task<bool> SetItemAsync<T>(string key, T value);
        Task<bool> RemoveItemAsync(string key);
        Task<bool> ClearAsync();
        Task<bool> ContainsKeyAsync(string key);
        Task<long> GetSizeAsync();
    }
}