// Services/Firebase/IFirestoreService.cs
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AirCode.Services.Firebase
{
    /// <summary>
    /// Interface for Firestore database operations
    /// </summary>
    public interface IFirestoreService
    {
        // Document operations
        Task<T> GetDocumentAsync<T>(string collection, string id) where T : class;
        Task<string> AddDocumentAsync<T>(string collection, T data, string customId = null) where T : class;
        Task<bool> UpdateDocumentAsync<T>(string collection, string id, T data) where T : class;
        Task<bool> DeleteDocumentAsync(string collection, string id);
        
        // Collection operations
        Task<List<T>> GetCollectionAsync<T>(string collection) where T : class;
        Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class;
        
        // Batch operations
        Task<bool> AddBatchAsync<T>(string collection, List<T> items) where T : class;
        
        // Sync operations
        Task<bool> SyncCollectionWithLocalAsync<T>(string collection, List<T> localData) where T : class;
        
        // Connection status
        Task<bool> IsConnectedAsync();
        bool IsInitialized { get; }
    }
}