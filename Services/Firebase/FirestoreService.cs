// Services/Firebase/FirestoreService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using AirCode.Utilities.HelperScripts;
using Newtonsoft.Json;

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

        public FirestoreService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _isInitialized = await _jsRuntime.InvokeAsync<bool>("firestoreModule.initializeFirestore");
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

        public async Task<T> GetDocumentAsync<T>(string collection, string id) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocument", collection, id);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return null;
                    
                return JsonConvert.DeserializeObject<T>(jsonResult);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting document: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<string> AddDocumentAsync<T>(string collection, T data) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data);
                return await _jsRuntime.InvokeAsync<string>("firestoreModule.addDocument", collection, json);
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
                var json = JsonConvert.SerializeObject(data);
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

        public async Task<List<T>> GetCollectionAsync<T>(string collection) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getCollection", collection);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                    
                return JsonConvert.DeserializeObject<List<T>>(jsonResult);
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
                var jsonValue = JsonConvert.SerializeObject(value);
                var jsonResult = await _jsRuntime.InvokeAsync<string>(
                    "firestoreModule.queryCollection", collection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                    
                return JsonConvert.DeserializeObject<List<T>>(jsonResult);
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
                var json = JsonConvert.SerializeObject(items);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addBatch", collection, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding batch: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> SyncCollectionWithLocalAsync<T>(string collection, List<T> localData) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(localData);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.syncCollectionWithLocal", collection, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error syncing collection: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

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
    }
}