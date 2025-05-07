using AirCode.Utilities.HelperScripts;

namespace AirCode;

using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using System.Security.Claims;
using System.Text.Json;
using System.Diagnostics;

public class CustomAccountFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
{
    public CustomAccountFactory(IAccessTokenProviderAccessor accessor)
        : base(accessor)
    { }

    public async override ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount account,
        RemoteAuthenticationUserOptions options)
    {
        // Step 1: create the user account
        var userAccount = await base.CreateUserAsync(account, options);
        var userIdentity = (ClaimsIdentity)userAccount.Identity;

        if (userIdentity.IsAuthenticated)
        {
            try
            {
                Console.WriteLine("Creating authenticated user in CustomAccountFactory");
                
                // Check for standard role claim type first
                var roleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
                
                // Step 2: Look for the roles in different possible locations
                JsonElement? roles = null;
                
                // Try the normal role claim
                if (account.AdditionalProperties.TryGetValue(roleClaim, out var roleValue))
                {
                    roles = roleValue as JsonElement?;
                    Console.WriteLine($"Found roles in standard claim: {roleClaim}");
                }
                // Try Auth0 custom namespace
                else if (account.AdditionalProperties.TryGetValue("https://air-code/roles", out var auth0RoleValue))
                {
                    roles = auth0RoleValue as JsonElement?;
                    Console.WriteLine("Found roles in Auth0 custom namespace");
                }
                
                // Process roles if found
                if (roles?.ValueKind == JsonValueKind.Array)
                {
                    // Step 3: remove the existing role claim with the serialized array
                    var existingRoleClaim = userIdentity.Claims.FirstOrDefault(c => c.Type == options.RoleClaim);
                    if (existingRoleClaim != null)
                    {
                        userIdentity.RemoveClaim(existingRoleClaim);
                        Console.WriteLine("Removed serialized array role claim");
                    }

                    // Step 4: add each role separately
                    foreach (JsonElement element in roles.Value.EnumerateArray())
                    {
                        var role = element.GetString();
                        userIdentity.AddClaim(new Claim(options.RoleClaim, role));
                        Console.WriteLine($"Added role claim: {role}");
                    }
                }
                else if (roles?.ValueKind == JsonValueKind.String)
                {
                    // Handle case where the role is a single string
                    var role = roles.Value.GetString();
                    
                    // Remove existing claim if it exists
                    var existingRoleClaim = userIdentity.Claims.FirstOrDefault(c => c.Type == options.RoleClaim);
                    if (existingRoleClaim != null)
                    {
                        userIdentity.RemoveClaim(existingRoleClaim);
                    }
                    
                    // Add the role
                    userIdentity.AddClaim(new Claim(options.RoleClaim, role));
                    Console.WriteLine($"Added single role claim: {role}");
                }

                // Debug output of all claims
                Console.WriteLine("All claims after processing:");
                foreach (var claim in userIdentity.Claims)
                {
                    Console.WriteLine($"Claim: {claim.Type} = {claim.Value}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CustomAccountFactory: {ex.Message}");
            }
        }

        return userAccount;
    }
}