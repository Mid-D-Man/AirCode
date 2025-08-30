
using AirCode.Utilities.HelperScripts;

namespace AirCode;

using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using System.Security.Claims;
using System.Text.Json;
/// <summary>
/// Custom account factory to properly process role claims from Auth0
/// </summary>
public class CustomAccountFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
{
    public CustomAccountFactory(IAccessTokenProviderAccessor accessor)
        : base(accessor)
    { }

    public async override ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount account,
        RemoteAuthenticationUserOptions options)
    {
        // Create the base user account
        var userAccount = await base.CreateUserAsync(account, options);
        var userIdentity = (ClaimsIdentity)userAccount.Identity;

        if (userIdentity.IsAuthenticated)
        {
            try
            {
                MID_HelperFunctions.DebugMessage("Processing authenticated user claims in CustomAccountFactory");
                
                // Check for role claims in multiple possible locations
                ProcessRoleClaims(account, userIdentity, options.RoleClaim);
                
                // Debug output all claims
            await MID_HelperFunctions.DebugMessageAsync("All claims after processing:");
                foreach (var claim in userIdentity.Claims)
                {
                   await MID_HelperFunctions.DebugMessageAsync($"Claim: {claim.Type} = {claim.Value}");
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Error in CustomAccountFactory: {ex.Message}",DebugClass.Exception);
            }
        }

        return userAccount;
    }
    
    private void ProcessRoleClaims(RemoteUserAccount account, ClaimsIdentity userIdentity, string roleClaim)
    {
        // Check standard role claim
        if (account.AdditionalProperties.TryGetValue(roleClaim, out var roleValue))
        {
             MID_HelperFunctions.DebugMessage($"Found roles in standard claim: {roleClaim}");
            ProcessRoleValue(roleValue, userIdentity, roleClaim);
        }
        // Check Auth0 custom namespace
        else if (account.AdditionalProperties.TryGetValue("https://air-code/roles", out var auth0RoleValue))
        {
             MID_HelperFunctions.DebugMessage("Found roles in Auth0 custom namespace");
            ProcessRoleValue(auth0RoleValue, userIdentity, roleClaim);
        }
    }
    
    private void ProcessRoleValue(object roleValue, ClaimsIdentity userIdentity, string roleClaim)
    {
        if (roleValue is JsonElement element)
        {
            // Remove existing role claims
            var existingRoleClaims = userIdentity.FindAll(roleClaim).ToList();
            foreach (var existingClaim in existingRoleClaims)
            {
                userIdentity.RemoveClaim(existingClaim);
            }

            // Add roles based on JSON element type
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    var role = item.GetString();
                    userIdentity.AddClaim(new Claim(roleClaim, role));
                     MID_HelperFunctions.DebugMessage($"Added role claim: {role}");
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var role = element.GetString();
                userIdentity.AddClaim(new Claim(roleClaim, role));
                 MID_HelperFunctions.DebugMessage($"Added single role claim: {role}");
            }
        }
    }
}