namespace AirCode.Services.Auth;

public interface IOfflineCredentialService
{
    Task<bool> StoreCredentials(string username, string password, bool isAdmin, string adminId);
    Task<OfflineCredentials> GetStoredCredentials();
    Task<bool> HasStoredCredentials();
    Task ClearCredentials();
    Task<bool> ValidateOfflineLogin(string username, string password, bool isAdmin, string adminId);
}