using Aircode.Models;

namespace Aircode.Utilities.DataStructures;

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

// Utilities/DataStructures/AttendanceRecord.cs
public class AttendanceRecord
{
    public string MatriculationNumber { get; set; }
    public string CourseCode { get; set; }
    public DateTime TimeStamp { get; set; }
    public string Location { get; set; }
    public AttendanceType Type { get; set; }
}

public enum AttendanceType
{
    Present,
    Late,
    Absent
}