using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VibeWallet.Data;
using VibeWallet.Models;
using VibeWallet.Services;

namespace VibeWallet.Api;

/// <summary>
/// Minimal API endpoints for VibeWallet
/// Provides REST API for external integrations
/// </summary>
public static class Endpoints
{
    public static void MapAll(WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        // ===== Health Check =====
        api.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow, App = "VibeWallet" }));

        // ===== Wallet Endpoints =====
        MapWalletEndpoints(api);

        // ===== User Endpoints =====
        MapUserEndpoints(api);

        // ===== Transaction Endpoints =====
        MapTransactionEndpoints(api);

        // ===== Payment Endpoints =====
        MapPaymentEndpoints(api);

        // ===== Transfer Endpoints =====
        MapTransferEndpoints(api);

        // ===== Bank Endpoints =====
        MapBankEndpoints(api);

        // ===== Rewards Endpoints =====
        MapRewardsEndpoints(api);

        // ===== Chat Endpoints =====
        MapChatEndpoints(api);
    }

    private static void MapWalletEndpoints(RouteGroupBuilder api)
    {
        var wallet = api.MapGroup("/wallet").RequireAuthorization();

        wallet.MapGet("/balance", async (IWalletService walletService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var balance = await walletService.GetBalanceAsync(userId);
            return Results.Ok(new { UserId = userId, Balance = balance });
        });

        wallet.MapGet("/{userId:guid}", async (Guid userId, IWalletService walletService) =>
        {
            var w = await walletService.GetWalletByUserIdAsync(userId);
            return w == null ? Results.NotFound() : Results.Ok(w);
        });

        wallet.MapPost("/topup", async (TopUpRequest request, IWalletService walletService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var transaction = await walletService.TopUpAsync(userId, request.Amount, request.Method, request.Notes);
            return Results.Ok(transaction);
        });

        wallet.MapPost("/withdraw", async (WithdrawRequest request, IWalletService walletService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var transaction = await walletService.WithdrawAsync(userId, request.Amount, request.BankAccount, request.Notes);
            return Results.Ok(transaction);
        });

        wallet.MapGet("/transactions", async (int? page, int? pageSize, IWalletService walletService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var transactions = await walletService.GetTransactionHistoryAsync(userId, page ?? 1, pageSize ?? 20);
            return Results.Ok(transactions);
        });
    }

    private static void MapUserEndpoints(RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").RequireAuthorization();

        users.MapGet("/me", async (IUserService userService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var user = await userService.GetUserByIdAsync(userId);
            return user == null ? Results.NotFound() : Results.Ok(new
            {
                user.Id, user.FullName, user.Email, user.PhoneNumber,
                user.KycStatus, user.ThemePreference,
                Wallet = user.Wallet
            });
        });

        users.MapGet("/{userId:guid}", async (Guid userId, IUserService userService) =>
        {
            var user = await userService.GetUserByIdAsync(userId);
            return user == null ? Results.NotFound() : Results.Ok(new
            {
                user.Id, user.FullName, user.Email, user.PhoneNumber, user.KycStatus
            });
        });

        users.MapPut("/profile", async (UpdateProfileRequest request, IUserService userService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var user = await userService.GetUserByIdAsync(userId);
            if (user == null) return Results.NotFound();

            user.FullName = request.FullName ?? user.FullName;
            user.Address = request.Address ?? user.Address;
            user.City = request.City ?? user.City;

            await userService.UpdateProfileAsync(userId, user);
            return Results.Ok(new { Message = "Profile updated" });
        });

        users.MapPost("/pin", async (SetPinRequest request, IUserService userService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            await userService.SetTransactionPinAsync(userId, request.Pin);
            return Results.Ok(new { Message = "PIN set successfully" });
        });
    }

    private static void MapTransactionEndpoints(RouteGroupBuilder api)
    {
        var tx = api.MapGroup("/transactions").RequireAuthorization();

        tx.MapGet("/", async (int? page, int? pageSize, ITransactionService transactionService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var transactions = await transactionService.GetAllTransactionsAsync(userId, page ?? 1, pageSize ?? 20);
            return Results.Ok(transactions);
        });

        tx.MapGet("/{transactionRef}", async (string transactionRef, ITransactionService transactionService) =>
        {
            var t = await transactionService.GetTransactionByRefAsync(transactionRef);
            return t == null ? Results.NotFound() : Results.Ok(t);
        });

        tx.MapGet("/limits", async (ITransactionService transactionService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var limits = await transactionService.GetDailyLimitsAsync(userId);
            return Results.Ok(new
            {
                DailyTransfer = limits.dailyTransfer,
                DailyPayment = limits.dailyPayment,
                DailyTopUp = limits.dailyTopUp
            });
        });
    }

    private static void MapPaymentEndpoints(RouteGroupBuilder api)
    {
        var payments = api.MapGroup("/payments").RequireAuthorization();

        payments.MapPost("/qris", async (QrisRequest request, IPaymentService paymentService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var payment = await paymentService.ProcessQrisPaymentAsync(userId, request.QrContent, request.Amount, request.Notes);
            return Results.Ok(payment);
        });

        payments.MapPost("/bill", async (BillRequest request, IPaymentService paymentService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var payment = await paymentService.PayBillAsync(userId, request.BillType, request.ProviderName,
                request.CustomerId, request.Amount, request.BillPeriod);
            return Results.Ok(payment);
        });

        payments.MapPost("/topup", async (MobileTopUpRequest request, IPaymentService paymentService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var topUp = await paymentService.ProcessTopUpAsync(userId, request.TopUpType, request.Provider,
                request.PhoneNumber, request.ProductCode, request.Amount);
            return Results.Ok(topUp);
        });

        payments.MapGet("/bill/check", async (BillType type, string customerId, IPaymentService paymentService) =>
        {
            var amount = await paymentService.CheckBillAmountAsync(type, customerId);
            return Results.Ok(new { BillType = type, CustomerId = customerId, Amount = amount });
        });
    }

    private static void MapTransferEndpoints(RouteGroupBuilder api)
    {
        var transfers = api.MapGroup("/transfers").RequireAuthorization();

        transfers.MapPost("/p2p", async (P2PRequest request, ITransferService transferService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var transfer = await transferService.TransferAsync(userId, request.ReceiverWalletNumber, request.Amount, request.Notes);
            return Results.Ok(transfer);
        });

        transfers.MapPost("/split", async (SplitBillRequest request, ITransferService transferService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var splitBill = await transferService.CreateSplitBillAsync(userId, request.Title,
                request.TotalAmount, request.ParticipantIds, request.Description);
            return Results.Ok(splitBill);
        });

        transfers.MapPost("/split/{splitBillId:guid}/pay", async (Guid splitBillId, ITransferService transferService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var result = await transferService.PaySplitBillAsync(userId, splitBillId);
            return result ? Results.Ok(new { Message = "Payment successful" }) : Results.BadRequest();
        });
    }

    private static void MapBankEndpoints(RouteGroupBuilder api)
    {
        var banks = api.MapGroup("/banks").RequireAuthorization();

        banks.MapGet("/", async (IBankService bankService) =>
        {
            var banksList = await bankService.GetSupportedBanksAsync();
            return Results.Ok(banksList);
        });

        banks.MapGet("/accounts", async (IBankService bankService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var accounts = await bankService.GetUserBankAccountsAsync(userId);
            return Results.Ok(accounts);
        });

        banks.MapPost("/transfer", async (BankTransferRequest request, IBankService bankService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var transfer = await bankService.TransferToBankAsync(userId, request.BankCode,
                request.AccountNumber, request.Amount, request.Notes);
            return Results.Ok(transfer);
        });
    }

    private static void MapRewardsEndpoints(RouteGroupBuilder api)
    {
        var rewards = api.MapGroup("/rewards").RequireAuthorization();

        rewards.MapGet("/points", async (IRewardsService rewardsService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var points = await rewardsService.GetUserPointsAsync(userId);
            return Results.Ok(new { UserId = userId, Points = points });
        });

        rewards.MapGet("/vouchers", async (IRewardsService rewardsService) =>
        {
            var vouchers = await rewardsService.GetAvailableVouchersAsync();
            return Results.Ok(vouchers);
        });

        rewards.MapGet("/promos", async (string? category, IRewardsService rewardsService) =>
        {
            var promos = await rewardsService.GetActivePromosAsync(category);
            return Results.Ok(promos);
        });

        rewards.MapPost("/vouchers/{voucherId:guid}/claim", async (Guid voucherId, IRewardsService rewardsService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var claimed = await rewardsService.ClaimVoucherAsync(userId, voucherId);
            return claimed == null ? Results.BadRequest("Voucher not available") : Results.Ok(claimed);
        });
    }

    private static void MapChatEndpoints(RouteGroupBuilder api)
    {
        var chat = api.MapGroup("/chat").RequireAuthorization();

        chat.MapGet("/sessions", async (IChatService chatService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var sessions = await chatService.GetUserSessionsAsync(userId);
            return Results.Ok(sessions);
        });

        chat.MapPost("/sessions", async (CreateSessionRequest request, IChatService chatService, HttpContext context) =>
        {
            var userId = GetUserId(context);
            var session = await chatService.CreateSessionAsync(userId, request.Title, request.Provider);
            return Results.Ok(session);
        });

        chat.MapPost("/sessions/{sessionId:guid}/messages", async (Guid sessionId, SendMessageRequest request, IChatService chatService) =>
        {
            var message = await chatService.SendMessageAsync(sessionId, request.Message, request.Attachments);
            return Results.Ok(message);
        });

        chat.MapGet("/sessions/{sessionId:guid}/messages", async (Guid sessionId, IChatService chatService) =>
        {
            var messages = await chatService.GetSessionMessagesAsync(sessionId);
            return Results.Ok(messages);
        });

        chat.MapDelete("/sessions/{sessionId:guid}", async (Guid sessionId, IChatService chatService) =>
        {
            await chatService.DeleteSessionAsync(sessionId);
            return Results.Ok(new { Message = "Session deleted" });
        });
    }

    private static Guid GetUserId(HttpContext context)
    {
        var claim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirst("sub");
        return claim != null ? Guid.Parse(claim.Value) : Guid.Empty;
    }
}

// ===== Request DTOs =====
public record TopUpRequest(decimal Amount, PaymentMethod Method, string? Notes);
public record WithdrawRequest(decimal Amount, string BankAccount, string? Notes);
public record UpdateProfileRequest(string? FullName, string? Address, string? City);
public record SetPinRequest(string Pin);
public record QrisRequest(string QrContent, decimal Amount, string? Notes);
public record BillRequest(BillType BillType, string ProviderName, string CustomerId, decimal Amount, string BillPeriod);
public record MobileTopUpRequest(TopUpType TopUpType, ProviderType Provider, string PhoneNumber, string ProductCode, decimal Amount);
public record P2PRequest(string ReceiverWalletNumber, decimal Amount, string? Notes);
public record SplitBillRequest(string Title, decimal TotalAmount, List<Guid> ParticipantIds, string? Description);
public record BankTransferRequest(string BankCode, string AccountNumber, decimal Amount, string? Notes);
public record CreateSessionRequest(string? Title, ChatProvider Provider);
public record SendMessageRequest(string Message, List<ChatAttachment>? Attachments);
