using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of payment service
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly VibeWalletDbContext _context;
    private readonly IWalletService _walletService;
    private readonly ITransactionService _transactionService;
    private readonly IRewardsService _rewardsService;
    private readonly TransactionLimitsConfig _limits;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(VibeWalletDbContext context, IWalletService walletService,
        ITransactionService transactionService, IRewardsService rewardsService,
        IOptions<TransactionLimitsConfig> limits, ILogger<PaymentService> logger)
    {
        _context = context;
        _walletService = walletService;
        _transactionService = transactionService;
        _rewardsService = rewardsService;
        _limits = limits.Value;
        _logger = logger;
    }

    // ===== QRIS =====
    public async Task<QrisPayment> ProcessQrisPaymentAsync(Guid userId, string qrContent, decimal amount, string? notes = null)
    {
        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (amount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var paymentRef = await _transactionService.GenerateTransactionRefAsync();
        var balanceBefore = wallet.Balance;

        wallet.Balance -= amount;
        wallet.TotalSpending += amount;
        wallet.DailyPaymentAmount += amount;

        var payment = new QrisPayment
        {
            UserId = userId,
            PaymentRef = paymentRef,
            QrContent = qrContent,
            Amount = amount,
            Status = TransactionStatus.Completed,
            PaidAt = DateTime.UtcNow,
            Notes = notes
        };

        // Record wallet transaction
        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = paymentRef,
            Type = TransactionType.Payment,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.QRIS,
            Amount = amount,
            Fee = 0,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = $"QRIS Payment - {amount:C}",
            Notes = notes,
            CompletedAt = DateTime.UtcNow
        };

        _context.QrisPayments.Add(payment);
        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Apply cashback
        await _rewardsService.CalculateAndApplyCashbackAsync(userId, paymentRef, amount);

        return payment;
    }

    public async Task<QrisPayment?> GetQrisPaymentAsync(string paymentRef)
    {
        return await _context.QrisPayments.FirstOrDefaultAsync(p => p.PaymentRef == paymentRef);
    }

    public async Task<string> GenerateQrCodeAsync(string content)
    {
        // Using QRCoder library
        var qrGenerator = new QRCoder.QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.Q);
        var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
        var qrBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrBytes);
    }

    // ===== Bill Payment =====
    public async Task<BillPayment> PayBillAsync(Guid userId, BillType billType, string providerName,
        string customerId, decimal amount, string billPeriod)
    {
        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        var adminFee = 2500m; // Default admin fee
        var totalAmount = amount + adminFee;

        if (totalAmount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var paymentRef = await _transactionService.GenerateTransactionRefAsync();
        var balanceBefore = wallet.Balance;

        wallet.Balance -= totalAmount;
        wallet.TotalSpending += totalAmount;

        var billPayment = new BillPayment
        {
            UserId = userId,
            PaymentRef = paymentRef,
            BillType = billType,
            ProviderName = providerName,
            CustomerId = customerId,
            BillPeriod = billPeriod,
            Amount = amount,
            AdminFee = adminFee,
            Status = TransactionStatus.Completed,
            PaidAt = DateTime.UtcNow
        };

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = paymentRef,
            Type = TransactionType.Payment,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = totalAmount,
            Fee = adminFee,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = $"Bill Payment - {billType}: {providerName} ({customerId})",
            CompletedAt = DateTime.UtcNow
        };

        _context.BillPayments.Add(billPayment);
        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return billPayment;
    }

    public async Task<List<BillPayment>> GetBillHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await _context.BillPayments
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<decimal> CheckBillAmountAsync(BillType billType, string customerId)
    {
        // Simulated bill checking - in real app would call provider API
        var random = new Random();
        return billType switch
        {
            BillType.Electricity => random.Next(100000, 2000000),
            BillType.Water => random.Next(50000, 500000),
            BillType.Internet => random.Next(200000, 800000),
            BillType.BPJS => random.Next(50000, 500000),
            _ => random.Next(50000, 1000000)
        };
    }

    // ===== Mobile Top-up =====
    public async Task<MobileTopUp> ProcessTopUpAsync(Guid userId, TopUpType type, ProviderType provider,
        string phoneNumber, string productCode, decimal amount)
    {
        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (amount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var topUpRef = await _transactionService.GenerateTransactionRefAsync();
        var balanceBefore = wallet.Balance;

        wallet.Balance -= amount;
        wallet.TotalSpending += amount;

        var topUp = new MobileTopUp
        {
            UserId = userId,
            TopUpRef = topUpRef,
            Type = type,
            Provider = provider,
            PhoneNumber = phoneNumber,
            ProductCode = productCode,
            Amount = amount,
            Status = TransactionStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = topUpRef,
            Type = TransactionType.Payment,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = amount,
            Fee = 0,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = $"{type} - {provider}: {phoneNumber}",
            CompletedAt = DateTime.UtcNow
        };

        _context.MobileTopUps.Add(topUp);
        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return topUp;
    }

    public async Task<List<MobileTopUp>> GetTopUpHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await _context.MobileTopUps
            .Where(t => t.UserId == userId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<(string Code, string Name, decimal Price)>> GetAvailableProductsAsync(ProviderType provider, TopUpType type)
    {
        // Simulated product catalog
        var products = new List<(string Code, string Name, decimal Price)>();

        if (type == TopUpType.Pulsa)
        {
            products.AddRange(new[]
            {
                ("P5K", "Pulsa 5.000", 5500m),
                ("P10K", "Pulsa 10.000", 10500m),
                ("P20K", "Pulsa 20.000", 20000m),
                ("P50K", "Pulsa 50.000", 49500m),
                ("P100K", "Pulsa 100.000", 99000m),
            });
        }
        else if (type == TopUpType.DataPackage)
        {
            products.AddRange(new[]
            {
                ("D1GB", "Data 1 GB / 7 Hari", 15000m),
                ("D3GB", "Data 3 GB / 30 Hari", 35000m),
                ("D5GB", "Data 5 GB / 30 Hari", 50000m),
                ("D10GB", "Data 10 GB / 30 Hari", 85000m),
                ("D25GB", "Data 25 GB / 30 Hari", 150000m),
            });
        }
        else if (type == TopUpType.ElectricityToken)
        {
            products.AddRange(new[]
            {
                ("E20K", "Token 20.000", 20000m),
                ("E50K", "Token 50.000", 50000m),
                ("E100K", "Token 100.000", 100000m),
                ("E200K", "Token 200.000", 200000m),
                ("E500K", "Token 500.000", 500000m),
            });
        }

        return products;
    }

    // ===== E-commerce =====
    public async Task<EcommercePayment> ProcessEcommercePaymentAsync(Guid userId, string platform,
        string orderId, decimal amount, string? details = null)
    {
        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (amount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var paymentRef = await _transactionService.GenerateTransactionRefAsync();
        var balanceBefore = wallet.Balance;

        wallet.Balance -= amount;
        wallet.TotalSpending += amount;

        var payment = new EcommercePayment
        {
            UserId = userId,
            PaymentRef = paymentRef,
            PlatformName = platform,
            OrderId = orderId,
            Amount = amount,
            OrderDetails = details,
            Status = TransactionStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = paymentRef,
            Type = TransactionType.Payment,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = amount,
            Fee = 0,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = $"E-commerce Payment - {platform}: {orderId}",
            CompletedAt = DateTime.UtcNow
        };

        _context.EcommercePayments.Add(payment);
        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return payment;
    }
}
