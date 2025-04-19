using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using AirCode.Models;
using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace AirCode.Services.Auth
{
    public class Auth0Service : IAuth0Service
    {
        private readonly Auth0Settings _settings;
        private readonly NavigationManager _navigationManager;
        private readonly IJSRuntime _jsRuntime;
        private readonly IAccessTokenProvider _tokenProvider;

        public Auth0Service(
            Auth0Settings settings, 
            NavigationManager navigationManager, 
            IJSRuntime jsRuntime,
            IAccessTokenProvider tokenProvider)
        {
            _settings = settings;
            _navigationManager = navigationManager;
            _jsRuntime = jsRuntime;
            _tokenProvider = tokenProvider;
        }

        public async Task LoginAsync()
        {
            // Use the built-in OIDC authentication
            _navigationManager.NavigateTo("authentication/login");
        }

        public string GetLoginUrl()
        {
            var redirectUri = _navigationManager.BaseUri + _settings.RedirectUri;
            return $"https://{_settings.Domain}/authorize" +
                   $"?client_id={_settings.ClientId}" +
                   $"&response_type=code" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                   $"&scope=openid profile email" +
                   $"&audience={Uri.EscapeDataString(_settings.Audience)}";
        }
        
        public async Task<string> GetAccessTokenAsync()
        {
            var tokenResult = await _tokenProvider.RequestAccessToken();
            
            if (tokenResult.TryGetToken(out var token))
            {
                return token.Value;
            }
            
            return null;
        }
    }
}