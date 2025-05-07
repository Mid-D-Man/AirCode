namespace AirCode.Services.Auth.Offline;

/// <summary>
/// Service interface for offline authentication functionality
/// </summary>
public interface IAuthOfflineService
{
    /// <summary>
    /// Authenticates a user using offline credentials
    /// </summary>
    Task<bool> AuthenticateOfflineAsync();
    
    /// <summary>
    /// Creates offline credentials from the current authenticated user
    /// </summary>
    Task<bool> CreateOfflineCredentialsFromCurrentUserAsync();
}