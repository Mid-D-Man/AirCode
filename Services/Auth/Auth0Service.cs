using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using AirCode.Models;
using Microsoft.JSInterop;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace AirCode.Services.Auth
{
    public class Auth0Service : IAuth0Service
    {
        private readonly Auth0Settings _settings;
        private readonly NavigationManager _navigationManager;
        private readonly IJSRuntime _jsRuntime;

        public Auth0Service(Auth0Settings settings, NavigationManager navigationManager, IJSRuntime jsRuntime)
        {
            _settings = settings;
            _navigationManager = navigationManager;
            _jsRuntime = jsRuntime;
        }

        public async Task LoginAsync()
        {
            // Generate PKCE code verifier and challenge
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = await GenerateCodeChallengeAsync(codeVerifier);
        
            // Store code verifier in session storage (not local storage)
            await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "auth_code_verifier", codeVerifier);

        
            var redirectUri = _navigationManager.BaseUri + _settings.RedirectUri;
            var url = GetLoginUrl(codeChallenge);
            await _jsRuntime.InvokeVoidAsync("window.location.replace", url);
        }

        public string GetLoginUrl(string codeChallenge)
        {
            var redirectUri = _navigationManager.BaseUri + _settings.RedirectUri;
            return $"https://{_settings.Domain}/authorize" +
                   $"?client_id={_settings.ClientId}" +
                   $"&response_type=code" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                   $"&scope=openid profile email" +
                   $"&audience={Uri.EscapeDataString(_settings.Audience)}" +
                   $"&code_challenge={codeChallenge}" +
                   $"&code_challenge_method=S256";
        }
    
        private string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    [JSInvokable]
        public async Task<string> GenerateCodeChallengeAsync(string codeVerifier)
        {
            // Use JS interop to call browser's crypto API
            return await _jsRuntime.InvokeAsync<string>("generateCodeChallenge", codeVerifier);
        }
    }
}