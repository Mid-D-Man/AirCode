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
            
        // For superior admin, check if it matches the pattern
        if (adminId.StartsWith("AIRCODE-ADMIN-") && IsBase32Format(adminId.Substring(14)))
            return ValidationResult.Success;
            
        // For regular admin check if it's a valid Base64 string
        try
        {
            // Simple check for Base64 format (not perfect but good enough for basic validation)
            if (IsBase64Format(adminId))
                return ValidationResult.Success;
        }
        catch
        {
            // Conversion failed, not a valid Base64 string
        }
        
        // Special case for testing
        if (adminId == "admin")
            return ValidationResult.Success;
            
        return new ValidationResult("Admin ID must be in the correct format");
    }
    
    private bool IsBase64Format(string base64String)
    {
        // Check if the string contains only Base64 characters
        if (string.IsNullOrWhiteSpace(base64String))
            return false;
            
        // Check against regex pattern for Base64
        return Regex.IsMatch(base64String, @"^[a-zA-Z0-9\+/]*={0,3}$");
    }
    
    private bool IsBase32Format(string base32String)
    {
        // Check if the string contains only Base32 characters (A-Z, 2-7)
        if (string.IsNullOrWhiteSpace(base32String))
            return false;
            
        // Check against regex pattern for Base32
        return Regex.IsMatch(base32String, @"^[A-Z2-7]*=*$");
    }
}