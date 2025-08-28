
namespace AirCode.Services.Auth
{
    public interface IAuthService
    {
        /// <summary>
        /// Logs an authentication message for debugging purposes
        /// </summary>
        void LogAuthenticationMessage(string message);
    
        /// <summary>
        /// Logs an authentication message asynchronously
        /// </summary>
        Task LogAuthenticationMessageAsync(string message);
    
        /// <summary>
        /// Retrieves the JWT token for the authenticated user
        /// </summary>
        Task<string> GetJwtTokenAsync();
    
        /// <summary>
        /// Processes a successful login and creates offline credentials
        /// </summary>
        Task ProcessSuccessfulLoginAsync();
    
        /// <summary>
        /// Gets the authenticated user's role from their claims
        /// </summary>
        Task<string> GetUserRoleAsync();
//try get user pic
        Task<string> GetUserPictureAsync();
        /// <summary>
        /// Gets the authenticated user's ID from their claims
        /// </summary>
        Task<string> GetUserIdAsync();
        /// <summary>
        /// Gets the authenticated user's Email from their claims
        /// </summary>
        Task<string> GetUserEmailAsync();
        /// <summary>
        /// Gets the lecturer ID for lecturer admin users
        /// </summary>
        Task<string> GetLecturerIdAsync();

        /// <summary>
        /// Gets the matric number for student and course admin users
        /// </summary>
        Task<string> GetMatricNumberAsync();

        /// <summary>
        /// Gets a unique device identifier
        /// </summary>
        Task<string> GetDeviceIdAsync();
    
        /// <summary>
        /// Checks if the user is authenticated
        /// </summary>
        Task<bool> IsAuthenticatedAsync();
    }
}