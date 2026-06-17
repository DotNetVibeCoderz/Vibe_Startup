using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk memproses pembayaran (simulasi)
/// </summary>
public class PaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    // Daftar metode pembayaran yang didukung
    public static readonly string[] PaymentMethods = { "EWallet", "CreditCard", "BankTransfer", "QRIS" };

    // Daftar e-wallet yang didukung
    public static readonly string[] EWallets = { "GoPay", "OVO", "DANA", "ShopeePay", "LinkAja" };

    // Daftar bank untuk transfer
    public static readonly string[] Banks = { "BCA", "Mandiri", "BNI", "BRI", "CIMB Niaga", "BSI" };

    public PaymentService(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    /// <summary>
    /// Memproses pembayaran (simulasi - selalu sukses)
    /// </summary>
    public async Task<(bool success, string? message, Payment? payment)> ProcessPaymentAsync(
        int orderId, string paymentMethod, string? paymentDetails = null)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return (false, "Order tidak ditemukan", null);
        if (order.Status == "Paid") return (false, "Order sudah dibayar", null);
        if (order.Status == "Cancelled") return (false, "Order sudah dibatalkan", null);

        // Simulasi: pembayaran selalu berhasil
        var transactionId = $"TRX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";

        var payment = new Payment
        {
            OrderId = orderId,
            PaymentMethod = paymentMethod,
            Amount = order.GrandTotal,
            TransactionId = transactionId,
            Status = "Success",
            PaymentResponse = System.Text.Json.JsonSerializer.Serialize(new
            {
                transactionId,
                paymentMethod,
                amount = order.GrandTotal,
                timestamp = DateTime.UtcNow,
                status = "success"
            }),
            CompletedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);

        // Update order status
        order.Status = "Paid";
        order.PaidAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _audit.LogAsync("Payment", "Order", orderId.ToString(), null,
            System.Text.Json.JsonSerializer.Serialize(new { paymentMethod, order.GrandTotal, transactionId }));

        return (true, "Pembayaran berhasil!", payment);
    }

    public async Task<Payment?> GetByOrderAsync(int orderId)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
    }

    /// <summary>
    /// Generate payment instructions/invoice
    /// </summary>
    public async Task<string> GeneratePaymentInstructionsAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Showtime!).ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null) return "Order tidak ditemukan";

        return $@"
INVOICE PEMBAYARAN - BIOSKOP
============================
No. Order: {order.OrderNumber}
Film: {order.Showtime?.Movie?.Title}
Tanggal: {order.Showtime?.StartTime:dd MMM yyyy HH:mm}
Total: Rp {order.GrandTotal:N0}
Status: {order.Status}
============================
Silakan lakukan pembayaran melalui metode yang tersedia.
";
    }
}
