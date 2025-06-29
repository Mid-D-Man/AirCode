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
    /// Stores offline credentials with additional role-specific data using provided encryption key and IV
    /// </summary>
    Task<bool> StoreCredentialsWithAdditionalDataAsync(
        string userId, 
        string role, 
        string key, 
        string iv, 
        int expirationDays = 14,
        string lecturerId = null,
        string matricNumber = null);
        
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
    /// Gets the lecturer ID from stored credentials (LecturerAdmin role only)
    /// </summary>
    Task<string> GetLecturerIdAsync();

    /// <summary>
    /// Gets the matric number from stored credentials (Student/CourseRepAdmin roles only)
    /// </summary>
    Task<string> GetMatricNumberAsync();

    /// <summary>
    /// Gets the device GUID from stored credentials
    /// </summary>
    Task<string> GetDeviceGuidAsync();
        
    /// <summary>
    /// Clears stored credentials
    /// </summary>
    Task<bool> ClearCredentialsAsync();

    /// <summary>
    /// Checks if offline credentials exist and are still valid (not expired)
    /// If expired, automatically clears them
    /// </summary>
    Task<bool> AreCredentialsValidAsync();

    /// <summary>
    /// Checks and cleans up expired credentials
    /// Returns true if credentials were expired and cleaned up, false if they were valid or didn't exist
    /// </summary>
    Task<bool> CheckAndCleanExpiredCredentialsAsync();

    /// <summary>
    /// Gets comprehensive credential status for debugging
    /// </summary>
    Task<object> GetCredentialStatusAsync();
}

/// <summary>
/// Enhanced user credentials model with role-specific data
/// </summary>
public class OfflineUserCredentials
{
    public string UserId { get; set; }
    public string Role { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string DeviceGuid { get; set; }
    
    // Role-specific properties
    public string LecturerId { get; set; } // For LecturerAdmin role
    public string MatricNumber { get; set; } // For Student/CourseRepAdmin roles
}