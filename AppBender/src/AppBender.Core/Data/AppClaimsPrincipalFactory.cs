using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppBender.Core.Data;

/// <summary>Adds tenant + display-name claims so middleware and circuits can resolve them cheaply.</summary>
public class AppClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("org", user.OrganizationId));
        identity.AddClaim(new Claim("displayname",
            string.IsNullOrEmpty(user.DisplayName) ? user.UserName ?? "" : user.DisplayName));
        return identity;
    }
}
