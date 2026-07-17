using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WashUp.Models;

namespace WashUp.Services;

/// <summary>
/// Menambahkan klaim kustom (FullName) ke cookie Identity agar
/// layout dan halaman bisa menampilkan nama tanpa query database.
/// </summary>
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrEmpty(user.FullName))
            identity.AddClaim(new Claim("FullName", user.FullName));
        return identity;
    }
}
