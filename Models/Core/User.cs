using AirCode.Domain.Enums;

namespace AirCode.Models;

// Models/UserRole.cs

public class User
{
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string? MatriculationNumber { get; set; } //matric number changes to lecturer id for lecturer tpes
    public string? AdminId { get; set; }
    public string Department { get; set; }
    public UserRole Role { get; set; }
    
    
    
}