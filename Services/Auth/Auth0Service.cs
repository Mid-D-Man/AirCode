
using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using AirCode.Models;
using AirCode.Domain.Enums;
using AirCode.Services.Storage;

namespace AirCode.Services.Auth
{
    public class Auth0Service : IAuth0Service
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly IOfflineCredentialService _offlineCredentialService;
        private readonly IUserStorageService _userStorageService;
        
        private const string AUTH_TOKEN_KEY = "auth0_token";
        private const string USER_KEY = "auth0_user";

        public Auth0Service(
            IJSRuntime jsRuntime,
            IBlazorAppLocalStorageService localStorage,
            IOfflineCredentialService offlineCredentialService,
            IUserStorageService userStorageService)
        {
            _jsRuntime = jsRuntime;
            _localStorage = localStorage;
            _offlineCredentialService = offlineCredentialService;
            _userStorageService = userStorageService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("auth0Client.initialize");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Auth0: {ex.Message}");
            }
        }

        public async Task<bool> SignUpAsync(SignUpModel model)
        {
            try
            {
                // Create user metadata with extra properties
                var userMetadata = new Dictionary<string, object>
                {
                    { "firstName", model.FirstName },
                    { "middleName", model.MiddleName ?? "" },
                    { "lastName", model.LastName },
                    { "dateOfBirth", model.DateOfBirth.ToString("yyyy-MM-dd") },
                    { "gender", model.Gender },
                    { "role", model.Role.ToString() },
                    { "matriculationNumber", model.MatriculationNumber ?? "" },
                    { "isAdmin", model.IsAdmin },
                    { "adminId", model.AdminId ?? "" }
                };

                var metadataJson = JsonSerializer.Serialize(userMetadata);
                
                // Call Auth0 signup via JS interop
                var success = await _jsRuntime.InvokeAsync<bool>(
                    "auth0Client.signUp", 
                    model.Email, 
                    model.Password,
                    model.Username,
                    metadataJson);
                
                if (success)
                {
                    // Store offline credentials for offline operation
                    await _offlineCredentialService.StoreCredentials(
                        model.Username, 
                        model.Password, 
                        model.IsAdmin, 
                        model.AdminId ?? "");
                    
                    // Create local User object
                    var user = new User
                    {
                        FirstName = model.FirstName,
                        MiddleName = model.MiddleName ?? "",
                        LastName = model.LastName,
                        DateOfBirth = model.DateOfBirth,
                        Gender = model.Gender,
                        Email = model.Email,
                        Username = model.Username,
                        MatriculationNumber = model.MatriculationNumber,
                        AdminId = model.AdminId,
                        Department = "", // Will be updated later in profile
                        Role = model.Role
                    };
                    
                    // Cache current user
                    await _localStorage.SetItemAsync(USER_KEY, JsonSerializer.Serialize(user));
                    
                    // Also store in user repository for offline use
                    await _userStorageService.AddUser(user);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during signup: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LoginAsync(LoginModel model)
        {
            try
            {
                // Check network connectivity first
                var isOnline = await _jsRuntime.InvokeAsync<bool>("connectivityService.isOnline");
                
                if (isOnline)
                {
                    // Online login through Auth0
                    var result = await _jsRuntime.InvokeAsync<bool>(
                        "auth0Client.login", 
                        model.Username, 
                        model.Password,
                        model.IsAdmin,
                        model.AdminId);
                    
                    if (result)
                    {
                        // Get user profile from Auth0
                        var userJson = await _jsRuntime.InvokeAsync<string>("auth0Client.getUserProfile");
                        if (!string.IsNullOrEmpty(userJson))
                        {
                            await _localStorage.SetItemAsync(USER_KEY, userJson);
                            
                            // Also store offline credentials
                            await _offlineCredentialService.StoreCredentials(
                                model.Username, 
                                model.Password, 
                                model.IsAdmin, 
                                model.AdminId);
                        }
                    }
                    
                    return result;
                }
                else
                {
                    // Offline login using stored credentials
                    return await _offlineCredentialService.ValidateOfflineLogin(
                        model.Username, 
                        model.Password, 
                        model.IsAdmin, 
                        model.AdminId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
                return false;
            }
        }

        public async Task<User> GetCurrentUserAsync()
        {
            try
            {
                // First try to get from session storage
                var userJson = await _localStorage.GetItemAsync<string>(USER_KEY);
                
                if (!string.IsNullOrEmpty(userJson))
                {
                    return JsonSerializer.Deserialize<User>(userJson);
                }
                
                // If not in storage, try to get from Auth0
                var isAuthenticated = await IsAuthenticatedAsync();
                if (isAuthenticated)
                {
                    userJson = await _jsRuntime.InvokeAsync<string>("auth0Client.getUserProfile");
                    if (!string.IsNullOrEmpty(userJson))
                    {
                        await _localStorage.SetItemAsync(USER_KEY, userJson);
                        return JsonSerializer.Deserialize<User>(userJson);
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current user: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                // Clear local storage
                await _localStorage.RemoveItemAsync(AUTH_TOKEN_KEY);
                await _localStorage.RemoveItemAsync(USER_KEY);
                
                // Call Auth0 logout
                await _jsRuntime.InvokeVoidAsync("auth0Client.logout");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during logout: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                // Check Auth0 authentication status
                return await _jsRuntime.InvokeAsync<bool>("auth0Client.isAuthenticated");
            }
            catch
            {
                // If JS interop fails, check local storage for token
                var token = await _localStorage.GetItemAsync<string>(AUTH_TOKEN_KEY);
                return !string.IsNullOrEmpty(token);
            }
        }
    }
}