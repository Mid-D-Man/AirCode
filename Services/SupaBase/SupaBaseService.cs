using Supabase;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AirCode.Models;
using AirCode.Domain.Enums;
/*
namespace AirCode.Services.SupaBase
{
    public class SupaBaseService : ISupaBaseService
    {
        private readonly Supabase.Client _supabaseClient;
        
        public SupaBaseService(string supabaseUrl, string supabaseKey)
        {
            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };
            
            _supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey, options);
        }
        
        public async Task<bool> SignUpAsync(SignUpModel model)
        {
            try
            {
                // Prepare user metadata
                var userMetadata = new Dictionary<string, object>
                {
                    { "first_name", model.FirstName },
                    { "last_name", model.LastName },
                    { "gender", model.Gender },
                    { "date_of_birth", model.DateOfBirth.ToString("yyyy-MM-dd") },
                    { "role", (int)model.Role },
                    { "matric_number", model.MatriculationNumber ?? "" }
                };
                
                // Add middle name if provided
                if (!string.IsNullOrEmpty(model.MiddleName))
                {
                    userMetadata.Add("middle_name", model.MiddleName);
                }
                
                // Add admin ID as Base64 if user is admin
                if (model.IsAdmin && !string.IsNullOrEmpty(model.AdminId))
                {
                    userMetadata.Add("admin_id", Convert.ToBase64String(Encoding.UTF8.GetBytes(model.AdminId)));
                }
                
                // Sign up user with Supabase
                var response = await _supabaseClient.Auth.SignUp(
                    model.Email,
                    model.Password,
                    new Supabase.Gotrue.SignUpOptions { 
                        Data = userMetadata,
                        RedirectTo = "https://yourapp.com/auth/callback"
                    });
                
                // Check if signup was successful
                return response.User != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Signup error: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> SignInAsync(string userNameOrEmail, string password, string? adminId = null)
        {
            try
            {
                // Determine if input is email or username
                bool isEmail = userNameOrEmail.Contains("@");
                
                Supabase.Gotrue.Session session;
                
                if (isEmail)
                {
                    // Sign in with email
                    session = await _supabaseClient.Auth.SignIn(userNameOrEmail, password);
                }
                else
                {
                    // Sign in with username by querying users table first
                    var response = await _supabaseClient.From("users")
                        .Select("email")
                        .eq("username", userNameOrEmail)
                        .Single<UserEmailResponse>();
                    
                    if (response == null || string.IsNullOrEmpty(response.Email))
                        return false;
                        
                    session = await _supabaseClient.Auth.SignIn(response.Email, password);
                }
                
                // Verify admin status if adminId provided
                if (!string.IsNullOrEmpty(adminId) && session?.User != null)
                {
                    var userData = session.User.UserMetadata;
                    
                    if (userData != null && 
                        userData.ContainsKey("admin_id") && 
                        userData["admin_id"] is string storedAdminId)
                    {
                        // Decode stored admin ID from Base64
                        var decodedAdminId = Encoding.UTF8.GetString(Convert.FromBase64String(storedAdminId));
                        
                        // Verify admin ID matches
                        if (decodedAdminId != adminId)
                        {
                            await _supabaseClient.Auth.SignOut();
                            return false;
                        }
                    }
                    else if (userData != null && userData.ContainsKey("role"))
                    {
                        // If user is trying to use admin ID but doesn't have admin role
                        int role = Convert.ToInt32(userData["role"]);
                        if (role == (int)UserRole.Student)
                        {
                            await _supabaseClient.Auth.SignOut();
                            return false;
                        }
                    }
                }
                
                return session?.User != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sign in error: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> ResetPasswordAsync(string email)
        {
            try
            {
                // Send password reset email
                await _supabaseClient.Auth.ResetPasswordForEmail(
                    email,
                    new Supabase.Gotrue.ResetPasswordForEmailOptions { 
                        RedirectTo = "https://yourapp.com/reset-password" 
                    });
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reset password error: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> SignOutAsync()
        {
            try
            {
                await _supabaseClient.Auth.SignOut();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sign out error: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                // Get current user or session
                var user = await _supabaseClient.Auth.GetUser();
                return user != null;
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<string?> GetCurrentUserIdAsync()
        {
            try {
                var user = await _supabaseClient.Auth.GetUser();
                return user?.Id;
            }
            catch {
                return null;
            }
        }
        
        // For free MFA, we can implement TOTP with a library like OtpNet
        public Task<bool> SetupMFAAsync(string email)
        {
            // We'll implement this with a third-party TOTP library later
            return Task.FromResult(false);
        }
        
        public Task<bool> VerifyMFAAsync(string email, string token)
        {
            // We'll implement this with a third-party TOTP library later
            return Task.FromResult(false);
        }
    }
    
    // Helper class for user email response
    public class UserEmailResponse
    {
        public string Email { get; set; }
    }
    */
