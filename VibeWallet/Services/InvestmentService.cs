using Microsoft.EntityFrameworkCore;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of investment service
/// </summary>
public class InvestmentService : IInvestmentService
{
    private readonly VibeWalletDbContext _context;
    private readonly IWalletService _walletService;
    private readonly ILogger<InvestmentService> _logger;

    public InvestmentService(VibeWalletDbContext context, IWalletService walletService,
        ILogger<InvestmentService> logger)
    {
        _context = context;
        _walletService = walletService;
        _logger = logger;
    }

    // ===== Savings =====
    public async Task<SavingsAccount> CreateSavingsAccountAsync(Guid userId, string accountName, decimal initialDeposit)
    {
        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (initialDeposit > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        wallet.Balance -= initialDeposit;

        var accountNumber = "SAV" + DateTime.UtcNow.ToString("yyyyMMdd") + new Random().Next(1000, 9999);

        var savingsAccount = new SavingsAccount
        {
            UserId = userId,
            AccountName = accountName,
            AccountNumber = accountNumber,
            Balance = initialDeposit,
            InterestRate = 3.5m,
            StartDate = DateTime.UtcNow,
            IsActive = true
        };

        // Record initial deposit
        savingsAccount.Transactions.Add(new SavingsTransaction
        {
            SavingsAccountId = savingsAccount.Id,
            TransactionRef = "SAV-" + Guid.NewGuid().ToString("N")[..8],
            Type = "deposit",
            Amount = initialDeposit,
            BalanceAfter = initialDeposit,
            Notes = "Initial deposit"
        });

        _context.SavingsAccounts.Add(savingsAccount);

        // Record wallet transaction
        var walletTransaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = "SAV-" + Guid.NewGuid().ToString("N")[..8],
            Type = TransactionType.Payment,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = initialDeposit,
            Fee = 0,
            BalanceBefore = wallet.Balance + initialDeposit,
            BalanceAfter = wallet.Balance,
            Description = $"Create Savings - {accountName}",
            CompletedAt = DateTime.UtcNow
        };

        _context.WalletTransactions.Add(walletTransaction);
        await _context.SaveChangesAsync();

        return savingsAccount;
    }

