using System.ComponentModel.DataAnnotations;

namespace EstateHub.Models;

/// <summary>
/// Login request model
/// </summary>
public class LoginModel
{
    [Required(ErrorMessage = "Email wajib diisi")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

/// <summary>
/// Registration request model
/// </summary>
public class RegisterModel
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MinLength(8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare("Password", ErrorMessage = "Password tidak cocok")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Buyer"; // Buyer, Tenant, Agent
}

/// <summary>
/// Reset password model
/// </summary>
public class ResetPasswordModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Change password model
/// </summary>
public class ChangePasswordModel
{
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare("NewPassword")]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

/// <summary>
/// User profile update model
/// </summary>
public class UserProfileModel
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(255)]
    public string? AvatarUrl { get; set; }

    [MaxLength(50)]
    public string? PreferredLocation { get; set; }

    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }

    [MaxLength(20)]
    public string? PreferredType { get; set; }
}
