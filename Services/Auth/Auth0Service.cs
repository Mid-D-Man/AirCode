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
        private bool _isInitialized = false;

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
            if (_isInitialized)
            {
                return;
            }
            
            try
            {
                Console.WriteLine("Starting Auth0 initialization...");
                
                // First check if auth0Client.js is available
                var isModuleLoaded = await _jsRuntime.InvokeAsync<bool>(
                    "eval", 
                    "typeof window.auth0Client !== 'undefined'"
                );
                
                if (!isModuleLoaded)
                {
                    Console.WriteLine("Auth0Client module not found in window object");
                    return;
                }
                
                await _jsRuntime.InvokeVoidAsync("auth0Client.initialize");
                _isInitialized = true;
                
                Console.WriteLine("Auth0 initialization completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Auth0: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }

        public async Task<bool> SignUpAsync(SignUpModel model)
        {
            try
            {
                if (!_isInitialized)
                {
                    Console.WriteLine("Auth0 not initialized, attempting to initialize before signup");
                    await InitializeAsync();
                }
                
                Console.WriteLine($"Starting signup for user: {model.Username}");
                
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
                Console.WriteLine("User metadata prepared");
                
                // Check if auth0Client is available
                var isClientAvailable = await _jsRuntime.InvokeAsync<bool>(
                    "eval", 
                    "typeof window.auth0Client !== 'undefined' && typeof window.auth0Client.signUp === 'function'"
                );
                
                if (!isClientAvailable)
                {
                    Console.WriteLine("Auth0Client.signUp function is not available");
                    return false;
                }
                
                // Call Auth0 signup via JS interop
                Console.WriteLine("Calling Auth0 signup...");
                var success = await _jsRuntime.InvokeAsync<bool>(
                    "auth0Client.signUp", 
                    model.Email, 
                    model.Password,
                    model.Username,
                    metadataJson);
                
                if (success)
                {
                    Console.WriteLine("Auth0 signup successful, storing credentials for offline use");
                    
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
                    
                    Console.WriteLine("User data stored successfully");
                }
                else
                {
                    Console.WriteLine("Auth0 signup failed");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during signup: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<bool> LoginAsync(LoginModel model)
        {
            try
            {
                if (!_isInitialized)
                {
                    Console.WriteLine("Auth0 not initialized, attempting to initialize before login");
                    await InitializeAsync();
                }
                
                Console.WriteLine($"Starting login for user: {model.Username}");
                
                // Check network connectivity first
                var isOnline = await _jsRuntime.InvokeAsync<bool>("eval", "navigator.onLine");
                
                if (isOnline)
                {
                    Console.WriteLine("Online mode detected, using Auth0 for authentication");
                    
                    // Check if auth0Client is available
                    var isClientAvailable = await _jsRuntime.InvokeAsync<bool>(
                        "eval", 
                        "typeof window.auth0Client !== 'undefined' && typeof window.auth0Client.login === 'function'"
                    );
                    
                    if (!isClientAvailable)
                    {
                        Console.WriteLine("Auth0Client.login function is not available");
                        return false;
                    }
                    
                    // Online login through Auth0
                    var result = await _jsRuntime.InvokeAsync<bool>(
                        "auth0Client.login", 
                        model.Username, 
                        model.Password,
                        model.IsAdmin,
                        model.AdminId);
                    
                    if (result)
                    {
                        Console.WriteLine("Auth0 login successful, retrieving user profile");
                        
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
                                
                            Console.WriteLine("User profile stored for offline use");
                        }
                        else
                        {
                            Console.WriteLine("Could not retrieve user profile from Auth0");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Auth0 login failed");
                    }
                    
                    return result;
                }
                else
                {
                    Console.WriteLine("Offline mode detected, using stored credentials");
                    
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
                Console.WriteLine($"Error during login: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
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
                Console.WriteLine($"Error getting current user: {ex.GetType().Name} - {ex.Message}");
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
                Console.WriteLine($"Error during logout: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    return false;
                }
                
                // Check if auth0Client is available
                var isClientAvailable = await _jsRuntime.InvokeAsync<bool>(
                    "eval", 
                    "typeof window.auth0Client !== 'undefined' && typeof window.auth0Client.isAuthenticated === 'function'"
                );
                
                if (!isClientAvailable)
                {
                    return false;
                }
                
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