using AirCode.Domain.Enums;

namespace AirCode.Models;
//we will deal with department and ,level and courses stuff after signup in the user
//profile area thats were user will update his info we giving the him one of those tutorials to put him 
//on the right path but the below are enough for signup, also matric num becomes lecturer id if role is lecturer
using System.ComponentModel.DataAnnotations;

public class SignUpModel
{
    [Required(ErrorMessage = "First name is required")]
    public string FirstName { get; set; } = "";
    
    public string? MiddleName { get; set; }
    
    [Required(ErrorMessage = "Last name is required")]
    public string LastName { get; set; } = "";
    
    [Required(ErrorMessage = "Date of birth is required")]
    public DateTime DateOfBirth { get; set; }
    
    [Required(ErrorMessage = "Gender is required")]
    public string Gender { get; set; } = "";
    
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = "";
    
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = "";
    
    [Required(ErrorMessage = "Password is required")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[@$!%*#?&])[A-Za-z\d@$!%*#?&]{8,}$", 
        ErrorMessage = "Password must contain at least 8 characters, including letters, numbers and special characters")]
    public string Password { get; set; } = "";
    
    [Required(ErrorMessage = "Confirm password is required")]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = "";
    
    [Required(ErrorMessage = "Role is required")]
    public UserRole Role { get; set; } = UserRole.Student; // Default role
    
    public string? MatriculationNumber { get; set; } = "";
    
    public bool IsAdmin { get; set; } = false;

    public string? AdminId { get; set; } = "";
}