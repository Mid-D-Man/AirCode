using System.ComponentModel.DataAnnotations;

namespace AirCode.Models;

//its user name or pasword
public class LoginModel
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;
        
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
        
    public bool IsAdmin { get; set; }
        
    public string AdminId { get; set; } = string.Empty;
}