using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of transaction processing service
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly VibeWalletDbContext _context;
    private readonly TransactionLimitsConfig _limits;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(VibeWalletDbContext context, IOptions<TransactionLimitsConfig> limits,
        ILogger<TransactionService> logger)
    {
        _context = context;
        _limits = limits.Value;
        _logger = logger;
    }

    public async Task<string> GenerateTransactionRefAsync()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random = new Random().Next(1000, 9999);
        return $"VW-{timestamp}-{random}";
    }

    public async Task<WalletTransaction?> GetTransactionByRefAsync(string transactionRef)
    {
        return await _context.WalletTransactions
            .FirstOrDefaultAsync(t => t.TransactionRef == transactionRef);
    }

    public async Task<List<WalletTransaction>> GetAllTransactionsAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await _context.WalletTransactions
            .Where(t => t.UserId == userId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<WalletTransaction> ProcessTransactionAsync(WalletTransaction transaction)
    {
        transaction.Status = TransactionStatus.Completed;
        transaction.CompletedAt = DateTime.UtcNow;

        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction processed: {Ref} Type:{Type} Amount:{Amount}",
            transaction.TransactionRef, transaction.Type, transaction.Amount);

        return transaction;
    }

    public async Task<bool> CheckDailyLimitsAsync(Guid userId, decimal amount, TransactionType type)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) return false;

        // Reset if needed
        await ResetDailyLimitsIfNeededAsync(userId);

        return type switch
        {
            TransactionType.Transfer => (wallet.DailyTransferAmount + amount) <= _limits.DailyTransferLimit,
            TransactionType.Payment => (wallet.DailyPaymentAmount + amount) <= _limits.DailyPaymentLimit,
            TransactionType.TopUp => (wallet.DailyTopUpAmount + amount) <= _limits.DailyTopUpLimit,
            _ => true
        };
    }

    public async Task ResetDailyLimitsIfNeededAsync(Guid userId)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) return;

        if (wallet.DailyLimitResetAt <= DateTime.UtcNow)
        {
            wallet.DailyTransferAmount = 0;
            wallet.DailyPaymentAmount = 0;
            wallet.DailyTopUpAmount = 0;
            wallet.DailyLimitResetAt = DateTime.UtcNow.Date.AddDays(1);
        }

        if (wallet.MonthlyLimitResetAt <= DateTime.UtcNow)
        {
            wallet.MonthlyTransferAmount = 0;
            wallet.MonthlyLimitResetAt = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<(decimal dailyTransfer, decimal dailyPayment, decimal dailyTopUp)> GetDailyLimitsAsync(Guid userId)
    {
        await ResetDailyLimitsIfNeededAsync(userId);
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);

        return wallet == null
            ? (0, 0, 0)
            : (wallet.DailyTransferAmount, wallet.DailyPaymentAmount, wallet.DailyTopUpAmount);
    }

    public async Task<int> GetTransactionCountTodayAsync(Guid userId)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.WalletTransactions
            .CountAsync(t => t.UserId == userId && t.CreatedAt >= today);
    }
}
