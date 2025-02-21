namespace Aircode.Models;

public class SignUpModel
{
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
    public string Role { get; set; } = "";
    public string Department { get; set; } = "";
    public string Level { get; set; } = "";
    public string? MatriculationNumber { get; set; } = "";
    public string Courses { get; set; } = "";

    public bool IsAdmin { get; set; } = false;

    public string? AdminId { get; set; } = "";
}