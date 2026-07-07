using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class PaymentService
{
    private readonly AppDbContext _db;
    public PaymentService(AppDbContext db) => _db = db;

    public async Task<List<Payment>> GetAllAsync() =>
        await _db.Payments.Include(p => p.User).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<List<Payment>> GetUserPaymentsAsync(string userId) =>
        await _db.Payments.Where(p => p.UserId == userId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<Payment> CreateAsync(Payment payment)
    {
        payment.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return payment;
    }

    public async Task<Payment?> UpdateStatusAsync(int id, PaymentStatus status, string? transactionId = null)
    {
        var payment = await _db.Payments.FindAsync(id);
        if (payment == null) return null;
        payment.Status = status;
        payment.TransactionId = transactionId;
        if (status == PaymentStatus.Completed) payment.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return payment;
    }

    /// <summary>
    /// Admin: generate invoice bulanan untuk semua member aktif yang belum punya payment bulan ini.
    /// </summary>
    public async Task<(int generated, string message)> GenerateMonthlyPaymentsAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // Cari member aktif yang sudah punya payment bulan ini (any status selain Cancelled/Refunded)
        var alreadyBilled = await _db.Payments
            .Where(p => p.CreatedAt >= monthStart && p.CreatedAt < monthEnd
                     && p.Status != PaymentStatus.Cancelled && p.Status != PaymentStatus.Refunded)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync();

        // Ambil member aktif yang BELUM ditagih bulan ini
        var members = await _db.Users
            .Where(u => u.IsActive && u.Role == UserRole.Member && !alreadyBilled.Contains(u.Id))
            .ToListAsync();

        if (!members.Any()) return (0, "Semua member sudah memiliki invoice bulan ini.");

        // Ambil paket membership untuk tentukan amount
        var plans = await _db.MembershipPlans.Where(p => p.IsActive).ToListAsync();
        var defaultAmount = plans.Any() ? plans.Min(p => p.Price) : 300000m;

        int count = 0;
        foreach (var member in members)
        {
            // Cek membership member
            var memberMembership = await _db.MemberMemberships
                .Include(m => m.MembershipPlan)
                .Where(m => m.UserId == member.Id && m.Status == MembershipStatus.Active)
                .OrderByDescending(m => m.EndDate)
                .FirstOrDefaultAsync();

            var amount = memberMembership?.MembershipPlan?.DiscountedPrice
                      ?? memberMembership?.MembershipPlan?.Price
                      ?? defaultAmount;

            var payment = new Payment
            {
                UserId = member.Id,
                InvoiceNumber = $"INV-{now:yyyyMM}-{member.FullName[..Math.Min(3, member.FullName.Length)].ToUpper()}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                Amount = amount,
                Method = PaymentMethod.BankTransfer,
                Status = PaymentStatus.Pending,
                Description = $"Membership bulan {now:MMMM yyyy} — {memberMembership?.MembershipPlan?.Name ?? "Regular"}",
                CreatedAt = now
            };
            _db.Payments.Add(payment);
            count++;
        }

        await _db.SaveChangesAsync();
        return (count, $"✅ {count} invoice berhasil dibuat untuk bulan {now:MMMM yyyy}.");
    }

    /// <summary>
    /// Member: konfirmasi bahwa sudah melakukan pembayaran.
    /// Status berubah dari Pending → Confirmed.
    /// </summary>
    public async Task<(bool ok, string message)> ConfirmPaymentAsync(int paymentId, string userId, string method, string? transactionId)
    {
        var payment = await _db.Payments.FindAsync(paymentId);
        if (payment == null) return (false, "Payment tidak ditemukan.");
        if (payment.UserId != userId) return (false, "Ini bukan payment kamu.");
        if (payment.Status != PaymentStatus.Pending) return (false, $"Status saat ini: {payment.Status}. Hanya Pending yang bisa dikonfirmasi.");

        payment.Status = PaymentStatus.Confirmed;
        payment.Method = Enum.TryParse<PaymentMethod>(method, out var pm) ? pm : PaymentMethod.BankTransfer;
        payment.TransactionId = transactionId;
        await _db.SaveChangesAsync();

        return (true, "✅ Konfirmasi berhasil! Menunggu verifikasi admin.");
    }

    /// <summary>
    /// Admin: verifikasi pembayaran yang sudah dikonfirmasi member. Status Confirmed → Completed.
    /// </summary>
    public async Task<(bool ok, string message)> VerifyPaymentAsync(int paymentId)
    {
        var payment = await _db.Payments.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment == null) return (false, "Payment tidak ditemukan.");
        if (payment.Status != PaymentStatus.Confirmed) return (false, $"Status saat ini: {payment.Status}. Hanya Confirmed yang bisa diverifikasi.");

        payment.Status = PaymentStatus.Completed;
        payment.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var memberName = payment.User?.FullName ?? "Member";
        return (true, $"✅ Pembayaran {payment.InvoiceNumber} dari {memberName} telah diverifikasi.");
    }

    /// <summary>
    /// Admin: reject pembayaran yang dikonfirmasi. Status Confirmed → Pending (kembali).
    /// </summary>
    public async Task<(bool ok, string message)> RejectPaymentAsync(int paymentId)
    {
        var payment = await _db.Payments.FindAsync(paymentId);
        if (payment == null) return (false, "Payment tidak ditemukan.");
        if (payment.Status != PaymentStatus.Confirmed) return (false, $"Status saat ini: {payment.Status}.");

        payment.Status = PaymentStatus.Pending;
        payment.TransactionId = null;
        await _db.SaveChangesAsync();
        return (true, "Pembayaran dikembalikan ke status Pending.");
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _db.Payments.Where(p => p.Status == PaymentStatus.Completed);
        if (from.HasValue) query = query.Where(p => p.PaidAt >= from);
        if (to.HasValue) query = query.Where(p => p.PaidAt <= to);
        return await query.SumAsync(p => p.Amount);
    }

    public async Task<List<object>> GetRevenueByMonthAsync()
    {
        return (await _db.Payments.Where(p => p.Status == PaymentStatus.Completed && p.PaidAt.HasValue)
            .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt.Value.Month })
            .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Total = g.Sum(p => p.Amount), Count = g.Count() })
            .OrderBy(g => g.Year).ThenBy(g => g.Month).ToListAsync())
            .Cast<object>().ToList();
    }
}
