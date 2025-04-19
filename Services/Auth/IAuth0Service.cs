using System.Threading.Tasks;

namespace AirCode.Services.Auth
{
    public interface IAuth0Service
    {
        /// <summary>
        /// Initiates the login process by redirecting to Auth0 Universal Login
        /// </summary>
        /// <returns>Task representing the operation</returns>
        Task LoginAsync();
        
        /// <summary>
        /// Gets the Auth0 login URL
        /// </summary>
        /// <returns>The URL to the Auth0 Universal Login</returns>
        string GetLoginUrl(string codeChallenge);
    }
}