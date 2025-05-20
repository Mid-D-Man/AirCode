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
                
                // Preserve original JSON structure and convert to strongly typed object
                var settings = new JsonSerializerSettings 
                { 
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                };
                return JsonConvert.DeserializeObject<T>(jsonResult, settings);
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
                
                // Use consistent JSON serialization settings
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                var json = JsonConvert.SerializeObject(data, settings);
                
                // Pass customId to JavaScript function
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
                
                // Use consistent JSON serialization settings
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                var json = JsonConvert.SerializeObject(data, settings);
                
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
                
                // Preserve original JSON structure and convert to strongly typed list
                var settings = new JsonSerializerSettings 
                { 
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                };
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, settings);
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
                
                // Serialize value with consistent settings
                var settings = new JsonSerializerSettings 
                { 
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore
                };
                var jsonValue = JsonConvert.SerializeObject(value, settings);
                
                var jsonResult = await _jsRuntime.InvokeAsync<string>(
                    "firestoreModule.queryCollection", collection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                // Deserialize with same settings
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, settings);
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
                
                // Use consistent JSON serialization settings
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                var json = JsonConvert.SerializeObject(items, settings);
                
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
                
                // Use consistent JSON serialization settings
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                var json = JsonConvert.SerializeObject(localData, settings);
                
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
        // Add these methods to your FirestoreService.cs class

        /// <summary>
        /// Manually enables or disables Firebase Firestore network connectivity
        /// </summary>
        /// <param name="enableConnection">True to enable connection, false to disable</param>
        /// <returns>True if the operation was successful</returns>
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

        /// <summary>
        /// Gets the current manual connection state of Firestore
        /// </summary>
        /// <returns>True if connection is enabled, false if manually disabled</returns>
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
                // Default to enabled if we can't determine the state
                return true;
            }
        }
    }
}