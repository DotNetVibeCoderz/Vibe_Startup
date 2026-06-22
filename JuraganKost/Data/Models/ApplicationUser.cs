using Microsoft.AspNetCore.Identity;

namespace JuraganKost.Data.Models;

/// <summary>
/// Extended Identity user with additional profile fields
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string NamaLengkap { get; set; } = string.Empty;
    public string? Alamat { get; set; }
    public string? FotoUrl { get; set; }
    public UserRoleExt RoleExt { get; set; } = UserRoleExt.Penghuni;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Relationships
    public ICollection<Kost> KostMilik { get; set; } = new List<Kost>();
}

public enum UserRoleExt
{
    SuperAdmin,
    Pemilik,
    Admin,
    Penghuni,
    Staff
}
