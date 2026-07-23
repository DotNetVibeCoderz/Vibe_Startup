using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// Savings account (digital savings with interest)
/// </summary>
public class SavingsAccount : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string AccountNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRate { get; set; } = 3.5m; // Annual interest rate in %

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalInterestEarned { get; set; } = 0;

    public virtual ICollection<SavingsTransaction> Transactions { get; set; } = new List<SavingsTransaction>();
}

/// <summary>
/// Savings transaction
/// </summary>
public class SavingsTransaction : BaseEntity
{
    public Guid SavingsAccountId { get; set; }

    [ForeignKey(nameof(SavingsAccountId))]
    public virtual SavingsAccount? SavingsAccount { get; set; }

    [MaxLength(50)]
    public string TransactionRef { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Type { get; set; } = string.Empty; // "deposit", "withdraw", "interest"

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Investment portfolio
/// </summary>
public class Investment : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    public InvestmentType Type { get; set; }

    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ProductCode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal InvestedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentValue { get; set; }

    [Column(TypeName = "decimal(10,5)")]
    public decimal? CurrentUnitPrice { get; set; } // NAV for mutual funds, per gram for gold

    [Column(TypeName = "decimal(18,5)")]
    public decimal? Units { get; set; }

    [Column(TypeName = "decimal(8,4)")]
    public decimal ReturnPercentage { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReturnAmount { get; set; } = 0;

    public DateTime InvestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? MaturityDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? RiskLevel { get; set; } // "low", "medium", "high"
}

/// <summary>
/// Insurance product
/// </summary>
public class InsuranceProduct : BaseEntity
{
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    public InsuranceType Type { get; set; }

    [MaxLength(100)]
    public string ProviderName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PremiumAmount { get; set; } // Monthly/yearly premium

    [MaxLength(20)]
    public string PremiumPeriod { get; set; } = "monthly"; // monthly, yearly, one-time

    [Column(TypeName = "decimal(18,2)")]
    public decimal CoverageAmount { get; set; }

    public int DurationMonths { get; set; }

    [MaxLength(200)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// User insurance enrollment
/// </summary>
public class UserInsurance : BaseEntity
{
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    public Guid InsuranceProductId { get; set; }

    [ForeignKey(nameof(InsuranceProductId))]
    public virtual InsuranceProduct? InsuranceProduct { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string PolicyNumber { get; set; } = string.Empty;

    public DateTime? NextPremiumDate { get; set; }
}
