using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// Peer-to-peer transfer between VibeWallet users
/// </summary>
public class P2PTransfer : BaseEntity
{
    [Required]
    public Guid SenderUserId { get; set; }

    [ForeignKey(nameof(SenderUserId))]
    public virtual VibeUser? Sender { get; set; }

    [Required]
    public Guid ReceiverUserId { get; set; }

    [ForeignKey(nameof(ReceiverUserId))]
    public virtual VibeUser? Receiver { get; set; }

    [MaxLength(50)]
    public string TransferRef { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Fee { get; set; } = 0;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Split bill group
/// </summary>
public class SplitBill : BaseEntity
{
    [Required]
    public Guid CreatorUserId { get; set; }

    [ForeignKey(nameof(CreatorUserId))]
    public virtual VibeUser? Creator { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public int ParticipantCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPerPerson { get; set; }

    public bool IsSettled { get; set; } = false;

    public DateTime? SettledAt { get; set; }

    public virtual ICollection<SplitBillParticipant> Participants { get; set; } = new List<SplitBillParticipant>();
}

/// <summary>
/// Split bill participant
/// </summary>
public class SplitBillParticipant : BaseEntity
{
    [Required]
    public Guid SplitBillId { get; set; }

    [ForeignKey(nameof(SplitBillId))]
    public virtual SplitBill? SplitBill { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    [MaxLength(50)]
    public string? PaymentRef { get; set; }

    public DateTime? PaidAt { get; set; }
}
