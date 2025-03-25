using AirCode.Models;

namespace AirCode.Utilities.DataStructures;

// Utilities/DataStructures/UserCollection.cs
public class UserCollection
{
    public List<User> Users { get; set; } = new();
    public DateTime LastUpdated { get; set; }

    public void AddUser(User user)
    {
        Users.Add(user);
        LastUpdated = DateTime.Now;
    }

    public User? FindUser(string matricNumber) =>
        Users.FirstOrDefault(u => u.MatriculationNumber == matricNumber);
}
