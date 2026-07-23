using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for transaction management and processing
/// </summary>
public interface ITransactionService
{
    Task<string> GenerateTransactionRefAsync();
    Task<WalletTransaction?> GetTransactionByRefAsync(string transactionRef);
    Task<List<WalletTransaction>> GetAllTransactionsAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<WalletTransaction> ProcessTransactionAsync(WalletTransaction transaction);
    Task<bool> CheckDailyLimitsAsync(Guid userId, decimal amount, TransactionType type);
    Task ResetDailyLimitsIfNeededAsync(Guid userId);
    Task<(decimal dailyTransfer, decimal dailyPayment, decimal dailyTopUp)> GetDailyLimitsAsync(Guid userId);
    Task<int> GetTransactionCountTodayAsync(Guid userId);
}
