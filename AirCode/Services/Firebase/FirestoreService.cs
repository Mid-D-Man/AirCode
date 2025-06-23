// Services/Firebase/FirestoreService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using AirCode.Utilities.HelperScripts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AirCode.Services.Firebase
{
    /// <summary>
    /// Service for Firestore database operations using JavaScript interop
    /// </summary>
    public class FirestoreService : IFirestoreService
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;
        public bool IsConfigured { get; private set; }

        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public FirestoreService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (IsConfigured) return;

                _isInitialized = await _jsRuntime.InvokeAsync<bool>("firestoreModule.initializeFirestore");
                IsConfigured = _isInitialized;
                
                if (_isInitialized)
                {
                    MID_HelperFunctions.DebugMessage("Firestore initialized successfully", DebugClass.Info);
                }
                else
                {
                    MID_HelperFunctions.DebugMessage("Failed to initialize Firestore", DebugClass.Error);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error initializing Firestore: {ex.Message}", DebugClass.Exception);
            }
        }

        // ==================== DOCUMENT OPERATIONS ====================

        public async Task<T> GetDocumentAsync<T>(string collection, string id) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocument", collection, id);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return null;
                
                return JsonConvert.DeserializeObject<T>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting document: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<string> AddDocumentAsync<T>(string collection, T data, string customId = null) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<string>("firestoreModule.addDocument", collection, json, customId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding document: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<bool> UpdateDocumentAsync<T>(string collection, string id, T data) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateDocument", collection, id, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating document: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> DeleteDocumentAsync(string collection, string id)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.deleteDocument", collection, id);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error deleting document: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

     
       // ==================== FIELD OPERATIONS (FIXED) ====================

public async Task<bool> AddOrUpdateFieldAsync<T>(string collection, string docId, string fieldName, T value)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        var json = JsonConvert.SerializeObject(value, _jsonSettings);
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addOrUpdateField", collection, docId, fieldName, json);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error updating field: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

public async Task<bool> UpdateFieldsAsync<T>(string collection, string docId, T fields) where T : class
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        var json = JsonConvert.SerializeObject(fields, _jsonSettings);
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateFields", collection, docId, json);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error updating fields: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

// Remove the duplicate - keep only this one
public async Task<bool> RemoveFieldAsync(string collection, string docId, string fieldName)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeField", collection, docId, fieldName);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error removing field: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

// Remove nested field with proper path handling
public async Task<bool> RemoveNestedFieldAsync(string collection, string docId, string fieldPath)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeNestedField", collection, docId, fieldPath);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error removing nested field: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

public async Task<bool> RemoveFieldsAsync(string collection, string docId, List<string> fieldNames)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        var json = JsonConvert.SerializeObject(fieldNames, _jsonSettings);
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeFields", collection, docId, json);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error removing fields: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

public async Task<T> GetFieldAsync<T>(string collection, string docId, string fieldName)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getField", collection, docId, fieldName);
        
        if (string.IsNullOrEmpty(jsonResult))
            return default(T);
        
        return JsonConvert.DeserializeObject<T>(jsonResult, _jsonSettings);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error getting field: {ex.Message}", DebugClass.Exception);
        return default(T);
    }
}

        // ==================== SUBCOLLECTION OPERATIONS ====================

        public async Task<string> AddToSubcollectionAsync<T>(string collection, string docId, string subcollection, T data, string customId = null) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<string>("firestoreModule.addToSubcollection", collection, docId, subcollection, json, customId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding to subcollection: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<List<T>> GetSubcollectionAsync<T>(string collection, string docId, string subcollection) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getSubcollection", collection, docId, subcollection);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting subcollection: {ex.Message}", DebugClass.Exception);
                return new List<T>();
            }
        }

        public async Task<T> GetSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getSubcollectionDocument", collection, docId, subcollection, subdocId);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return null;
                
                return JsonConvert.DeserializeObject<T>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting subcollection document: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<bool> UpdateSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId, T data) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateSubcollectionDocument", collection, docId, subcollection, subdocId, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating subcollection document: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> DeleteSubcollectionDocumentAsync(string collection, string docId, string subcollection, string subdocId)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.deleteSubcollectionDocument", collection, docId, subcollection, subdocId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error deleting subcollection document: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<List<T>> QuerySubcollectionAsync<T>(string collection, string docId, string subcollection, string field, object value) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonValue = JsonConvert.SerializeObject(value, _jsonSettings);
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.querySubcollection", collection, docId, subcollection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error querying subcollection: {ex.Message}", DebugClass.Exception);
                return new List<T>();
            }
        }

        // ==================== ARRAY FIELD OPERATIONS ====================

        public async Task<bool> AddToArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addToArrayField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding to array field: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> RemoveFromArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeFromArrayField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing from array field: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        // ==================== COLLECTION OPERATIONS ====================

        public async Task<List<T>> GetCollectionAsync<T>(string collection) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getCollection", collection);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting collection: {ex.Message}", DebugClass.Exception);
                return new List<T>();
            }
        }

        public async Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonValue = JsonConvert.SerializeObject(value, _jsonSettings);
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.queryCollection", collection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error querying collection: {ex.Message}", DebugClass.Exception);
                return new List<T>();
            }
        }

        public async Task<bool> AddBatchAsync<T>(string collection, List<T> items) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(items, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addBatch", collection, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding batch: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        // ==================== LEGACY/SPECIALIZED OPERATIONS ====================

        public async Task<bool> FindAndDeleteCourseAsync(string courseCode)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.findAndDeleteCourse", courseCode);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error finding and deleting course {courseCode}: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> DeleteFromSpecificCollectionAsync(string collection, string courseCode)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.deleteFromSpecificCollection", collection, courseCode);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error deleting course {courseCode} from {collection}: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> SyncCollectionWithLocalAsync<T>(string collection, List<T> localData) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(localData, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.syncCollectionWithLocal", collection, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error syncing collection: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        // ==================== CONNECTION MANAGEMENT ====================

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.isConnected");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SetConnectionStateAsync(bool enableConnection)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.setConnectionState", enableConnection);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error setting connection state: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> GetManualConnectionStateAsync()
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.getManualConnectionState");
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting manual connection state: {ex.Message}", DebugClass.Exception);
                return true;
            }
        }

        public async Task ProcessPendingOperationsAsync()
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                await _jsRuntime.InvokeVoidAsync("firestoreModule.processPendingOperations");
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error processing pending operations: {ex.Message}", DebugClass.Exception);
            }
        }
    }
}