using Supabase;
using Supabase.Gotrue;
using System;
using System.Text;
using System.Threading.Tasks;
using AirCode.Models;
using AirCode.Domain.Enums;
using Constants = Supabase.Postgrest.Constants;

namespace AirCode.Services.SupaBase
{
    public class SupaBaseService : ISupaBaseService
    {
        private readonly Client _supabaseClient;
        
        public SupaBaseService(string supabaseUrl, string supabaseKey)
        {
            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };
            
            _supabaseClient = new Client(supabaseUrl, supabaseKey, options);
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
                var signUpOptions = new SignUpOptions
                {
                    EmailRedirectTo = "https://yourapp.com/auth/callback",
                    Data = userMetadata
                };
                
                var response = await _supabaseClient.Auth.SignUp(
                    model.Email,
                    model.Password,
                    signUpOptions);
                
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
                
                Session session;
                
                if (isEmail)
                {
                    // Sign in with email
                    session = await _supabaseClient.Auth.SignIn(userNameOrEmail, password);
                }
                else
                {
                    // Sign in with username by querying users table first
                    var response = await _supabaseClient
                        .From("users")
                        .Select("email")
                        .Filter("username", Constants.Operator.Equals, userNameOrEmail)
                        .Single();
                    
                    string email = response.Email;
                    session = await _supabaseClient.Auth.SignIn(email, password);
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
                    new ResetPasswordForEmailOptions
                    {
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
                var session = await _supabaseClient.Auth.GetSession();
                return session?.User != null;
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<string?> GetCurrentUserIdAsync()
        {
            var session = await _supabaseClient.Auth.GetSession();
            return session?.User?.Id;
        }
        
        public async Task<bool> SetupMFAAsync(string email)
        {
            try
            {
                // Check if Supabase instance supports MFA
                // Note: This requires a paid plan in Supabase
                await _supabaseClient.Auth.SetupMFA();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MFA setup error: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> VerifyMFAAsync(string email, string token)
        {
            try
            {
                // Verify MFA token
                // Note: Actual implementation depends on Supabase client specifics
                var response = await _supabaseClient.Auth.VerifyMFAToken(token);
                return response != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MFA verification error: {ex.Message}");
                return false;
            }
        }
    }
}