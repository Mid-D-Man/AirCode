namespace AirCode.Services.Auth;

public class Auth0Settings
{
    public string Authority { get; set; } = "https://dev-msf1vlcbx1a8wtg3.eu.auth0.com";
    public string ClientId { get; set; } = "B8U5o6PVyTkowZ9qffByDb9cs6NhZpsx";
    public string Audience { get; set; } = "https://dev-msf1vlcbx1a8wtg3.eu.auth0.com/api/v2/";
    // Keep these for compatibility with your custom Auth0Service
    public string Domain { get; set; } = "dev-msf1vlcbx1a8wtg3.eu.auth0.com";
    public string RedirectUri { get; set; } = "authentication/login-callback";
    public string PostLogoutRedirectUri { get; set; } = "/";
}