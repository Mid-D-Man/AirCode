using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AirCode.Models;

public class LoginModel
{
    [Required(ErrorMessage = "Username is required")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+@[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*\.[a-zA-Z0-9]{2,}$|^[a-zA-Z0-9_]{3,20}$", 
        ErrorMessage = "Enter a valid email or username (3-20 alphanumeric characters)")]
    public string Username { get; set; } = string.Empty;
        
    [Required(ErrorMessage = "Password is required")]
    [RegularExpression(@"^.{6,}$", ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;
        
    public bool IsAdmin { get; set; }
    
    [AdminIdValidation(ErrorMessage = "Invalid Admin ID format")]
    public string AdminId { get; set; } = string.Empty;
}

public class ForgotPasswordModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = string.Empty;
}

// Custom validator for Admin ID
public class AdminIdValidationAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var model = (LoginModel)validationContext.ObjectInstance;
        
        // Only validate if IsAdmin is true
        if (!model.IsAdmin)
            return ValidationResult.Success;
            
        var adminId = value as string;
        
        // Allow empty for now since we handle this in form submission
        if (string.IsNullOrWhiteSpace(adminId))
            return ValidationResult.Success;
            
        // For superior admin ID
        if (adminId.StartsWith("AIR-CODE-SUPERIOR-ADMIN-"))
        {
            var regex = new Regex(@"^AIR-CODE-SUPERIOR-ADMIN-([A-Z0-9]{5}-){15}[A-Z0-9]{5}\.[A-Z2-7]+=*$");
            if (regex.IsMatch(adminId))
                return ValidationResult.Success;
        }
        
        // For regular admin ID
        if (adminId.StartsWith("REG-"))
        {
            var regex = new Regex(@"^REG-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}\.[a-zA-Z0-9\+/]+=*$");
            if (regex.IsMatch(adminId))
                return ValidationResult.Success;
        }
        
        // Special case for testing
        if (adminId == "admin")
            return ValidationResult.Success;
            
        return new ValidationResult("Admin ID must be in the correct format");
    }
}