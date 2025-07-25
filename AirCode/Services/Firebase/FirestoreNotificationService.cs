using Microsoft.JSInterop;
using System.Text.Json;

namespace AirCode.Services.Firebase;

    public class FirestoreNotificationService : IFirestoreNotificationService, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<FirestoreNotificationService> _logger;
        private DotNetObjectReference<FirestoreNotificationService>? _objRef;
        private bool _isInitialized = false;

        public FirestoreNotificationService(IJSRuntime jsRuntime, ILogger<FirestoreNotificationService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized) return true;

                _objRef = DotNetObjectReference.Create(this);
                var result = await _jsRuntime.InvokeAsync<bool>("firestoreNotificationModule.initialize", _objRef);
                
                _isInitialized = result;
                _logger.LogInformation("Firestore notification service initialized: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firestore notification service");
                return false;
            }
        }

        public async Task<bool> SubscribeToDocumentAsync(string listenerId, string collection, string documentId)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var result = await _jsRuntime.InvokeAsync<bool>("firestoreNotificationModule.subscribeToDocument", 
                    listenerId, collection, documentId, "OnDocumentChanged");
                
                _logger.LogInformation("Document subscription created: {ListenerId}, {Collection}/{DocumentId}, Result: {Result}",
                    listenerId, collection, documentId, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to document: {ListenerId}", listenerId);
                return false;
            }
        }

        public async Task<bool> SubscribeToCollectionAsync(string listenerId, string collection, string? whereField = null, object? whereValue = null)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var result = await _jsRuntime.InvokeAsync<bool>("firestoreNotificationModule.subscribeToCollection",
                    listenerId, collection, whereField, whereValue != null ? JsonSerializer.Serialize(whereValue) : null, "OnCollectionChanged");
                
                _logger.LogInformation("Collection subscription created: {ListenerId}, {Collection}, Result: {Result}",
                    listenerId, collection, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to collection: {ListenerId}", listenerId);
                return false;
            }
        }

        public async Task<bool> SubscribeToAttendanceSessionAsync(string sessionId)
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var result = await _jsRuntime.InvokeAsync<bool>("firestoreNotificationModule.subscribeToAttendanceSession",
                    sessionId, "OnAttendanceSessionChanged");
                
                _logger.LogInformation("Attendance session subscription created: {SessionId}, Result: {Result}",
                    sessionId, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to attendance session: {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<bool> SubscribeToActiveAttendanceSessionsAsync()
        {
            if (!_isInitialized) await InitializeAsync();

            try
            {
                var result = await _jsRuntime.InvokeAsync<bool>("firestoreNotificationModule.subscribeToActiveAttendanceSessions",
                    "OnActiveSessionsChanged");
                
                _logger.LogInformation("Active attendance sessions subscription created. Result: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to active attendance sessions");
                return false;
            }
        }

        public async Task<bool> UnsubscribeAsync(string listenerId)
        {
            if (!_isInitialized) return false;

            try
            {
                var result = await _jsRuntime.InvokeAsync<bool>("firestoreNotificationModule.unsubscribe", listenerId);
                _logger.LogInformation("Unsubscribed from listener: {ListenerId}, Result: {Result}", listenerId, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from listener: {ListenerId}", listenerId);
                return false;
            }
        }

        public async Task<int> UnsubscribeAllAsync()
        {
            if (!_isInitialized) return 0;

            try
            {
                var count = await _jsRuntime.InvokeAsync<int>("firestoreNotificationModule.unsubscribeAll");
                _logger.LogInformation("Unsubscribed from all listeners. Count: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from all listeners");
                return 0;
            }
        }

        public async Task<List<FirestoreListener>> GetActiveListenersAsync()
        {
            if (!_isInitialized) return new List<FirestoreListener>();

            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("firestoreNotificationModule.getActiveListeners");
                var listeners = JsonSerializer.Deserialize<List<FirestoreListener>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<FirestoreListener>();

                return listeners;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active listeners");
                return new List<FirestoreListener>();
            }
        }

        // JavaScript callback methods
        [JSInvokable]
        public void OnDocumentChanged(string eventDataJson)
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<DocumentChangeData>(eventDataJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (eventData != null)
                {
                    DocumentChanged?.Invoke(this, new DocumentChangedEventArgs(eventData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document change: {Json}", eventDataJson);
            }
        }

        [JSInvokable]
        public void OnCollectionChanged(string eventDataJson)
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<CollectionChangeData>(eventDataJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (eventData != null)
                {
                    CollectionChanged?.Invoke(this, new CollectionChangedEventArgs(eventData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing collection change: {Json}", eventDataJson);
            }
        }

        [JSInvokable]
        public void OnAttendanceSessionChanged(string eventDataJson)
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<DocumentChangeData>(eventDataJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (eventData != null)
                {
                    AttendanceSessionChanged?.Invoke(this, new AttendanceSessionChangedEventArgs(eventData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing attendance session change: {Json}", eventDataJson);
            }
        }

        [JSInvokable]
        public void OnActiveSessionsChanged(string eventDataJson)
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<CollectionChangeData>(eventDataJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (eventData != null)
                {
                    ActiveSessionsChanged?.Invoke(this, new ActiveSessionsChangedEventArgs(eventData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing active sessions change: {Json}", eventDataJson);
            }
        }

        [JSInvokable]
        public void OnListenerError(string errorDataJson)
        {
            try
            {
                var errorData = JsonSerializer.Deserialize<ListenerErrorData>(errorDataJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (errorData != null)
                {
                    _logger.LogError("Firestore listener error: {ListenerId} - {Error}", errorData.ListenerId, errorData.Error);
                    ListenerError?.Invoke(this, new ListenerErrorEventArgs(errorData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing listener error: {Json}", errorDataJson);
            }
        }

        // Events
        public event EventHandler<DocumentChangedEventArgs>? DocumentChanged;
        public event EventHandler<CollectionChangedEventArgs>? CollectionChanged;
        public event EventHandler<AttendanceSessionChangedEventArgs>? AttendanceSessionChanged;
        public event EventHandler<ActiveSessionsChangedEventArgs>? ActiveSessionsChanged;
        public event EventHandler<ListenerErrorEventArgs>? ListenerError;

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_isInitialized)
                {
                    await UnsubscribeAllAsync();
                }
                
                _objRef?.Dispose();
                _objRef = null;
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
        }
    }

    // Data models
    public record FirestoreListener(
        string Id,
        string Type,
        string Collection,
        string? DocumentId,
        string? WhereField,
        object? WhereValue,
        DateTime CreatedAt
    );

    public record DocumentChangeData(
        string ListenerId,
        string Collection,
        string DocumentId,
        bool Exists,
        JsonElement? Data,
        DateTime Timestamp
    );

    public record CollectionChangeData(
        string ListenerId,
        string Collection,
        List<DocumentChange> Changes,
        int Size,
        DateTime Timestamp
    );

    public record DocumentChange(
        string Type,
        JsonElement Document,
        int OldIndex,
        int NewIndex
    );

    public record ListenerErrorData(
        string ListenerId,
        string Error,
        DateTime Timestamp
    );

    // Event argument classes
    public class DocumentChangedEventArgs : EventArgs
    {
        public DocumentChangeData Data { get; }
        public DocumentChangedEventArgs(DocumentChangeData data) => Data = data;
    }

    public class CollectionChangedEventArgs : EventArgs
    {
        public CollectionChangeData Data { get; }
        public CollectionChangedEventArgs(CollectionChangeData data) => Data = data;
    }

    public class AttendanceSessionChangedEventArgs : EventArgs
    {
        public DocumentChangeData Data { get; }
        public AttendanceSessionChangedEventArgs(DocumentChangeData data) => Data = data;
    }

    public class ActiveSessionsChangedEventArgs : EventArgs
    {
        public CollectionChangeData Data { get; }
        public ActiveSessionsChangedEventArgs(CollectionChangeData data) => Data = data;
    }

    public class ListenerErrorEventArgs : EventArgs
    {
        public ListenerErrorData Data { get; }
        public ListenerErrorEventArgs(ListenerErrorData data) => Data = data;
    }
