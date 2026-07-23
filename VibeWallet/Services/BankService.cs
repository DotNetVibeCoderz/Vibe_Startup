using Microsoft.EntityFrameworkCore;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of bank integration service
/// </summary>
public class BankService : IBankService
{
    private readonly VibeWalletDbContext _context;
    private readonly IWalletService _walletService;
    private readonly ITransactionService _transactionService;
    private readonly ILogger<BankService> _logger;

    public BankService(VibeWalletDbContext context, IWalletService walletService,
        ITransactionService transactionService, ILogger<BankService> logger)
    {
        _context = context;
        _walletService = walletService;
        _transactionService = transactionService;
        _logger = logger;
    }

    public async Task<List<SupportedBank>> GetSupportedBanksAsync()
    {
        return await _context.SupportedBanks
            .Where(b => b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.SortOrder)
            .ToListAsync();
    }

    public async Task<SupportedBank?> GetBankByCodeAsync(string bankCode)
    {
        return await _context.SupportedBanks
            .FirstOrDefaultAsync(b => b.BankCode == bankCode && b.IsActive);
    }

    public async Task<List<BankAccount>> GetUserBankAccountsAsync(Guid userId)
    {
        return await _context.BankAccounts
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .OrderByDescending(b => b.IsPrimary)
            .ToListAsync();
    }

    public async Task<BankAccount> AddBankAccountAsync(Guid userId, string bankName,
        string bankCode, string accountNumber, string accountHolderName)
    {
        // If this is the first account, make it primary
        var existingAccounts = await GetUserBankAccountsAsync(userId);
        var isPrimary = !existingAccounts.Any();

        var account = new BankAccount
        {
            UserId = userId,
            BankName = bankName,
            BankCode = bankCode,
            AccountNumber = accountNumber,
            AccountHolderName = accountHolderName,
            IsVerified = false, // Would require verification in real app
            IsPrimary = isPrimary
        };

        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        return account;
    }

    public async Task<bool> RemoveBankAccountAsync(Guid userId, Guid bankAccountId)
    {
        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == bankAccountId && b.UserId == userId);

        if (account == null) return false;

        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetPrimaryBankAccountAsync(Guid userId, Guid bankAccountId)
    {
        var accounts = await _context.BankAccounts
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .ToListAsync();

        foreach (var account in accounts)
        {
            account.IsPrimary = account.Id == bankAccountId;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<BankTransfer> TransferToBankAsync(Guid userId, string destinationBankCode,
        string destinationAccountNumber, decimal amount, string? notes = null)
    {
        var bank = await GetBankByCodeAsync(destinationBankCode)
            ?? throw new InvalidOperationException("Bank not supported");

        var wallet = await _walletService.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Wallet not found");

        var fee = bank.AdminFee;
        var totalAmount = amount + fee;

        if (totalAmount > wallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var transferRef = await _transactionService.GenerateTransactionRefAsync();
        var balanceBefore = wallet.Balance;

        wallet.Balance -= totalAmount;
        wallet.TotalSpending += totalAmount;

        var transfer = new BankTransfer
        {
            UserId = userId,
            TransferRef = transferRef,
            DestinationBank = bank.BankName,
            DestinationBankCode = destinationBankCode,
            DestinationAccountNumber = destinationAccountNumber,
            Amount = amount,
            Fee = fee,
            Notes = notes,
            Status = TransactionStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            TransactionRef = transferRef,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.BankTransfer,
            Amount = totalAmount,
            Fee = fee,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = $"Bank Transfer to {bank.BankName} - {destinationAccountNumber}",
            CounterpartyName = destinationAccountNumber,
            Notes = notes,
            CompletedAt = DateTime.UtcNow
        };

        _context.BankTransfers.Add(transfer);
        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transfer;
    }
}
