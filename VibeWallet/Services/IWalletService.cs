using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for wallet operations: balance, top-up, withdraw
/// </summary>
public interface IWalletService
{
    Task<Wallet?> GetWalletByUserIdAsync(Guid userId);
    Task<Wallet?> GetWalletByNumberAsync(string walletNumber);
    Task<decimal> GetBalanceAsync(Guid userId);
    Task<WalletTransaction> TopUpAsync(Guid userId, decimal amount, PaymentMethod method, string? notes = null);
    Task<WalletTransaction> WithdrawAsync(Guid userId, decimal amount, string bankAccountNumber, string? notes = null);
    Task<List<WalletTransaction>> GetTransactionHistoryAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<List<WalletTransaction>> GetTransactionsByDateRangeAsync(Guid userId, DateTime start, DateTime end);
    Task<BalanceHistory> GetDailyBalanceSnapshotAsync(Guid walletId, DateTime date);
    Task<bool> FreezeWalletAsync(Guid userId, string reason);
    Task<bool> UnfreezeWalletAsync(Guid userId);
    Task<string> GenerateWalletNumberAsync();
}
