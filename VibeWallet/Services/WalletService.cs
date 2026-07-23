using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of wallet operations
/// </summary>
public class WalletService : IWalletService
{
    private readonly VibeWalletDbContext _context;
    private readonly ILogger<WalletService> _logger;
    private readonly TransactionLimitsConfig _limits;

    public WalletService(VibeWalletDbContext context, ILogger<WalletService> logger,
        IOptions<TransactionLimitsConfig> limits)
    {
        _context = context;
        _logger = logger;
        _limits = limits.Value;
    }

    public async Task<Wallet?> GetWalletByUserIdAsync(Guid userId)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && !w.IsDeleted);
    }

    public async Task<Wallet?> GetWalletByNumberAsync(string walletNumber)
    {
        return await _context.Wallets
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.WalletNumber == walletNumber && !w.IsDeleted);
    }

    public async Task<decimal> GetBalanceAsync(Guid userId)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        return wallet?.AvailableBalance ?? 0;
    }

    public async Task<WalletTransaction> TopUpAsync(Guid userId, decimal amount, PaymentMethod method, string? notes = null)
    {
        var wallet = await GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (amount < _limits.MinTopUpAmount)
            throw new InvalidOperationException($"Minimum top-up amount is Rp {_limits.MinTopUpAmount:N0}");

        if (wallet.Balance + amount > _limits.MaxWalletBalance)
            throw new InvalidOperationException($"Maximum wallet balance is Rp {_limits.MaxWalletBalance:N0}");

        // Reset daily limits if needed
        if (wallet.DailyLimitResetAt <= DateTime.UtcNow)
        {
            wallet.DailyTopUpAmount = 0;
            wallet.DailyLimitResetAt = DateTime.UtcNow.Date.AddDays(1);
        }

        if (wallet.DailyTopUpAmount + amount > _limits.DailyTopUpLimit)
            throw new InvalidOperationException($"Daily top-up limit exceeded. Remaining: Rp {(_limits.DailyTopUpLimit - wallet.DailyTopUpAmount):N0}");

        var balanceBefore = wallet.Balance;

        wallet.Balance += amount;
        wallet.TotalTopUp += amount;
        wallet.DailyTopUpAmount += amount;

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = await GenerateRefAsync("TOPUP"),
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            Method = method,
            Amount = amount,
            Fee = 0,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = $"Top up via {method}",
            Notes = notes,
            CompletedAt = DateTime.UtcNow
        };

        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("TopUp successful: User:{UserId} Amount:{Amount} Ref:{Ref}",
            userId, amount, transaction.TransactionRef);

        return transaction;
    }

    public async Task<WalletTransaction> WithdrawAsync(Guid userId, decimal amount, string bankAccountNumber, string? notes = null)
    {
        var wallet = await GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (amount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var balanceBefore = wallet.Balance;
        wallet.Balance -= amount;

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = await GenerateRefAsync("WDRW"),
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.BankTransfer,
            Amount = -amount,
            Fee = 0,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = $"Withdraw to bank account {bankAccountNumber}",
            CounterpartyName = bankAccountNumber,
            Notes = notes,
            CompletedAt = DateTime.UtcNow
        };

        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<List<WalletTransaction>> GetTransactionHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await _context.WalletTransactions
            .Where(t => t.UserId == userId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<WalletTransaction>> GetTransactionsByDateRangeAsync(Guid userId, DateTime start, DateTime end)
    {
        return await _context.WalletTransactions
            .Where(t => t.UserId == userId && !t.IsDeleted &&
                        t.CreatedAt >= start && t.CreatedAt <= end)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<BalanceHistory> GetDailyBalanceSnapshotAsync(Guid walletId, DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var transactions = await _context.WalletTransactions
            .Where(t => t.WalletId == walletId && !t.IsDeleted &&
                        t.CreatedAt >= startOfDay && t.CreatedAt < endOfDay)
            .ToListAsync();

        var totalIn = transactions
            .Where(t => t.Type == TransactionType.TopUp || t.Type == TransactionType.Cashback || t.Type == TransactionType.Refund)
            .Sum(t => t.Amount);

        var totalOut = transactions
            .Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.Transfer || t.Type == TransactionType.Withdraw)
            .Sum(t => Math.Abs(t.Amount));

        var openingBalance = transactions.OrderBy(t => t.CreatedAt).FirstOrDefault()?.BalanceBefore ?? 0;
        var closingBalance = transactions.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.BalanceAfter ?? 0;

        return new BalanceHistory
        {
            WalletId = walletId,
            SnapshotDate = date.Date,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            TotalIn = totalIn,
            TotalOut = totalOut,
            TransactionCount = transactions.Count
        };
    }

    public async Task<bool> FreezeWalletAsync(Guid userId, string reason)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        if (wallet == null) return false;

        wallet.IsFrozen = true;
        wallet.FreezeReason = reason;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnfreezeWalletAsync(Guid userId)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        if (wallet == null) return false;

        wallet.IsFrozen = false;
        wallet.FreezeReason = null;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> GenerateWalletNumberAsync()
    {
        string walletNumber;
        var rng = Random.Shared;
        do
        {
            // Generate 15-digit number starting with '1'
            walletNumber = "1";
            for (int i = 0; i < 15; i++)
                walletNumber += rng.Next(0, 10).ToString();
        }
        while (await _context.Wallets.AnyAsync(w => w.WalletNumber == walletNumber));

        return walletNumber;
    }

    private async Task<string> GenerateRefAsync(string prefix)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Random.Shared.Next(1000, 9999);
        return $"VW-{prefix}-{timestamp}-{random}";
    }
}
