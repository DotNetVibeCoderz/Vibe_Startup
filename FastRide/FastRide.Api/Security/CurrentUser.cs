using System.Security.Claims;
using FastRide.Shared.Models;

namespace FastRide.Api.Security;

/// <summary>Reads the caller's identity out of the validated JWT.</summary>
public static class CurrentUser
{
    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static UserRole? Role(this ClaimsPrincipal principal) =>
        Enum.TryParse<UserRole>(principal.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.Role() == UserRole.Admin;

    /// <summary>
    /// True when the caller is acting on their own data, or is an admin.
    /// Every /{userId} route checks this — otherwise any logged-in rider could read
    /// another rider's trips just by changing the id in the URL.
    /// </summary>
    public static bool CanAccess(this ClaimsPrincipal principal, Guid targetUserId) =>
        principal.IsAdmin() || principal.UserId() == targetUserId;
}

/// <summary>Authorization policy names.</summary>
public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string DriverOnly = "DriverOnly";
    public const string RiderOnly = "RiderOnly";
    public const string StaffOrDriver = "StaffOrDriver";
}
