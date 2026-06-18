using System.ComponentModel.DataAnnotations;

namespace LandLord.Models;

/// <summary>
/// Model untuk autentikasi user
/// </summary>
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Nama Lengkap")]
    public string? FullName { get; set; }

    [MaxLength(20)]
    [Display(Name = "Role")]
    public string Role { get; set; } = "User"; // Admin, User, Viewer

    [MaxLength(500)]
    [Display(Name = "Avatar URL")]
    public string? AvatarUrl { get; set; }

    [MaxLength(20)]
    [Display(Name = "No Telepon")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Terakhir Login")]
    public DateTime? LastLoginAt { get; set; }

    [Display(Name = "Dibuat Pada")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? ResetToken { get; set; }

    public DateTime? ResetTokenExpiry { get; set; }
}
