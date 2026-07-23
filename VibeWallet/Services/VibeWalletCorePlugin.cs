using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Plugin providing wallet and user data functions for Semantic Kernel.
/// Diregister di Kernel agar Mbak Selvi bisa query data real-time pengguna.
/// </summary>
public class VibeWalletCorePlugin
{
    private readonly VibeWalletDbContext _db;
    private readonly IWalletService _wallet;
    private readonly IRewardsService _rewards;
    private readonly ITransactionService _tx;
    private readonly ILogger<VibeWalletCorePlugin> _logger;
    private Guid _currentUserId;

    public VibeWalletCorePlugin(VibeWalletDbContext db, IWalletService wallet,
        IRewardsService rewards, ITransactionService tx, ILogger<VibeWalletCorePlugin> logger)
    {
        _db = db; _wallet = wallet; _rewards = rewards; _tx = tx; _logger = logger;
    }

    /// <summary>Set user context sebelum setiap percakapan</summary>
    public void SetUserContext(Guid userId) => _currentUserId = userId;

    // ================================================================
    //  BALANCE & WALLET
    // ================================================================

    [KernelFunction("get_user_balance")]
    [Description("Mendapatkan saldo wallet pengguna saat ini dalam Rupiah beserta detail wallet.")]
    [return: Description("Informasi saldo dan detail wallet dalam format JSON")]
    public async Task<string> GetUserBalance()
    {
        var wallet = await _wallet.GetWalletByUserIdAsync(_currentUserId);
        if (wallet == null) return "Wallet tidak ditemukan.";

        return JsonSerializer.Serialize(new
        {
            Saldo = $"Rp {wallet.AvailableBalance:N0}",
            wallet.WalletNumber,
            wallet.LoyaltyPoints,
            TotalTopUp = $"Rp {wallet.TotalTopUp:N0}",
            TotalSpending = $"Rp {wallet.TotalSpending:N0}",
            Status = wallet.IsFrozen ? "Dibekukan" : "Aktif"
        });
    }

    [KernelFunction("get_user_profile")]
    [Description("Mendapatkan informasi profil pengguna yang sedang login.")]
    [return: Description("Informasi profil pengguna dalam format JSON")]
    public async Task<string> GetUserProfile()
    {
        var user = await _db.Users.FindAsync(_currentUserId);
        if (user == null) return "Pengguna tidak ditemukan.";

        return JsonSerializer.Serialize(new
        {
            user.FullName, user.Email, user.PhoneNumber,
            StatusKYC = user.KycStatus.ToString(),
            Tema = user.ThemePreference,
            user.City, user.Province
        });
    }

    // ================================================================
    //  TRANSACTIONS
    // ================================================================

    [KernelFunction("get_transaction_history")]
    [Description("Mendapatkan riwayat transaksi terbaru pengguna. Gunakan parameter count (default 10, maks 20).")]
    [return: Description("Daftar transaksi terbaru dalam format JSON")]
    public async Task<string> GetTransactionHistory(
        [Description("Jumlah transaksi (1-20)")] int count = 10)
    {
        count = Math.Clamp(count, 1, 20);
        var txns = await _db.WalletTransactions
            .Where(t => t.UserId == _currentUserId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt).Take(count)
            .Select(t => new
            {
                Tanggal = t.CreatedAt.ToString("dd MMM yyyy HH:mm"),
                t.Description, Tipe = t.Type.ToString(),
                Jumlah = $"Rp {t.Amount:N0}",
                Status = t.Status.ToString(), t.TransactionRef
            }).ToListAsync();
        return JsonSerializer.Serialize(txns);
    }

    [KernelFunction("get_daily_limits")]
    [Description("Mengecek batas transaksi harian pengguna (transfer, pembayaran, top-up).")]
    [return: Description("Informasi batas harian")]
    public async Task<string> GetDailyLimits()
    {
        var limits = await _tx.GetDailyLimitsAsync(_currentUserId);
        return JsonSerializer.Serialize(new
        {
            TransferHarian = $"Rp {limits.dailyTransfer:N0} / Rp 25.000.000",
            PembayaranHarian = $"Rp {limits.dailyPayment:N0} / Rp 50.000.000",
            TopUpHarian = $"Rp {limits.dailyTopUp:N0} / Rp 10.000.000"
        });
    }
}