    public async Task<List<SavingsAccount>> GetUserSavingsAccountsAsync(Guid userId)
    {
        return await _context.SavingsAccounts
            .Include(s => s.Transactions)
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<SavingsTransaction> DepositToSavingsAsync(Guid savingsAccountId, decimal amount)
    {
        var savings = await _context.SavingsAccounts.FindAsync(savingsAccountId)
            ?? throw new InvalidOperationException("Savings account not found");

        var wallet = await _walletService.GetWalletByUserIdAsync(savings.UserId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (amount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        wallet.Balance -= amount;
        savings.Balance += amount;

        var transaction = new SavingsTransaction
        {
            SavingsAccountId = savingsAccountId,
            TransactionRef = "SAVDEP-" + Guid.NewGuid().ToString("N")[..8],
            Type = "deposit",
            Amount = amount,
            BalanceAfter = savings.Balance,
            Notes = "Deposit to savings"
        };

        _context.SavingsTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<SavingsTransaction> WithdrawFromSavingsAsync(Guid savingsAccountId, decimal amount)
    {
        var savings = await _context.SavingsAccounts.FindAsync(savingsAccountId)
            ?? throw new InvalidOperationException("Savings account not found");

        if (amount > savings.Balance)
            throw new InvalidOperationException("Insufficient savings balance");

        var wallet = await _walletService.GetWalletByUserIdAsync(savings.UserId)
            ?? throw new InvalidOperationException("Wallet not found");

        savings.Balance -= amount;
        wallet.Balance += amount;

        var transaction = new SavingsTransaction
        {
            SavingsAccountId = savingsAccountId,
            TransactionRef = "SAVWDR-" + Guid.NewGuid().ToString("N")[..8],
            Type = "withdraw",
            Amount = amount,
            BalanceAfter = savings.Balance,
            Notes = "Withdraw from savings"
        };

        _context.SavingsTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task CalculateMonthlyInterestAsync()
    {
        var activeSavings = await _context.SavingsAccounts
            .Where(s => s.IsActive && !s.IsDeleted && s.Balance > 0)
            .ToListAsync();

        foreach (var savings in activeSavings)
        {
            var monthlyRate = savings.InterestRate / 100m / 12m;
            var interest = Math.Round(savings.Balance * monthlyRate, 2);

            if (interest > 0)
            {
                savings.Balance += interest;
                savings.TotalInterestEarned += interest;

                var transaction = new SavingsTransaction
                {
                    SavingsAccountId = savings.Id,
                    TransactionRef = "SAVINT-" + DateTime.UtcNow.ToString("yyyyMM"),
                    Type = "interest",
                    Amount = interest,
                    BalanceAfter = savings.Balance,
                    Notes = $"Monthly interest {savings.InterestRate}% p.a."
                };

                _context.SavingsTransactions.Add(transaction);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Monthly interest calculated for {Count} accounts", activeSavings.Count);
    }

    // ===== Investments =====
    public async Task<List<Investment>> GetAvailableInvestmentsAsync()
    {
        // Simulated investment products
        var products = new List<Investment>
        {
            new()
            {
                Type = InvestmentType.MutualFund,
                ProductName = "Reksa Dana Pasar Uang",
                ProductCode = "RDPU-001",
                RiskLevel = "low",
                ReturnPercentage = 5.5m,
                CurrentUnitPrice = 1500m
            },
            new()
            {
                Type = InvestmentType.Gold,
                ProductName = "Emas Digital 0.1g",
                ProductCode = "GLD-001",
                RiskLevel = "medium",
                ReturnPercentage = 8.0m,
                CurrentUnitPrice = 1050000m
            },
            new()
            {
                Type = InvestmentType.MutualFund,
                ProductName = "Reksa Dana Saham",
                ProductCode = "RDS-001",
                RiskLevel = "high",
                ReturnPercentage = 12.0m,
                CurrentUnitPrice = 2500m
            },
        };

        return products;
    }

    public async Task<Investment> InvestAsync(Guid userId, string productCode, decimal amount)
    {
        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (amount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var products = await GetAvailableInvestmentsAsync();
        var product = products.FirstOrDefault(p => p.ProductCode == productCode)
            ?? throw new InvalidOperationException("Investment product not found");

        wallet.Balance -= amount;

        var investment = new Investment
        {
            UserId = userId,
            Type = product.Type,
            ProductName = product.ProductName,
            ProductCode = productCode,
            InvestedAmount = amount,
            CurrentValue = amount,
            CurrentUnitPrice = product.CurrentUnitPrice,
            Units = amount / product.CurrentUnitPrice,
            RiskLevel = product.RiskLevel,
            InvestedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Investments.Add(investment);
        await _context.SaveChangesAsync();

        return investment;
    }

    public async Task<List<Investment>> GetUserInvestmentsAsync(Guid userId)
    {
        return await _context.Investments
            .Where(i => i.UserId == userId && !i.IsDeleted)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    // ===== Insurance =====
    public async Task<List<InsuranceProduct>> GetInsuranceProductsAsync()
    {
        return await _context.InsuranceProducts
            .Where(p => p.IsActive && !p.IsDeleted)
            .ToListAsync();
    }

    public async Task<UserInsurance> EnrollInsuranceAsync(Guid userId, Guid productId)
    {
        var product = await _context.InsuranceProducts.FindAsync(productId)
            ?? throw new InvalidOperationException("Insurance product not found");

        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        if (product.PremiumAmount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        wallet.Balance -= product.PremiumAmount;

        var enrollment = new UserInsurance
        {
            UserId = userId,
            InsuranceProductId = productId,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(product.DurationMonths),
            IsActive = true,
            PolicyNumber = "POL-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + new Random().Next(10000, 99999),
            NextPremiumDate = product.PremiumPeriod == "monthly" ? DateTime.UtcNow.AddMonths(1) : null
        };

        _context.UserInsurances.Add(enrollment);
        await _context.SaveChangesAsync();

        return enrollment;
    }

    public async Task<List<UserInsurance>> GetUserInsurancesAsync(Guid userId)
    {
        return await _context.UserInsurances
            .Include(u => u.InsuranceProduct)
            .Where(u => u.UserId == userId && !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }
}
