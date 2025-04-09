namespace AirCode.Services.Auth;

  public class Auth0Settings
    {
        public string Domain { get; set; } = "dev-msf1vlcbx1a8wtg3.eu.auth0.com";
        public string ClientId { get; set; } = "JGLYuTjBEYGcbl0TCqYbIQh1IlKH0sSD";
        public string Audience { get; set; } = "https://dev-msf1vlcbx1a8wtg3.eu.auth0.com/api/v2/";
        public string RedirectUri { get; set; } = "authentication/login-callback";
        public string PostLogoutRedirectUri { get; set; } = "";
    }