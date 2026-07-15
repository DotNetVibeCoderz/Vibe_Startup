using Microsoft.AspNetCore.Identity;

namespace AppBender.Core.Data;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string? JobTitle { get; set; }
    public string? AvatarUrl { get; set; }
    /// <summary>Tenant the user belongs to.</summary>
    public string OrganizationId { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Developer = "Developer";
    public const string EndUser = "EndUser";
    public static readonly string[] All = [Admin, Developer, EndUser];
    /// <summary>Roles allowed to design apps/forms/workflows.</summary>
    public const string Designers = Admin + "," + Developer;
}
