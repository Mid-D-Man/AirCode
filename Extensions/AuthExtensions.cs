using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace AirCode.Extensions
{
    public static class AuthExtensions
    {
        /// <summary>
        /// Attempts to get the access token and returns it as a string
        /// </summary>
        public static async Task<string> GetTokenAsync(this IAccessTokenProvider provider)
        {
            try
            {
                var tokenResult = await provider.RequestAccessToken();
                
                if (tokenResult.TryGetToken(out var accessToken))
                {
                    return accessToken.Value;
                }
                
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}