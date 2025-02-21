namespace Aircode.Models;

public class LoginModel
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsAdmin { get; set; }
    public string? AdminId { get; set; }
}