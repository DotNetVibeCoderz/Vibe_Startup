// Checkout: voucher validation, PayLater maths, and writing the transaction.
// Everything the admin console, settlement and fraud screens need starts here -
// before this existed, the checkout page produced no record at all.
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Payments;
using Joka.Models.Users;
using Joka.Services.Payments;

namespace Joka.Services;

public record VoucherResult(bool Success, string Message, decimal Discount, PromoVoucher? Voucher);

public record PayLaterPlan(int TenorMonths, decimal MonthlyAmount, decimal TotalAmount, decimal InterestAmount);

/// <param name="PaymentUrl">
/// Set when a real gateway wants the customer on its own page. Null for the
/// simulated gateway, which settles on the spot.
/// </param>
public record CheckoutResult(
    bool Success, string Message, PaymentTransaction? Transaction, int PointsEarned,
    string? PaymentUrl = null);

public class CheckoutService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly FraudDetectionService _fraud;
    private readonly NotificationService _notifications;
    private readonly PaymentGatewayFactory _gateways;

    public CheckoutService(
        AppDbContext db, IConfiguration config,
        FraudDetectionService fraud, NotificationService notifications,
        PaymentGatewayFactory gateways)
    {
        _db = db;
        _config = config;
        _fraud = fraud;
        _notifications = notifications;
        _gateways = gateways;
    }

    // ------------------------------------------------------------------
    // Vouchers
    // ------------------------------------------------------------------
    /// <summary>
    /// Validates a voucher against the database rather than the flat Rp50.000
    /// the checkout page used to assume. Checks activity, window, quota,
    /// minimum spend and product applicability, and caps the discount.
    /// </summary>
    public async Task<VoucherResult> ApplyVoucherAsync(string? code, decimal subtotal, string bookingType)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new(false, "Masukkan kode voucher dulu.", 0, null);

        var normalised = code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var voucher = await _db.PromoVouchers.FirstOrDefaultAsync(v => v.Code == normalised);

        if (voucher is null)
            return new(false, $"Kode {normalised} tidak ditemukan.", 0, null);

        if (!voucher.IsActive)
            return new(false, "Voucher ini sedang tidak aktif.", 0, null);

        if (voucher.ValidFrom > now)
            return new(false, $"Voucher berlaku mulai {voucher.ValidFrom:dd MMM yyyy}.", 0, null);

        if (voucher.ValidUntil < now)
            return new(false, $"Voucher sudah kedaluwarsa pada {voucher.ValidUntil:dd MMM yyyy}.", 0, null);

        if (voucher.UsedCount >= voucher.TotalQuota)
            return new(false, "Kuota voucher sudah habis.", 0, null);

        if (subtotal < voucher.MinPurchase)
            return new(false, $"Minimum transaksi {Rupiah(voucher.MinPurchase)} untuk memakai voucher ini.", 0, null);

        if (!string.IsNullOrEmpty(voucher.ApplicableTo)
            && !voucher.ApplicableTo.Equals("All", StringComparison.OrdinalIgnoreCase)
            && !voucher.ApplicableTo.Equals(bookingType, StringComparison.OrdinalIgnoreCase))
            return new(false, $"Voucher ini hanya berlaku untuk {voucher.ApplicableTo}.", 0, null);

        var discount = voucher.Type switch
        {
            "Percentage" => subtotal * voucher.Value / 100m,
            _ => voucher.Value
        };

        if (voucher.MaxDiscount > 0)
            discount = Math.Min(discount, voucher.MaxDiscount);

        discount = Math.Min(discount, subtotal);
        discount = Math.Round(discount, 0);

        return new(true, $"Voucher {voucher.Code} dipakai, hemat {Rupiah(discount)}.", discount, voucher);
    }

    // ------------------------------------------------------------------
    // PayLater
    // ------------------------------------------------------------------
    /// <summary>
    /// Instalment options from the Payment:PayLater section of appsettings.
    /// Flat interest per month, which is how the local providers quote it.
    /// </summary>
    public IReadOnlyList<PayLaterPlan> GetPayLaterPlans(decimal amount)
    {
        var section = _config.GetSection("Payment:PayLater");

        var maxTenor = section.GetValue<int?>("MaxTenorMonths") ?? 12;
        var minAmount = section.GetValue<decimal?>("MinAmount") ?? 500000m;
        var rate = section.GetValue<decimal?>("InterestRate") ?? 2.5m;

        if (amount < minAmount) return Array.Empty<PayLaterPlan>();

        var tenors = new[] { 3, 6, 12 }.Where(t => t <= maxTenor);
        var plans = new List<PayLaterPlan>();

        foreach (var tenor in tenors)
        {
            var interest = Math.Round(amount * rate / 100m * tenor, 0);
            var total = amount + interest;
            plans.Add(new PayLaterPlan(tenor, Math.Round(total / tenor, 0), total, interest));
        }

        return plans;
    }

    public decimal PayLaterMinimum() =>
        _config.GetSection("Payment:PayLater").GetValue<decimal?>("MinAmount") ?? 500000m;

    // ------------------------------------------------------------------
    // Checkout
    // ------------------------------------------------------------------
    /// <summary>
    /// Writes the PaymentTransaction, consumes the voucher, and awards loyalty
    /// points. Returns the transaction so the caller can show its code.
    /// </summary>
    public async Task<CheckoutResult> PayAsync(
        Guid? userId,
        string bookingType,
        Guid bookingId,
        decimal subtotal,
        string paymentMethod,
        PromoVoucher? voucher,
        decimal discount,
        TravelInsurance? insurance,
        int? payLaterTenor,
        string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
            return new(false, "Pilih metode pembayaran dulu.", null, 0);

        var insuranceAmount = insurance?.Price ?? 0m;
        var finalAmount = subtotal - discount + insuranceAmount;

        if (finalAmount < 0) finalAmount = 0;

        // PayLater is charged with interest, so the recorded amount must match.
        if (payLaterTenor is int tenor && tenor > 0)
        {
            var plan = GetPayLaterPlans(finalAmount).FirstOrDefault(p => p.TenorMonths == tenor);
            if (plan is not null) finalAmount = plan.TotalAmount;
        }

        var gateway = _gateways.Create();
        var code = $"JKA-PAY-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var customer = userId is Guid buyer
            ? await _db.Users.AsNoTracking()
                .Where(u => u.Id == buyer)
                .Select(u => new { u.FullName, u.Email, u.PhoneNumber })
                .FirstOrDefaultAsync()
            : null;

        // The charge is raised before anything is written, so a gateway refusal
        // leaves no orphan transaction behind.
        var charge = await gateway.CreateChargeAsync(new ChargeRequest(
            OrderId: code,
            Amount: finalAmount,
            PaymentMethod: paymentMethod,
            CustomerName: customer?.FullName,
            CustomerEmail: customer?.Email,
            CustomerPhone: customer?.PhoneNumber,
            ItemName: $"Joka {bookingType}",
            ReturnUrl: returnUrl ?? "https://localhost:7204/my-bookings"));

        if (!charge.Success)
            return new(false, charge.Message, null, 0);

        var transaction = new PaymentTransaction
        {
            UserId = userId ?? Guid.Empty,
            BookingType = bookingType,
            BookingId = bookingId,
            TransactionCode = code,
            Amount = subtotal,
            DiscountAmount = discount > 0 ? discount : null,
            InsuranceAmount = insuranceAmount > 0 ? insuranceAmount : null,
            FinalAmount = finalAmount,
            PaymentMethod = paymentMethod,
            PaymentGateway = gateway.Name,
            // Recorded rather than consumed here: the quota is only burned once
            // the money actually lands, which may be a webhook away.
            VoucherCode = voucher?.Code,
            GatewayTransactionId = charge.GatewayTransactionId,
            PaymentUrl = charge.PaymentUrl,
            // Only the stub gateway may declare the money received here. A real
            // gateway leaves this Pending until its webhook says otherwise.
            Status = charge.Settled ? "Completed" : "Pending",
            PaidAt = charge.Settled ? DateTime.UtcNow : null,
            ExpiryAt = DateTime.UtcNow.AddHours(1)
        };

        _db.PaymentTransactions.Add(transaction);

        // An unpaid transaction earns nothing and consumes nothing: no voucher
        // quota, no loyalty points, no "payment received" notification. All of
        // that happens in SettleAsync when the webhook confirms.
        if (!charge.Settled)
        {
            await _db.SaveChangesAsync();

            return new(true,
                "Transaksi dibuat. Selesaikan pembayaran di halaman penyedia.",
                transaction, 0, charge.PaymentUrl);
        }

        await _db.SaveChangesAsync();

        var points = await AwardAsync(transaction);

        return new(true, "Pembayaran berhasil.", transaction, points);
    }

    // ------------------------------------------------------------------
    // Settlement
    // ------------------------------------------------------------------
    /// <summary>
    /// Marks a Pending transaction paid. Called from the gateway webhooks in
    /// Program.cs after the signature has been verified - never from the browser.
    /// Idempotent: gateways retry notifications, and a retry must not hand out
    /// the loyalty points twice.
    /// </summary>
    public async Task<CheckoutResult> SettleAsync(string transactionCode, string status, string? gatewayReference)
    {
        var tx = await _db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode);

        if (tx is null)
            return new(false, $"Transaksi {transactionCode} tidak ditemukan.", null, 0);

        if (gatewayReference is not null) tx.GatewayTransactionId = gatewayReference;

        if (tx.Status == "Completed")
            return new(true, "Transaksi sudah lunas sebelumnya.", tx, 0);

        tx.Status = status;
        tx.UpdatedAt = DateTime.UtcNow;

        if (status != "Completed")
        {
            if (status == "Failed") tx.FailureReason = "Ditolak atau kedaluwarsa di gateway.";
            await _db.SaveChangesAsync();
            return new(true, $"Status transaksi menjadi {status}.", tx, 0);
        }

        tx.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var points = await AwardAsync(tx);

        return new(true, "Pembayaran dikonfirmasi gateway.", tx, points);
    }

    /// <summary>
    /// Everything that may only happen once the money is real: voucher quota,
    /// loyalty points, fraud screening, and the customer notification.
    /// </summary>
    private async Task<int> AwardAsync(PaymentTransaction transaction)
    {
        var userId = transaction.UserId == Guid.Empty ? (Guid?)null : transaction.UserId;

        if (!string.IsNullOrEmpty(transaction.VoucherCode))
        {
            var tracked = await _db.PromoVouchers
                .FirstOrDefaultAsync(v => v.Code == transaction.VoucherCode);

            if (tracked is not null)
            {
                tracked.UsedCount++;

                if (userId is Guid uid)
                {
                    _db.UserVouchers.Add(new UserVoucher
                    {
                        UserId = uid,
                        PromoVoucherId = tracked.Id,
                        IsUsed = true,
                        UsedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // 1 point per Rp10.000 spent - the tier field finally has something to move it.
        var points = (int)(transaction.FinalAmount / 10000m);

        if (userId is Guid loyaltyUser && points > 0)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == loyaltyUser);
            if (user is not null)
            {
                user.LoyaltyPoints += points;
                user.MembershipTier = TierFor(user.LoyaltyPoints);

                _db.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    UserId = loyaltyUser,
                    Points = points,
                    Type = "Earn",
                    Description = $"Transaksi {transaction.TransactionCode}",
                    ReferenceType = "Booking",
                    ReferenceId = transaction.TransactionCode
                });
            }
        }

        await _db.SaveChangesAsync();

        // Screen after the payment is committed, so a scoring problem can never
        // cost the customer their transaction.
        var email = userId is Guid screened
            ? await _db.Users.Where(u => u.Id == screened).Select(u => u.Email).FirstOrDefaultAsync()
            : null;

        await _fraud.ScreenAsync(transaction, email);

        if (userId is Guid notify)
        {
            var extra = points > 0 ? $" Kamu dapat {points} poin." : "";
            await _notifications.SendAsync(notify,
                "Pembayaran berhasil",
                $"Transaksi {transaction.TransactionCode} sebesar {Rupiah(transaction.FinalAmount)} sudah kami terima.{extra}",
                "Transaction",
                "/my-bookings");
        }

        return points;
    }

    /// <summary>
    /// Applies a voucher to a transaction that already exists, for operators
    /// handling compensation or a code the customer forgot at checkout.
    /// Recalculates the total and consumes the quota, same as a normal redemption.
    /// </summary>
    public async Task<CheckoutResult> ApplyVoucherToTransactionAsync(
        Guid transactionId, string code, string actor)
    {
        var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == transactionId);
        if (tx is null) return new(false, "Transaksi tidak ditemukan.", null, 0);

        if (tx.Status is "Refunded" or "Failed")
            return new(false, $"Transaksi berstatus {tx.Status} tidak bisa diberi voucher.", null, 0);

        if (tx.DiscountAmount is > 0)
            return new(false, "Transaksi ini sudah memakai voucher.", null, 0);

        var check = await ApplyVoucherAsync(code, tx.Amount, tx.BookingType);
        if (!check.Success || check.Voucher is null)
            return new(false, check.Message, null, 0);

        var before = tx.FinalAmount;
        tx.DiscountAmount = check.Discount;
        tx.FinalAmount = Math.Max(0, tx.Amount - check.Discount + (tx.InsuranceAmount ?? 0));
        tx.UpdatedAt = DateTime.UtcNow;

        var voucher = await _db.PromoVouchers.FirstOrDefaultAsync(v => v.Id == check.Voucher.Id);
        if (voucher is not null) voucher.UsedCount++;

        _db.AuditLogs.Add(new Joka.Models.Common.AuditLog
        {
            EntityName = "PaymentTransaction",
            EntityId = tx.TransactionCode,
            Action = "ApplyVoucher",
            Changes = $"{check.Voucher.Code}: {before:N0} -> {tx.FinalAmount:N0} (diskon {check.Discount:N0})",
            UserId = actor,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return new(true,
            $"Voucher {check.Voucher.Code} diterapkan. Total turun dari {Rupiah(before)} ke {Rupiah(tx.FinalAmount)}.",
            tx, 0);
    }

    public static string TierFor(int points) => points switch
    {
        >= 10000 => "Platinum",
        >= 5000 => "Gold",
        >= 1000 => "Silver",
        _ => "Classic"
    };

    private static string Rupiah(decimal value) => $"Rp{value:N0}";
}
