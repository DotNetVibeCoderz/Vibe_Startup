using Microsoft.EntityFrameworkCore;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of transfer service (P2P and Split Bill)
/// </summary>
public class TransferService : ITransferService
{
    private readonly VibeWalletDbContext _context;
    private readonly IWalletService _walletService;
    private readonly ITransactionService _transactionService;
    private readonly ILogger<TransferService> _logger;

    public TransferService(VibeWalletDbContext context, IWalletService walletService,
        ITransactionService transactionService, ILogger<TransferService> logger)
    {
        _context = context;
        _walletService = walletService;
        _transactionService = transactionService;
        _logger = logger;
    }

    // ===== P2P Transfer =====
    public async Task<P2PTransfer> TransferAsync(Guid senderUserId, string receiverWalletNumber,
        decimal amount, string? notes = null)
    {
        var senderWallet = await _walletService.GetWalletByUserIdAsync(senderUserId)
            ?? throw new InvalidOperationException("Sender wallet not found");

        var receiverWallet = await _walletService.GetWalletByNumberAsync(receiverWalletNumber)
            ?? throw new InvalidOperationException("Receiver wallet not found");

        if (receiverWallet.UserId == senderUserId)
            throw new InvalidOperationException("Cannot transfer to yourself");

        if (amount > senderWallet.AvailableBalance)
            throw new InvalidOperationException("Insufficient balance");

        var transferRef = await _transactionService.GenerateTransactionRefAsync();

        // Deduct from sender
        var senderBalanceBefore = senderWallet.Balance;
        senderWallet.Balance -= amount;
        senderWallet.TotalSpending += amount;
        senderWallet.DailyTransferAmount += amount;
        senderWallet.MonthlyTransferAmount += amount;

        // Add to receiver
        var receiverBalanceBefore = receiverWallet.Balance;
        receiverWallet.Balance += amount;

        // Create transfer record
        var transfer = new P2PTransfer
        {
            SenderUserId = senderUserId,
            ReceiverUserId = receiverWallet.UserId,
            TransferRef = transferRef,
            Amount = amount,
            Fee = 0,
            Notes = notes,
            Status = TransactionStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };

        // Sender transaction
        var senderTransaction = new WalletTransaction
        {
            WalletId = senderWallet.Id,
            UserId = senderUserId,
            TransactionRef = transferRef,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = -amount,
            Fee = 0,
            BalanceBefore = senderBalanceBefore,
            BalanceAfter = senderWallet.Balance,
            Description = $"Transfer to {receiverWallet.User?.FullName ?? receiverWalletNumber}",
            CounterpartyName = receiverWallet.User?.FullName,
            CounterpartyWallet = receiverWalletNumber,
            Notes = notes,
            CompletedAt = DateTime.UtcNow
        };

        // Receiver transaction
        var receiverTransaction = new WalletTransaction
        {
            WalletId = receiverWallet.Id,
            UserId = receiverWallet.UserId,
            TransactionRef = transferRef + "-RCV",
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            Amount = amount,
            Fee = 0,
            BalanceBefore = receiverBalanceBefore,
            BalanceAfter = receiverWallet.Balance,
            Description = $"Transfer from {senderWallet.User?.FullName ?? "User"}",
            CounterpartyName = senderWallet.User?.FullName,
            CounterpartyWallet = senderWallet.WalletNumber,
            Notes = notes,
            CompletedAt = DateTime.UtcNow
        };

        _context.P2PTransfers.Add(transfer);
        _context.WalletTransactions.Add(senderTransaction);
        _context.WalletTransactions.Add(receiverTransaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("P2P Transfer: {Sender} -> {Receiver} Amount:{Amount} Ref:{Ref}",
            senderUserId, receiverWallet.UserId, amount, transferRef);

        return transfer;
    }

    public async Task<List<P2PTransfer>> GetTransferHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await _context.P2PTransfers
            .Where(t => (t.SenderUserId == userId || t.ReceiverUserId == userId) && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<P2PTransfer?> GetTransferByRefAsync(string transferRef)
    {
        return await _context.P2PTransfers.FirstOrDefaultAsync(t => t.TransferRef == transferRef);
    }

    // ===== Split Bill =====
    public async Task<SplitBill> CreateSplitBillAsync(Guid creatorUserId, string title,
        decimal totalAmount, List<Guid> participantUserIds, string? description = null)
    {
        var participantCount = participantUserIds.Count + 1; // +1 for creator
        var amountPerPerson = Math.Ceiling(totalAmount / participantCount / 100) * 100; // Round up to nearest 100

        var splitBill = new SplitBill
        {
            CreatorUserId = creatorUserId,
            Title = title,
            Description = description,
            TotalAmount = totalAmount,
            ParticipantCount = participantCount,
            AmountPerPerson = amountPerPerson,
            IsSettled = false
        };

        // Add creator as participant
        splitBill.Participants.Add(new SplitBillParticipant
        {
            SplitBillId = splitBill.Id,
            UserId = creatorUserId,
            Amount = amountPerPerson,
            Status = TransactionStatus.Pending
        });

        // Add other participants
        foreach (var participantId in participantUserIds)
        {
            splitBill.Participants.Add(new SplitBillParticipant
            {
                SplitBillId = splitBill.Id,
                UserId = participantId,
                Amount = amountPerPerson,
                Status = TransactionStatus.Pending
            });
        }

        splitBill.ParticipantCount = splitBill.Participants.Count;

        _context.SplitBills.Add(splitBill);
        await _context.SaveChangesAsync();

        return splitBill;
    }

    public async Task<SplitBill?> GetSplitBillAsync(Guid splitBillId)
    {
        return await _context.SplitBills
            .Include(s => s.Participants)
            .ThenInclude(p => p.User)
            .Include(s => s.Creator)
            .FirstOrDefaultAsync(s => s.Id == splitBillId);
    }

    public async Task<bool> PaySplitBillAsync(Guid userId, Guid splitBillId)
    {
        var participant = await _context.SplitBillParticipants
            .FirstOrDefaultAsync(p => p.SplitBillId == splitBillId && p.UserId == userId);

        if (participant == null) return false;

        // Process payment
        await TransferAsync(userId,
            (await _context.SplitBills.FindAsync(splitBillId))!.CreatorUserId.ToString(),
            participant.Amount, $"Split Bill Payment");

        participant.Status = TransactionStatus.Completed;
        participant.PaidAt = DateTime.UtcNow;

        // Check if all paid
        var splitBill = await _context.SplitBills
            .Include(s => s.Participants)
            .FirstAsync(s => s.Id == splitBillId);

        if (splitBill.Participants.All(p => p.Status == TransactionStatus.Completed))
        {
            splitBill.IsSettled = true;
            splitBill.SettledAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<SplitBill>> GetUserSplitBillsAsync(Guid userId)
    {
        return await _context.SplitBills
            .Include(s => s.Participants)
            .Include(s => s.Creator)
            .Where(s => s.CreatorUserId == userId ||
                        s.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> SettleSplitBillAsync(Guid splitBillId)
    {
        var splitBill = await _context.SplitBills.FindAsync(splitBillId);
        if (splitBill == null) return false;

        splitBill.IsSettled = true;
        splitBill.SettledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }
}
