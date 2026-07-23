using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of rewards service (cashback, points, vouchers, promos)
/// </summary>
public class RewardsService : IRewardsService
{
    private readonly VibeWalletDbContext _context;
    private readonly RewardsConfig _config;
    private readonly ILogger<RewardsService> _logger;

    public RewardsService(VibeWalletDbContext context, IOptions<RewardsConfig> config,
        ILogger<RewardsService> logger)
    {
        _context = context;
        _config = config.Value;
        _logger = logger;
    }

    // ===== Cashback =====
    public async Task<Cashback> CalculateAndApplyCashbackAsync(Guid userId, string transactionRef, decimal amount)
    {
        var cashbackRate = _config.CashbackPercentage / 100m;
        var cashbackAmount = Math.Round(amount * cashbackRate, 2);

        if (cashbackAmount <= 0) return null!;

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet != null)
        {
            wallet.Balance += cashbackAmount;
        }

        var cashback = new Cashback
        {
            UserId = userId,
            TransactionRef = transactionRef,
            Description = $"Cashback {_config.CashbackPercentage}% - Transaction {transactionRef}",
            CashbackRate = cashbackRate,
            Amount = cashbackAmount,
            Status = TransactionStatus.Completed,
            CreditedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddMonths(3)
        };

        // Record as wallet transaction
        var walletTransaction = new WalletTransaction
        {
            WalletId = wallet!.Id,
            UserId = userId,
            TransactionRef = $"CB-{transactionRef}",
            Type = TransactionType.Cashback,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = cashbackAmount,
            Fee = 0,
            BalanceBefore = wallet.Balance - cashbackAmount,
            BalanceAfter = wallet.Balance,
            Description = cashback.Description,
            CompletedAt = DateTime.UtcNow
        };

        _context.Cashbacks.Add(cashback);
        _context.WalletTransactions.Add(walletTransaction);
        await _context.SaveChangesAsync();

        // Add loyalty points
        await AddPointsAsync(userId, _config.PointsPerTransaction,
            $"Points from transaction {transactionRef}");

        return cashback;
    }

    public async Task<List<Cashback>> GetCashbackHistoryAsync(Guid userId)
    {
        return await _context.Cashbacks
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    // ===== Loyalty Points =====
    public async Task<int> AddPointsAsync(Guid userId, int points, string description, string source = "transaction")
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) return 0;

        wallet.LoyaltyPoints += points;

        var pointRecord = new LoyaltyPoint
        {
            UserId = userId,
            Description = description,
            Points = points,
            PointSource = source,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        _context.LoyaltyPoints.Add(pointRecord);
        await _context.SaveChangesAsync();

        return wallet.LoyaltyPoints;
    }

    public async Task<bool> RedeemPointsAsync(Guid userId, int points, string description)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null || wallet.LoyaltyPoints < points) return false;

        wallet.LoyaltyPoints -= points;

        var pointRecord = new LoyaltyPoint
        {
            UserId = userId,
            Description = description,
            Points = -points,
            PointSource = "redemption"
        };

        _context.LoyaltyPoints.Add(pointRecord);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetUserPointsAsync(Guid userId)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        return wallet?.LoyaltyPoints ?? 0;
    }

    public async Task<List<LoyaltyPoint>> GetPointsHistoryAsync(Guid userId)
    {
        return await _context.LoyaltyPoints
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    // ===== Vouchers =====
    public async Task<List<Voucher>> GetAvailableVouchersAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.Vouchers
            .Where(v => v.IsActive && v.ValidFrom <= now && v.ValidUntil >= now
                        && v.UsedQuota < v.TotalQuota && !v.IsDeleted)
            .OrderBy(v => v.PointsRequired)
            .ToListAsync();
    }

    public async Task<UserVoucher?> ClaimVoucherAsync(Guid userId, Guid voucherId)
    {
        var voucher = await _context.Vouchers.FindAsync(voucherId);
        if (voucher == null || voucher.UsedQuota >= voucher.TotalQuota) return null;

        voucher.UsedQuota++;

        var userVoucher = new UserVoucher
        {
            UserId = userId,
            VoucherId = voucherId,
            ClaimedAt = DateTime.UtcNow
        };

        _context.UserVouchers.Add(userVoucher);
        await _context.SaveChangesAsync();

        return userVoucher;
    }

    public async Task<UserVoucher?> ClaimVoucherWithPointsAsync(Guid userId, Guid voucherId)
    {
        var voucher = await _context.Vouchers.FindAsync(voucherId);
        if (voucher == null || voucher.PointsRequired <= 0) return null;

        var redeemed = await RedeemPointsAsync(userId, voucher.PointsRequired,
            $"Redeem points for voucher: {voucher.Title}");

        if (!redeemed) return null;

        return await ClaimVoucherAsync(userId, voucherId);
    }

    public async Task<bool> RedeemVoucherAsync(Guid userId, Guid userVoucherId, string transactionRef)
    {
        var userVoucher = await _context.UserVouchers
            .FirstOrDefaultAsync(uv => uv.Id == userVoucherId && uv.UserId == userId);

        if (userVoucher == null || userVoucher.IsRedeemed) return false;

        userVoucher.IsRedeemed = true;
        userVoucher.RedeemedTransactionRef = transactionRef;
        userVoucher.RedeemedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<UserVoucher>> GetUserVouchersAsync(Guid userId)
    {
        return await _context.UserVouchers
            .Include(uv => uv.Voucher)
            .Where(uv => uv.UserId == userId && !uv.IsDeleted)
            .OrderByDescending(uv => uv.ClaimedAt)
            .ToListAsync();
    }

    // ===== Promos =====
    public async Task<List<Promo>> GetActivePromosAsync(string? category = null)
    {
        var now = DateTime.UtcNow;
        var query = _context.Promos
            .Where(p => p.IsActive && p.ValidFrom <= now && p.ValidUntil >= now && !p.IsDeleted);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        return await query.OrderBy(p => p.Priority).ToListAsync();
    }

    public async Task<List<Promo>> GetPersonalizedPromosAsync(Guid userId)
    {
        // Get user's spending categories (simplified - in real app would analyze transaction history)
        var transactions = await _context.WalletTransactions
            .Where(t => t.UserId == userId && t.Type == TransactionType.Payment)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .ToListAsync();

        // Return active promos - in real app would be personalized
        return await GetActivePromosAsync();
    }
}
