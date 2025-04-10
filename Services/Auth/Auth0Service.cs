using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

        public Auth0Service(Auth0Settings settings, NavigationManager navigationManager, IJSRuntime jsRuntime)
        {
            _settings = settings;
            _navigationManager = navigationManager;
            _jsRuntime = jsRuntime;
        }

        public async Task LoginAsync()
        {
            var redirectUri = _navigationManager.BaseUri + _settings.RedirectUri;
            var url = GetLoginUrl();
            await _jsRuntime.InvokeVoidAsync("window.location.replace", url);
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
    }
}