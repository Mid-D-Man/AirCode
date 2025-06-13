namespace AirCode.Services.Auth;
/// <summary>
/// Service for managing offline user credentials in AirCode app
/// </summary>
public interface IOfflineCredentialsService
{
    /// <summary>
    /// Stores offline credentials for user authentication
    /// </summary>
    Task<bool> StoreCredentialsAsync(string userId, string role, string key, string iv, int expirationDays = 14);
        
    /// <summary>
    /// Stores offline credentials using test key and IV (for development only)
    /// </summary>
    Task<bool> StoreCredentialsWithTestKeyAsync(string userId, string role, int expirationDays = 14);

    /// <summary>
    /// Creates offline credentials from the current authenticated user using test keys
    /// </summary>
    Task<bool> CreateOfflineCredentialsFromCurrentUserAsync();

    /// <summary>
    /// Generates and stores offline credentials with proper encryption
    /// </summary>
    Task<(bool Success, string Key, string IV)> GenerateAndStoreCredentialsAsync(string userId, string role, int expirationDays = 14);
        
    /// <summary>
    /// Retrieves user credentials if available and valid
    /// </summary>
    Task<OfflineUserCredentials> GetCredentialsAsync();
        
    /// <summary>
    /// Gets the user's role from stored credentials
    /// </summary>
    Task<string> GetUserRoleAsync();
        
    /// <summary>
    /// Gets the user's ID from stored credentials
    /// </summary>
    Task<string> GetUserIdAsync();
        
    /// <summary>
    /// Clears stored credentials
    /// </summary>
    Task<bool> ClearCredentialsAsync();
}

/// <summary>
/// User credentials model
/// </summary>
public class OfflineUserCredentials
{
    public string UserId { get; set; }
    public string Role { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}