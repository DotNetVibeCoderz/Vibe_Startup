// User and profile models with auth support
using System.ComponentModel.DataAnnotations;
using Joka.Models.Common;

namespace Joka.Models.Users;

public class User : BaseEntity
{
    [Required]
    public string Username { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string PreferredLanguage { get; set; } = "id";
    public string PreferredCurrency { get; set; } = "IDR";
    public string? Theme { get; set; } = "light";
    public int LoyaltyPoints { get; set; }
    public string MembershipTier { get; set; } = "Classic"; // Classic, Silver, Gold, Platinum
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Auth fields
    public string? GoogleId { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public string Role { get; set; } = "User"; // User, Admin, Operator, Merchant

    /// <summary>Set for Merchant accounts - which partner this user acts for.</summary>
    public Guid? MerchantId { get; set; }

    // Blacklist. Separate from LockoutEnd, which is only the automatic
    // lockout after failed logins; this one is an admin decision.
    public bool IsBlocked { get; set; }
    public string? BlockedReason { get; set; }
    public DateTime? BlockedAt { get; set; }
    public string? BlockedBy { get; set; }

    public UserProfile? Profile { get; set; }
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
}

public class UserProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime? PassportExpiry { get; set; }
    public string? IdCardNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
}

public class WishlistItem : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string ItemType { get; set; } = string.Empty; // Flight, Hotel, Train, Activity, Package, Car
    public string ItemId { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public string? ItemImageUrl { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; } = "IDR";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class UserNotification : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Type { get; set; } // Reminder, Promo, Transaction, System
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

// Auth DTOs
public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class RegisterRequest
{
    [Required, MinLength(3)] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required, Compare("Password")] public string ConfirmPassword { get; set; } = string.Empty;
    public string? FullName { get; set; }
}

public class ResetPasswordRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required] public string Token { get; set; } = string.Empty;
    [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
    [Required, Compare("NewPassword")] public string ConfirmPassword { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? PreferredCurrency { get; set; }
    public string? Theme { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Nationality { get; set; }
}
