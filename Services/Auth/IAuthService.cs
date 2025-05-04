using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AirCode.Services.Auth
{
    public interface IAuthService
    {
        Task<string> GetJwtTokenAsync();
        Task LogAuthenticationMessageAsync(string message);
        void LogAuthenticationMessage(string message);
        Task<AccessTokenResult> GetAccessTokenResultAsync();
    }

    public class AuthService : IAuthService
    {
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IJSRuntime _jsRuntime;
        private readonly IAccessTokenProvider _tokenProvider;

        public AuthService(
            AuthenticationStateProvider authStateProvider, 
            IJSRuntime jsRuntime,
            IAccessTokenProvider tokenProvider)
        {
            _authStateProvider = authStateProvider;
            _jsRuntime = jsRuntime;
            _tokenProvider = tokenProvider;
        }

        public async Task<AccessTokenResult> GetAccessTokenResultAsync()
        {
            try
            {
                var tokenResult = await _tokenProvider.RequestAccessToken();
                await LogAuthenticationMessageAsync($"Access token request status: {tokenResult.Status}");
                return tokenResult;
            }
            catch (Exception ex)
            {
                await LogAuthenticationMessageAsync($"Error getting access token: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetJwtTokenAsync()
        {
            try
            {
                // Try to get token from AccessTokenProvider first
                var tokenResult = await _tokenProvider.RequestAccessToken();
                
                if (tokenResult.TryGetToken(out var accessToken))
                {
                    await LogAuthenticationMessageAsync($"Token successfully retrieved from provider");
                    return accessToken.Value;
                }
                
                // Fall back to looking in claims
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;

                if (user.Identity.IsAuthenticated)
                {
                    // Try various possible claim types for the token
                    foreach (var possibleClaimType in new[] { "access_token", "id_token", "token", "jwt" })
                    {
                        var claim = user.FindFirst(possibleClaimType);
                        if (claim != null)
                        {
                            await LogAuthenticationMessageAsync($"Token found in claim: {possibleClaimType}");
                            return claim.Value;
                        }
                    }
                    
                    // If we get here, log all claim types to help debugging
                    var claimTypes = string.Join(", ", user.Claims.Select(c => c.Type).Distinct());
                    await LogAuthenticationMessageAsync($"No token found. Available claim types: {claimTypes}");
                }
                
                return "Token not found";
            }
            catch (Exception ex)
            {
                await LogAuthenticationMessageAsync($"Error retrieving token: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public void LogAuthenticationMessage(string message)
        {
            Console.WriteLine($"[Authentication] -> {message}");
        }
//i think we can use our unity debug method with the [class name] - - - [function name] -> log. method here to
        public async Task LogAuthenticationMessageAsync(string message)
        {
            LogAuthenticationMessage(message);
            await _jsRuntime.InvokeVoidAsync("console.log", $"[Authentication] -> {message}");
        }
    }
}