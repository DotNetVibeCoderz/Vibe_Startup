using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// Kontak wallet tersimpan untuk quick transfer
/// </summary>
public class SavedContact : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(16)]
    public string WalletNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Notes { get; set; }

    public int TransferCount { get; set; } = 0;

    public DateTime? LastTransferAt { get; set; }
}
