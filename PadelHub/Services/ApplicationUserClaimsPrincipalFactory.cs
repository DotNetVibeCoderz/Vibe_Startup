using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PadelHub.Models;

namespace PadelHub.Services;

/// <summary>
/// Menambahkan FullName dan MemberNumber ke cookie login sehingga UI bisa
/// menyapa pengguna dengan namanya tanpa query database di setiap render.
/// Tanpa ini, klaim "FullName" tidak pernah ada dan antarmuka jatuh ke email.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (!string.IsNullOrWhiteSpace(user.FullName))
            identity.AddClaim(new Claim("FullName", user.FullName));

        if (!string.IsNullOrWhiteSpace(user.MemberNumber))
            identity.AddClaim(new Claim("MemberNumber", user.MemberNumber));

        if (!string.IsNullOrWhiteSpace(user.Email))
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));

        return identity;
    }
}
