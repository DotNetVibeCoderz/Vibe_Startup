using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EstateHub.Models;

/// <summary>
/// Chat session for Tante Rita AI chatbot
/// </summary>
public class ChatSession
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = "New Chat";

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }

    public ICollection<ChatHistory> Messages { get; set; } = new List<ChatHistory>();
}

/// <summary>
/// Individual chat message in a session
/// </summary>
public class ChatHistory
{
    [Key]
    public long Id { get; set; }

    public int SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public ChatSession? Session { get; set; }

    /// <summary>user, assistant, system</summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    /// <summary>text, image, document</summary>
    [MaxLength(20)]
    public string ContentType { get; set; } = "text";

    [MaxLength(500)]
    public string? AttachmentUrl { get; set; }

    public int? TokenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// KPR simulation record
/// </summary>
public class KprSimulation
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public decimal PropertyPrice { get; set; }
    public decimal DownPayment { get; set; }
    public decimal LoanAmount { get; set; }
    public double InterestRate { get; set; } // Annual percentage
    public int TenorMonths { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal TotalPayment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Ad/Promotion package for property
/// </summary>
public class Advertisement
{
    [Key]
    public long Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    /// <summary>Basic, Premium, Featured</summary>
    [MaxLength(20)]
    public string PackageType { get; set; } = "Basic";

    public decimal Price { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Active, Expired, Cancelled</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public int Impressions { get; set; }
    public int Clicks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Agent CRM Lead
/// </summary>
public class Lead
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string AgentId { get; set; } = string.Empty;

    [ForeignKey(nameof(AgentId))]
    public ApplicationUser? Agent { get; set; }

    [MaxLength(100)]
    public string? ProspectName { get; set; }

    [MaxLength(20)]
    public string? ProspectPhone { get; set; }

    public int? PropertyId { get; set; }

    /// <summary>New, Contacted, Negotiating, Won, Lost</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "New";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? FollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
