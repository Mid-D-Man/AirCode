using AirCode.Models;

namespace AirCode.Services;


    public interface IUserStorageService
    {
        Task<bool> SaveUsers(List<User> users);
        Task<List<User>> GetUsers();
        Task<User?> GetUser(string matricNumber);
        Task<bool> AddUser(User user);
        Task<bool> UpdateUser(User user);
        
    }
