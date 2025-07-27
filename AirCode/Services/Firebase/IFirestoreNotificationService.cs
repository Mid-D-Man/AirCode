using AirCode.Models.Firebase;

namespace AirCode.Services.Firebase;

public interface IFirestoreNotificationService
{
    Task<bool> InitializeAsync();
    Task<bool> SubscribeToDocumentAsync(string listenerId, string collection, string documentId);
    Task<bool> SubscribeToCollectionAsync(string listenerId, string collection, string? whereField = null, object? whereValue = null);
    Task<bool> SubscribeToAttendanceSessionAsync(string sessionId);
    Task<bool> SubscribeToActiveAttendanceSessionsAsync();
    Task<bool> UnsubscribeAsync(string listenerId);
    Task<int> UnsubscribeAllAsync();
    Task<List<FirestoreListener>> GetActiveListenersAsync();
        
    event EventHandler<DocumentChangedEventArgs>? DocumentChanged;
    event EventHandler<CollectionChangedEventArgs>? CollectionChanged;
    event EventHandler<AttendanceSessionChangedEventArgs>? AttendanceSessionChanged;
    event EventHandler<ActiveSessionsChangedEventArgs>? ActiveSessionsChanged;
    event EventHandler<ListenerErrorEventArgs>? ListenerError;
}