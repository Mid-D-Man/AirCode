using AirCode.Domain.Enums;
using AirCode.Models;
using System.Threading.Tasks;

namespace AirCode.Services.Auth
{
    public interface IAuth0Service
    {
        Task<bool> SignUpAsync(SignUpModel model);
        Task<bool> LoginAsync(LoginModel model);
        Task<User> GetCurrentUserAsync();
        Task<bool> LogoutAsync();
        Task<bool> IsAuthenticatedAsync();
        Task InitializeAsync();
    }
}