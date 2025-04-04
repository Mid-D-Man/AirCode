using AirCode.Models;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AirCode.Models;
/*
namespace AirCode.Services
{
    /// <summary>
    /// Interface for Supabase authentication operations
    /// </summary>
    public interface ISupabaseService
    {
        /// <summary>
        /// Gets the current authenticated user
        /// </summary>
        /// <returns>The current user, or null if not authenticated</returns>
        Task<User> GetCurrentUserAsync();

        /// <summary>
        /// Gets the current session if one exists
        /// </summary>
        /// <returns>The current session, or null if no active session</returns>
        Task<Session> GetSessionAsync();

        /// <summary>
        /// Registers a new user
        /// </summary>
        /// <param name="model">The signup information</param>
        /// <returns>The session object if email confirmation is disabled, null otherwise</returns>
        Task<Session> SignUpAsync(SignUpModel model);

        /// <summary>
        /// Signs in a user with email and password
        /// </summary>
        /// <param name="email">User email</param>
        /// <param name="password">User password</param>
        /// <returns>Session data for the authenticated user</returns>
        Task<Session> SignInAsync(string email, string password);

        /// <summary>
        /// Signs in a user using email/phone with OTP (One Time Password)
        /// </summary>
        /// <param name="emailOrPhone">User email or phone number</param>
        /// <param name="options">Optional sign-in options like redirect URL</param>
        /// <returns>True if magic link/OTP was sent successfully</returns>
        Task<bool> SendMagicLinkAsync(string emailOrPhone, SignInOptions options = null);

        /// <summary>
        /// Verifies OTP for authentication
        /// </summary>
        /// <param name="emailOrPhone">Email or phone number</param>
        /// <param name="token">Verification token</param>
        /// <param name="type">Verification type (sms, email, etc.)</param>
        /// <returns>Session data for the authenticated user</returns>
        Task<Session> VerifyOtpAsync(string emailOrPhone, string token, OtpType type);

        /// <summary>
        /// Signs out the current user
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SignOutAsync();

        /// <summary>
        /// Updates user information
        /// </summary>
        /// <param name="attributes">User attributes to update</param>
        /// <returns>The updated user</returns>
        Task<User> UpdateUserAsync(UserAttributes attributes);

        /// <summary>
        /// Resets user password using token received via email
        /// </summary>
        /// <param name="email">User email</param>
        /// <returns>True if password reset email was sent successfully</returns>
        Task<bool> ResetPasswordAsync(string email);
        
        /// <summary>
        /// Registers for authentication state changes
        /// </summary>
        /// <param name="callback">The callback to invoke when auth state changes</param>
        void AddAuthStateChangedListener(Action<AuthState> callback);
        
        /// <summary>
        /// Removes an authentication state change listener
        /// </summary>
        /// <param name="callback">The callback to remove</param>
        void RemoveAuthStateChangedListener(Action<AuthState> callback);
    }

    /// <summary>
    /// Represents a user session
    /// </summary>
    public class Session
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public User User { get; set; }
    }

    /// <summary>
    /// Represents a user
    /// </summary>
    public class User
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public bool EmailConfirmed { get; set; }
        public Dictionary<string, object> UserMetadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Options for sign in
    /// </summary>
    public class SignInOptions
    {
        public string RedirectTo { get; set; }
    }

    /// <summary>
    /// User attributes for updating user information
    /// </summary>
    public class UserAttributes
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Authentication state enum
    /// </summary>
    public enum AuthState
    {
        SignedIn,
        SignedOut,
        UserUpdated,
        PasswordRecovery,
        TokenRefreshed
    }

    /// <summary>
    /// OTP verification types
    /// </summary>
    public enum OtpType
    {
        Sms,
        PhoneChange,
        Signup,
        MagicLink,
        Recovery,
        Invite,
        EmailChange
    }

    /// <summary>
    /// OAuth providers
    /// </summary>
    public enum Provider
    {
        Google,
        Github, 
        Azure,
        Facebook,
        Twitter,
        Discord,
        Apple
    }8
    */
