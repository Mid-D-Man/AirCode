using System.Security.Claims;
using System.Text.Json;
using AirCode.Services.Storage;
using Microsoft.AspNetCore.Components.Authorization;

namespace AirCode.Services.Auth
{
    public class RoleAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IBlazorAppLocalStorageService _localStorage;
        
        public RoleAuthStateProvider(IBlazorAppLocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }
        
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var accessToken = await _localStorage.GetItemAsync<string>("auth_access_token");
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }
                
                var userId = await _localStorage.GetItemAsync<string>("auth_user_id");
                var userName = await _localStorage.GetItemAsync<string>("user_name");
                var userEmail = await _localStorage.GetItemAsync<string>("user_email");
                
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Name, userName ?? string.Empty),
                    new Claim(ClaimTypes.Email, userEmail ?? string.Empty)
                }, "auth0");
                
                // Add roles to the identity if available
                var rolesJson = await _localStorage.GetItemAsync<string>("user_roles");
                if (!string.IsNullOrEmpty(rolesJson))
                {
                    var roles = JsonSerializer.Deserialize<List<string>>(rolesJson);
                    if (roles != null)
                    {
                        foreach (var role in roles)
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }
                    }
                }
                
                var user = new ClaimsPrincipal(identity);
                return new AuthenticationState(user);
            }
            catch
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }
        
        public void NotifyAuthenticationStateChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
        
        public async Task<bool> IsInRoleAsync(string role)
        {
            var authState = await GetAuthenticationStateAsync();
            var user = authState.User;
            return user.IsInRole(role);
        }
        
        public async Task<bool> IsAuthenticatedAsync()
        {
            var authState = await GetAuthenticationStateAsync();
            return authState.User.Identity?.IsAuthenticated ?? false;
        }
    }
}