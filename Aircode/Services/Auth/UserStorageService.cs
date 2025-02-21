using Aircode.Models;
using Aircode.Utilities;
using Blazored.LocalStorage;
namespace Aircode.Services;

public class UserStorageService : IUserStorageService
{
    private const string USERS_KEY = "users";
    private readonly ILocalStorageService _localStorage; // This is from Blazored.LocalStorage

    public UserStorageService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<bool> SaveUsers(List<User> users)
    {
        try
        {
            var json = JsonHelper.Serialize(users);
            await _localStorage.SetItemAsStringAsync(USERS_KEY, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<User>> GetUsers()
    {
        try
        {
            var json = await _localStorage.GetItemAsStringAsync(USERS_KEY);
            return JsonHelper.Deserialize<List<User>>(json) ?? new List<User>();
        }
        catch
        {
            return new List<User>();
        }
    }

    public async Task<User?> GetUser(string matricNumber)
    {
        var users = await GetUsers();
        return users.FirstOrDefault(u => u.MatriculationNumber == matricNumber);
    }

    public async Task<bool> AddUser(User user)
    {
        var users = await GetUsers();
        users.Add(user);
        return await SaveUsers(users);
    }

    public async Task<bool> UpdateUser(User user)
    {
        var users = await GetUsers();
        var index = users.FindIndex(u => u.MatriculationNumber == user.MatriculationNumber);
        if (index == -1) return false;
        
        users[index] = user;
        return await SaveUsers(users);
    }
}