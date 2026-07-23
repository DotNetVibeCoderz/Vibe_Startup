using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Plugin providing rewards, promos, banks, insurance info for Semantic Kernel.
/// Mbak Selvi bisa panggil fungsi-fungsi ini untuk info promo/voucher/bank/asuransi.
/// </summary>
public class VibeWalletInfoPlugin
{
    private readonly VibeWalletDbContext _db;
    private readonly ILogger<VibeWalletInfoPlugin> _logger;

    public VibeWalletInfoPlugin(VibeWalletDbContext db, ILogger<VibeWalletInfoPlugin> logger)
    { _db = db; _logger = logger; }

    // ================================================================
    //  PROMOS
    // ================================================================

    [KernelFunction("get_active_promos")]
    [Description("Mendapatkan daftar promo yang sedang aktif. Bisa filter kategori: food, transport, shopping, atau 'all'.")]
    [return: Description("Daftar promo aktif dalam format JSON")]
    public async Task<string> GetActivePromos(
        [Description("Kategori promo: food, transport, shopping, atau 'all'")] string category = "all")
    {
        var now = DateTime.UtcNow;
        var query = _db.Promos.Where(p => p.IsActive && p.ValidFrom <= now && p.ValidUntil >= now && !p.IsDeleted);
        if (!string.IsNullOrEmpty(category) && category != "all")
            query = query.Where(p => p.Category == category);

        var promos = await query.OrderBy(p => p.Priority).Take(10)
            .Select(p => new
            {
                p.Title, p.Description, p.MerchantName, p.Category,
                TipePromo = p.Type.ToString(), p.Value,
                BerlakuHingga = p.ValidUntil.ToString("dd MMM yyyy")
            }).ToListAsync();

        return promos.Any() ? JsonSerializer.Serialize(promos)
            : $"Saat ini tidak ada promo aktif{(category != "all" ? $" untuk kategori '{category}'" : "")}.";
    }

    // ================================================================
    //  VOUCHERS
    // ================================================================

    [KernelFunction("get_available_vouchers")]
    [Description("Mendapatkan daftar voucher yang bisa diklaim, termasuk yang bisa ditukar dengan loyalty points.")]
    [return: Description("Daftar voucher dalam format JSON")]
    public async Task<string> GetAvailableVouchers()
    {
        var now = DateTime.UtcNow;
        var vouchers = await _db.Vouchers
            .Where(v => v.IsActive && v.ValidFrom <= now && v.ValidUntil >= now
                        && v.UsedQuota < v.TotalQuota && !v.IsDeleted)
            .OrderBy(v => v.PointsRequired)
            .Select(v => new
            {
                v.VoucherCode, v.Title, v.Description,
                v.VoucherType, v.Value, v.MinimumTransaction, v.MaximumDiscount,
                v.PointsRequired, SisaKuota = v.TotalQuota - v.UsedQuota,
                BerlakuHingga = v.ValidUntil.ToString("dd MMM yyyy")
            }).ToListAsync();

        return vouchers.Any() ? JsonSerializer.Serialize(vouchers) : "Saat ini tidak ada voucher tersedia.";
    }

    // ================================================================
    //  BANKS
    // ================================================================

    [KernelFunction("get_supported_banks")]
    [Description("Mendapatkan daftar bank yang didukung untuk transfer dan top-up.")]
    [return: Description("Daftar bank dalam format JSON")]
    public async Task<string> GetSupportedBanks()
    {
        var banks = await _db.SupportedBanks
            .Where(b => b.IsActive && !b.IsDeleted).OrderBy(b => b.SortOrder)
            .Select(b => new
            {
                b.BankName, b.BankCode,
                BiayaTransfer = $"Rp {b.TransferFee:N0}",
                BiayaAdmin = $"Rp {b.AdminFee:N0}"
            }).ToListAsync();
        return JsonSerializer.Serialize(banks);
    }

    // ================================================================
    //  INSURANCE
    // ================================================================

    [KernelFunction("get_insurance_products")]
    [Description("Mendapatkan daftar produk asuransi yang tersedia. Bisa filter jenis: Health, Travel, Gadget, Vehicle, Life.")]
    [return: Description("Daftar produk asuransi dalam format JSON")]
    public async Task<string> GetInsuranceProducts(
        [Description("Jenis asuransi (opsional)")] string? type = null)
    {
        var query = _db.InsuranceProducts.Where(p => p.IsActive && !p.IsDeleted);
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<InsuranceType>(type, true, out var insType))
            query = query.Where(p => p.Type == insType);

        var products = await query.Select(p => new
        {
            p.ProductName, Tipe = p.Type.ToString(), p.ProviderName, p.Description,
            Premi = $"Rp {p.PremiumAmount:N0}", p.PremiumPeriod,
            Cakupan = $"Rp {p.CoverageAmount:N0}",
            Durasi = $"{p.DurationMonths} bulan"
        }).ToListAsync();

        return products.Any() ? JsonSerializer.Serialize(products) : "Tidak ada produk asuransi tersedia.";
    }

    // ================================================================
    //  SAVINGS
    // ================================================================

    [KernelFunction("get_savings_info")]
    [Description("Mendapatkan informasi tentang produk tabungan digital VibeWallet beserta suku bunga.")]
    [return: Description("Informasi tabungan digital")]
    public Task<string> GetSavingsInfo()
    {
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            Info = "Tabungan digital VibeWallet menawarkan bunga hingga 3.5% per tahun! 🏦",
            BungaPerTahun = "3.5%",
            BungaPerBulan = "0.29%",
            MinimalSetoran = "Rp 10.000",
            BebasBiayaAdmin = true,
            CaraDaftar = "Klik menu Savings di aplikasi VibeWallet"
        }));
    }
}
