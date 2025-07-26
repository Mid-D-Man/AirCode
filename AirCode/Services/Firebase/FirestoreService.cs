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
        
        // Document size tracking (approximate)
        private const int MAX_DOCUMENT_SIZE_BYTES = 900000; // 900KB to leave buffer
        private const int ESTIMATED_STUDENT_ENTRY_SIZE = 2000; // ~2KB per student entry
        private const int ESTIMATED_ATTENDANCE_ENTRY_SIZE = 500; // ~500B per attendance


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

        // ==================== FIELD OPERATIONS ====================

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
        
        #region Distributed Storage
        
        // ==================== DISTRIBUTED DOCUMENT OPERATIONS ====================

    /// <summary>
    /// Add student course data with automatic document distribution
    /// </summary>
    public async Task<string> AddStudentCourseAsync(string matricNumber, object courseData, string level)
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"studentcourses_{level.ToLower()}level";
            string targetDocumentId = await FindAvailableDocumentAsync("StudentCourses", baseDocumentId, ESTIMATED_STUDENT_ENTRY_SIZE);
            
            var json = JsonConvert.SerializeObject(courseData, _jsonSettings);
            return await _jsRuntime.InvokeAsync<string>("firestoreModule.addToDistributedDocument", 
                "StudentCourses", targetDocumentId, matricNumber, json);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error adding student course: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }

    /// <summary>
    /// Add attendance event with automatic document distribution
    /// </summary>
    public async Task<string> AddAttendanceEventAsync(string courseCode, object attendanceData)
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"attendanceevent{courseCode.ToUpper()}";
            string targetDocumentId = await FindAvailableDocumentAsync("AttendanceEvents", baseDocumentId, ESTIMATED_ATTENDANCE_ENTRY_SIZE);
            
            var eventId = Guid.NewGuid().ToString();
            var json = JsonConvert.SerializeObject(attendanceData, _jsonSettings);
            return await _jsRuntime.InvokeAsync<string>("firestoreModule.addToDistributedDocument", 
                "AttendanceEvents", targetDocumentId, eventId, json);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error adding attendance event: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }

    /// <summary>
    /// Get all student courses for a level (across all distributed documents)
    /// </summary>
    public async Task<Dictionary<string, T>> GetAllStudentCoursesAsync<T>(string level) where T : class
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"studentcourses_{level.ToLower()}level";
            var allDocuments = await GetDistributedDocumentsAsync("StudentCourses", baseDocumentId);
            
            var combinedData = new Dictionary<string, T>();
            
            foreach (var docData in allDocuments)
            {
                var studentData = JsonConvert.DeserializeObject<Dictionary<string, T>>(docData, _jsonSettings);
                if (studentData != null)
                {
                    foreach (var kvp in studentData)
                    {
                        combinedData[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            return combinedData;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting student courses: {ex.Message}", DebugClass.Exception);
            return new Dictionary<string, T>();
        }
    }

    /// <summary>
    /// Get all attendance events for a course (across all distributed documents)
    /// </summary>
    public async Task<List<T>> GetAllAttendanceEventsAsync<T>(string courseCode) where T : class
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"attendanceevent{courseCode.ToUpper()}";
            var allDocuments = await GetDistributedDocumentsAsync("AttendanceEvents", baseDocumentId);
            
            var combinedEvents = new List<T>();
            
            foreach (var docData in allDocuments)
            {
                var eventData = JsonConvert.DeserializeObject<Dictionary<string, T>>(docData, _jsonSettings);
                if (eventData != null)
                {
                    combinedEvents.AddRange(eventData.Values);
                }
            }
            
            return combinedEvents;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting attendance events: {ex.Message}", DebugClass.Exception);
            return new List<T>();
        }
    }

    /// <summary>
    /// Get specific student course data (searches across distributed documents)
    /// </summary>
    public async Task<T> GetStudentCourseAsync<T>(string matricNumber, string level) where T : class
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"studentcourses_{level.ToLower()}level";
            return await GetFromDistributedDocumentsAsync<T>("StudentCourses", baseDocumentId, matricNumber);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting student course: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }

    /// <summary>
    /// Update student course data (finds correct distributed document automatically)
    /// </summary>
    public async Task<bool> UpdateStudentCourseAsync<T>(string matricNumber, string level, T courseData) where T : class
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"studentcourses_{level.ToLower()}level";
            string targetDocumentId = await FindDocumentContainingKeyAsync("StudentCourses", baseDocumentId, matricNumber);
            
            if (string.IsNullOrEmpty(targetDocumentId))
            {
                // Student not found, add to available document
                return !string.IsNullOrEmpty(await AddStudentCourseAsync(matricNumber, courseData, level));
            }
            
            var json = JsonConvert.SerializeObject(courseData, _jsonSettings);
            return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateFieldInDistributedDocument", 
                "StudentCourses", targetDocumentId, matricNumber, json);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error updating student course: {ex.Message}", DebugClass.Exception);
            return false;
        }
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Find an available document that can accommodate new data
    /// </summary>
    private async Task<string> FindAvailableDocumentAsync(string collection, string baseDocumentId, int estimatedDataSize)
    {
        try
        {
            int documentIndex = 1;
            string currentDocumentId = baseDocumentId;
            
            while (true)
            {
                var documentSizeInfo = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocumentSizeInfo", collection, currentDocumentId);
                
                if (string.IsNullOrEmpty(documentSizeInfo))
                {
                    // Document doesn't exist, use this one
                    return currentDocumentId;
                }
                
                var sizeInfo = JsonConvert.DeserializeObject<DocumentSizeInfo>(documentSizeInfo);
                if (sizeInfo.EstimatedSize + estimatedDataSize < MAX_DOCUMENT_SIZE_BYTES)
                {
                    return currentDocumentId;
                }
                
                // Try next document
                documentIndex++;
                currentDocumentId = $"{baseDocumentId}_{documentIndex}";
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error finding available document: {ex.Message}", DebugClass.Exception);
            return baseDocumentId; // Fallback to base document
        }
    }

    /// <summary>
    /// Get all distributed documents for a base document ID
    /// </summary>
    private async Task<List<string>> GetDistributedDocumentsAsync(string collection, string baseDocumentId)
    {
        try
        {
            var documents = new List<string>();
            int documentIndex = 1;
            string currentDocumentId = baseDocumentId;
            
            // Get base document
            var baseData = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocument", collection, baseDocumentId);
            if (!string.IsNullOrEmpty(baseData))
            {
                documents.Add(baseData);
            }
            
            // Get additional documents
            while (true)
            {
                currentDocumentId = $"{baseDocumentId}_{documentIndex}";
                var documentData = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocument", collection, currentDocumentId);
                
                if (string.IsNullOrEmpty(documentData))
                {
                    break; // No more documents
                }
                
                documents.Add(documentData);
                documentIndex++;
            }
            
            return documents;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting distributed documents: {ex.Message}", DebugClass.Exception);
            return new List<string>();
        }
    }

    /// <summary>
    /// Find which distributed document contains a specific key
    /// </summary>
    private async Task<string> FindDocumentContainingKeyAsync(string collection, string baseDocumentId, string key)
    {
        try
        {
            int documentIndex = 1;
            string currentDocumentId = baseDocumentId;
            
            while (true)
            {
                var hasKey = await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentContainsKey", collection, currentDocumentId, key);
                
                if (hasKey)
                {
                    return currentDocumentId;
                }
                
                // Check if document exists
                var exists = await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentExists", collection, currentDocumentId);
                if (!exists)
                {
                    break; // No more documents to check
                }
                
                documentIndex++;
                currentDocumentId = $"{baseDocumentId}_{documentIndex}";
            }
            
            return null; // Key not found in any document
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error finding document containing key: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }

    /// <summary>
    /// Get data from distributed documents by key
    /// </summary>
    private async Task<T> GetFromDistributedDocumentsAsync<T>(string collection, string baseDocumentId, string key) where T : class
    {
        try
        {
            string targetDocumentId = await FindDocumentContainingKeyAsync(collection, baseDocumentId, key);
            
            if (string.IsNullOrEmpty(targetDocumentId))
            {
                return null;
            }
            
            return await GetFieldAsync<T>(collection, targetDocumentId, key);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting from distributed documents: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }
        
        #endregion
        
        
        // ==================== DATA MODELS ====================
    
        private class DocumentSizeInfo
        {
            public int EstimatedSize { get; set; }
            public int FieldCount { get; set; }
            public bool Exists { get; set; }
        }
    }
}