// // Services/Firebase/IFirestoreService.cs
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AirCode.Services.Firebase
{
    /// <summary>
    /// Interface for Firestore database operations
    /// </summary>
    public interface IFirestoreService
    {
        // ==================== INITIALIZATION & STATE ====================
        bool IsInitialized { get; }
        Task<bool> IsConnectedAsync();
        Task<bool> SetConnectionStateAsync(bool enableConnection);
        Task<bool> GetManualConnectionStateAsync();
        Task ProcessPendingOperationsAsync();

        // ==================== DOCUMENT OPERATIONS ====================
        Task<T> GetDocumentAsync<T>(string collection, string id) where T : class;
        Task<string> AddDocumentAsync<T>(string collection, T data, string customId = null) where T : class;
        Task<bool> UpdateDocumentAsync<T>(string collection, string id, T data) where T : class;
        Task<bool> DeleteDocumentAsync(string collection, string id);

        // ==================== FIELD OPERATIONS ====================
        Task<bool> AddOrUpdateFieldAsync<T>(string collection, string docId, string fieldName, T value);
        Task<bool> UpdateFieldsAsync<T>(string collection, string docId, T fields) where T : class;
        Task<bool> RemoveFieldAsync(string collection, string docId, string fieldName);
        Task<bool> RemoveFieldsAsync(string collection, string docId, List<string> fieldNames);
        Task<T> GetFieldAsync<T>(string collection, string docId, string fieldName);

        // ==================== SUBCOLLECTION OPERATIONS ====================
        Task<string> AddToSubcollectionAsync<T>(string collection, string docId, string subcollection, T data, string customId = null) where T : class;
        Task<List<T>> GetSubcollectionAsync<T>(string collection, string docId, string subcollection) where T : class;
        Task<T> GetSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId) where T : class;
        Task<bool> UpdateSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId, T data) where T : class;
        Task<bool> DeleteSubcollectionDocumentAsync(string collection, string docId, string subcollection, string subdocId);
        Task<List<T>> QuerySubcollectionAsync<T>(string collection, string docId, string subcollection, string field, object value) where T : class;

        // ==================== ARRAY FIELD OPERATIONS ====================
        Task<bool> AddToArrayFieldAsync<T>(string collection, string docId, string fieldName, T value);
        Task<bool> RemoveFromArrayFieldAsync<T>(string collection, string docId, string fieldName, T value);

        // ==================== COLLECTION OPERATIONS ====================
        Task<List<T>> GetCollectionAsync<T>(string collection) where T : class;
        Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class;
        Task<bool> AddBatchAsync<T>(string collection, List<T> items) where T : class;

        // ==================== LEGACY/SPECIALIZED OPERATIONS ====================
        Task<bool> FindAndDeleteCourseAsync(string courseCode);
        Task<bool> DeleteFromSpecificCollectionAsync(string collection, string courseCode);
        Task<bool> SyncCollectionWithLocalAsync<T>(string collection, List<T> localData) where T : class;
        
        // Add these methods to IFirestoreService interface

        // Add these missing interface members to IFirestoreService

        // Add these methods to IFirestoreService interface

        #region Distributed Document Operations

        /// <summary>
        /// Add data to distributed document
        /// </summary>
        Task<string> AddToDistributedDocumentAsync(string collection, string documentId, string key, string jsonData);

        /// <summary>
        /// Update field in distributed document
        /// </summary>
        Task<bool> UpdateFieldInDistributedDocumentAsync(string collection, string documentId, string key, string jsonData);

        /// <summary>
        /// Get document size information
        /// </summary>
        Task<string> GetDocumentSizeInfoAsync(string collection, string documentId);

        /// <summary>
        /// Check if document contains specific key
        /// </summary>
        Task<bool> DocumentContainsKeyAsync(string collection, string documentId, string key);

        /// <summary>
        /// Check if document exists
        /// </summary>
        Task<bool> DocumentExistsAsync(string collection, string documentId);

        #endregion
    }
}
