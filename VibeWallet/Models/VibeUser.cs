using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace VibeWallet.Models;

/// <summary>
/// Extended user model for VibeWallet
/// </summary>
public class VibeUser : IdentityUser<Guid>
{
    // Extended profile fields
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? AlternativePhone { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(200)]
    public string? PlaceOfBirth { get; set; }

    public Gender? Gender { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Province { get; set; }

    [MaxLength(10)]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    public string? Country { get; set; } = "Indonesia";

    [MaxLength(500)]
    public string? ProfilePictureUrl { get; set; }

    // KYC Fields
    public KycStatus KycStatus { get; set; } = KycStatus.NotSubmitted;

    [MaxLength(50)]
    public string? IdentityNumber { get; set; } // NIK

    public IdentityType? IdentityType { get; set; }

    public DateTime? KycSubmittedAt { get; set; }
    public DateTime? KycVerifiedAt { get; set; }

    // Security Fields
    [MaxLength(200)]
    public string? TransactionPin { get; set; } // Hashed PIN (increased length for BCrypt)

    public bool BiometricEnabled { get; set; } = false;

    // Note: TwoFactorEnabled is inherited from IdentityUser<Guid>, so we don't redefine it

    public int FailedPinAttempts { get; set; } = 0;
    public DateTime? PinLockedUntil { get; set; }

    // Preferences
    [MaxLength(10)]
    public string ThemePreference { get; set; } = "light"; // light/dark

    [MaxLength(20)]
    public string LanguagePreference { get; set; } = "id";

    // Navigation properties
    public virtual Wallet? Wallet { get; set; }
    public virtual ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
    public virtual ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
    public virtual ICollection<KycDocument> KycDocuments { get; set; } = new List<KycDocument>();
    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public virtual ICollection<OtpCode> OtpCodes { get; set; } = new List<OtpCode>();
}
