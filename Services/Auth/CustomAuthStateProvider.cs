namespace AirCode.Services.Auth;

using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

/// <summary>
/// Custom authentication state provider that supports both online and offline authentication
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

    /// <summary>
    /// Gets the current authentication state
    /// </summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    /// <summary>
    /// Updates the authentication state with a new user
    /// </summary>
    public Task UpdateAuthenticationStateAsync(ClaimsPrincipal user)
    {
        _currentUser = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears the current authentication state (logout)
    /// </summary>
    public Task ClearAuthenticationStateAsync()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return Task.CompletedTask;
    }
}