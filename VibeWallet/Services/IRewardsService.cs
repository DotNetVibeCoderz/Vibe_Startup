using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for rewards, cashback, points, vouchers, and promos
/// </summary>
public interface IRewardsService
{
    // Cashback
    Task<Cashback> CalculateAndApplyCashbackAsync(Guid userId, string transactionRef, decimal amount);
    Task<List<Cashback>> GetCashbackHistoryAsync(Guid userId);

    // Loyalty Points
    Task<int> AddPointsAsync(Guid userId, int points, string description, string source = "transaction");
    Task<bool> RedeemPointsAsync(Guid userId, int points, string description);
    Task<int> GetUserPointsAsync(Guid userId);
    Task<List<LoyaltyPoint>> GetPointsHistoryAsync(Guid userId);

    // Vouchers
    Task<List<Voucher>> GetAvailableVouchersAsync();
    Task<UserVoucher?> ClaimVoucherAsync(Guid userId, Guid voucherId);
    Task<UserVoucher?> ClaimVoucherWithPointsAsync(Guid userId, Guid voucherId);
    Task<bool> RedeemVoucherAsync(Guid userId, Guid userVoucherId, string transactionRef);
    Task<List<UserVoucher>> GetUserVouchersAsync(Guid userId);

    // Promos
    Task<List<Promo>> GetActivePromosAsync(string? category = null);
    Task<List<Promo>> GetPersonalizedPromosAsync(Guid userId);
}
