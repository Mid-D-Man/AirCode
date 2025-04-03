using AirCode.Models;

namespace AirCode.Services.SupaBase;

public interface ISupaBaseService
{
    Task<bool> SignUpAsync(SignUpModel model);
    Task<bool> SignInAsync(string userNameOrEmail, string password, string? adminId = null);
    Task<bool> ResetPasswordAsync(string email);
    Task<bool> SignOutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetCurrentUserIdAsync();
    
    // Keeping these methods in the interface for future implementation with a third-party provider
    Task<bool> SetupMFAAsync(string email);
    Task<bool> VerifyMFAAsync(string email, string token);
}