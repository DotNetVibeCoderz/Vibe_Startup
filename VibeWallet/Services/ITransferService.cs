using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for P2P transfers and split bills
/// </summary>
public interface ITransferService
{
    // P2P Transfer
    Task<P2PTransfer> TransferAsync(Guid senderUserId, string receiverWalletNumber, decimal amount, string? notes = null);
    Task<List<P2PTransfer>> GetTransferHistoryAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<P2PTransfer?> GetTransferByRefAsync(string transferRef);

    // Split Bill
    Task<SplitBill> CreateSplitBillAsync(Guid creatorUserId, string title, decimal totalAmount, List<Guid> participantUserIds, string? description = null);
    Task<SplitBill?> GetSplitBillAsync(Guid splitBillId);
    Task<bool> PaySplitBillAsync(Guid userId, Guid splitBillId);
    Task<List<SplitBill>> GetUserSplitBillsAsync(Guid userId);
    Task<bool> SettleSplitBillAsync(Guid splitBillId);
}
