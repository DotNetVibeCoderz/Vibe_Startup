using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Payment processing service
/// </summary>
public class PaymentService
{
    private readonly NgibridDbContext _db;
    private readonly AuditService _audit;

    public PaymentService(NgibridDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>
    /// Open a payment for an order. Payment is 1:1 with Order — EF puts a unique index on OrderId —
    /// so a second call must never insert: an unsettled row is re-pointed at the newly chosen
    /// method/channel (the customer changed their mind, or just re-opened the instructions), and an
    /// already-settled one is refused. Without this the second click died on a raw
    /// DbUpdateException about a constraint the caller has no way to know about.
    /// </summary>
    public async Task<Payment> CreatePaymentAsync(long orderId, string method, string channel)
    {
        var order = await _db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        var existing = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        if (existing is not null)
        {
            if (existing.Status is "PAID" or "REFUNDED")
                throw new InvalidOperationException(
                    $"Pesanan {order.OrderNumber} sudah dibayar ({existing.PaymentNumber}).");

            existing.PaymentMethod = method;
            existing.PaymentChannel = channel;
            existing.Status = "PENDING";
            existing.Amount = order.TotalAmount;
            existing.TotalAmount = order.TotalAmount;
            existing.ExpiredAt = DateTime.UtcNow.AddHours(24);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("UPDATE_PAYMENT", "Payment", existing.Id,
                notes: $"Payment for order {order.OrderNumber} re-opened as {method}/{channel}");
            return existing;
        }

        var payment = new Payment
        {
            OrderId = orderId,
            PaymentNumber = $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            PaymentMethod = method,
            PaymentChannel = channel,
            Amount = order.TotalAmount,
            TotalAmount = order.TotalAmount,
            Status = "PENDING",
            ExpiredAt = DateTime.UtcNow.AddHours(24)
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CREATE_PAYMENT", "Payment", payment.Id, notes: $"Payment for order {order.OrderNumber}");
        return payment;
    }

    public async Task<Payment> ConfirmPaymentAsync(long paymentId, string transactionId)
    {
        var payment = await _db.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException();
        payment.Status = "PAID";
        payment.PaidAt = DateTime.UtcNow;
        payment.TransactionId = transactionId;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CONFIRM_PAYMENT", "Payment", paymentId, notes: $"Confirmed with TXN: {transactionId}");
        return payment;
    }

    public async Task<List<Payment>> GetPaymentsAsync(int page = 1, int pageSize = 20) =>
        await _db.Payments.Include(p => p.Order)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    /// <summary>Payment channels supported per method, driving the checkout UI.</summary>
    public static readonly Dictionary<string, string[]> Channels = new()
    {
        ["E_WALLET"] = new[] { "GoPay", "OVO", "DANA", "ShopeePay", "LinkAja" },
        ["BANK_TRANSFER"] = new[] { "BCA", "Mandiri", "BNI", "BRI", "Permata" },
        ["CREDIT_CARD"] = new[] { "Visa", "Mastercard", "JCB" },
        ["COD"] = new[] { "Cash on Delivery" }
    };

    /// <summary>
    /// Mark a payment failed (gateway decline / expiry) so it stops showing as collectable.
    /// </summary>
    public async Task<Payment> FailPaymentAsync(long paymentId, string reason)
    {
        var payment = await _db.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException($"Payment {paymentId} not found");
        payment.Status = "FAILED";
        payment.Notes = reason;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("FAIL_PAYMENT", "Payment", paymentId, notes: reason);
        return payment;
    }

    /// <summary>Aggregate finance figures straight from persisted rows (no UI-side estimates).</summary>
    public async Task<FinanceSummary> GetFinanceSummaryAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var payments = await _db.Payments.Where(p => p.CreatedAt >= since).ToListAsync();
        var invoices = await _db.Invoices.Where(i => i.CreatedAt >= since).ToListAsync();

        return new FinanceSummary
        {
            TotalCollected = payments.Where(p => p.Status == "PAID").Sum(p => p.TotalAmount),
            TotalOutstanding = payments.Where(p => p.Status == "PENDING").Sum(p => p.TotalAmount),
            TransactionCount = payments.Count,
            PaidCount = payments.Count(p => p.Status == "PAID"),
            TaxCollected = invoices.Sum(i => i.TaxAmount),
            InsurancePremium = invoices.Sum(i => i.InsuranceFee),
            ByMethod = payments.GroupBy(p => p.PaymentMethod)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.TotalAmount))
        };
    }
}

public class FinanceSummary
{
    public decimal TotalCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int TransactionCount { get; set; }
    public int PaidCount { get; set; }
    public decimal TaxCollected { get; set; }
    public decimal InsurancePremium { get; set; }
    public Dictionary<string, decimal> ByMethod { get; set; } = new();
}

/// <summary>
/// Invoice generation service
/// </summary>
public class InvoiceService
{
    private readonly NgibridDbContext _db;

    public InvoiceService(NgibridDbContext db) { _db = db; }

    public async Task<Invoice> GenerateInvoiceAsync(long orderId)
    {
        var order = await _db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException();
        
        var invoice = new Invoice
        {
            OrderId = orderId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            SubTotal = order.BasePrice,
            TaxAmount = order.TaxAmount,
            InsuranceFee = order.InsuranceFee,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Status = "UNPAID"
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    public async Task<Invoice?> GetInvoiceAsync(long orderId) =>
        await _db.Invoices.Include(i => i.Order).FirstOrDefaultAsync(i => i.OrderId == orderId);

    public async Task<List<Invoice>> GetInvoicesAsync(int page = 1, int pageSize = 50) =>
        await _db.Invoices.Include(i => i.Order)
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    /// <summary>
    /// Generate the invoice for an order, or return the existing one — invoice numbers must not
    /// be duplicated for the same order.
    /// </summary>
    public async Task<Invoice> GetOrGenerateInvoiceAsync(long orderId)
    {
        var existing = await GetInvoiceAsync(orderId);
        if (existing != null) return existing;
        return await GenerateInvoiceAsync(orderId);
    }

    /// <summary>
    /// Render a printable HTML invoice / e-receipt. HTML keeps it dependency-free and the browser's
    /// own print-to-PDF produces the PDF.
    /// </summary>
    public async Task<string> RenderInvoiceHtmlAsync(long invoiceId)
    {
        var invoice = await _db.Invoices.Include(i => i.Order).ThenInclude(o => o!.Customer)
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new KeyNotFoundException($"Invoice {invoiceId} not found");

        var o = invoice.Order;
        string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "-");

        // Two '$' so a single brace stays literal for the CSS block; interpolation uses {{ }}.
        return $$"""
            <!DOCTYPE html>
            <html lang="id"><head><meta charset="utf-8"/>
            <title>Invoice {{Esc(invoice.InvoiceNumber)}}</title>
            <style>
              body{font-family:'Segoe UI',Arial,sans-serif;margin:40px;color:#1c1e21;}
              h1{margin:0;font-size:22px;} .muted{color:#65676b;}
              table{width:100%;border-collapse:collapse;margin-top:24px;}
              th,td{padding:10px;border-bottom:1px solid #dadde1;text-align:left;}
              th{background:#f0f2f5;} .right{text-align:right;}
              .total{font-weight:700;font-size:18px;}
              .head{display:flex;justify-content:space-between;align-items:flex-start;}
              @media print{ body{margin:12px;} }
            </style></head><body>
            <div class="head">
              <div><h1>🚚 Ngibrid Logistics</h1>
                <div class="muted">Platform Manajemen Logistik</div></div>
              <div class="right"><h1>INVOICE</h1>
                <div class="muted">{{Esc(invoice.InvoiceNumber)}}</div>
                <div class="muted">Tanggal: {{invoice.InvoiceDate:dd MMM yyyy}}</div>
                <div class="muted">Jatuh tempo: {{invoice.DueDate:dd MMM yyyy}}</div></div>
            </div>
            <table>
              <tr><th>Pelanggan</th><th>Pesanan</th></tr>
              <tr>
                <td>{{Esc(o?.Customer?.FullName)}}<br/><span class="muted">{{Esc(o?.Customer?.Email)}}</span></td>
                <td>{{Esc(o?.OrderNumber)}}<br/><span class="muted">Resi: {{Esc(o?.TrackingNumber)}}</span></td>
              </tr>
            </table>
            <table>
              <thead><tr><th>Deskripsi</th><th class="right">Jumlah</th></tr></thead>
              <tbody>
                <tr><td>Ongkos kirim {{Esc(o?.ServiceType)}} — {{Esc(o?.SenderCity)}} ke {{Esc(o?.RecipientCity)}}
                    ({{o?.WeightKg ?? 0}} kg)</td><td class="right">Rp {{invoice.SubTotal:N0}}</td></tr>
                <tr><td>Asuransi</td><td class="right">Rp {{invoice.InsuranceFee:N0}}</td></tr>
                <tr><td>PPN</td><td class="right">Rp {{invoice.TaxAmount:N0}}</td></tr>
                <tr class="total"><td>Total</td><td class="right">Rp {{invoice.TotalAmount:N0}}</td></tr>
              </tbody>
            </table>
            <p class="muted">Status pembayaran: <strong>{{Esc(invoice.Status)}}</strong></p>
            <p class="muted">Dokumen ini sah dan diproses komputer — tidak memerlukan tanda tangan.</p>
            </body></html>
            """;
    }
}

/// <summary>
/// Insurance service
/// </summary>
public class InsuranceService
{
    private readonly NgibridDbContext _db;
    public InsuranceService(NgibridDbContext db) { _db = db; }

    public async Task<InsuranceClaim> SubmitClaimAsync(long orderId, decimal amount, string reason, string? docUrl = null)
    {
        var claim = new InsuranceClaim
        {
            OrderId = orderId,
            ClaimNumber = $"CLM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            ClaimAmount = amount,
            ClaimReason = reason,
            SupportingDocumentUrl = docUrl,
            Status = "SUBMITTED"
        };
        _db.InsuranceClaims.Add(claim);
        await _db.SaveChangesAsync();
        return claim;
    }

    public async Task<List<InsuranceClaim>> GetClaimsAsync(string? status = null, int take = 100)
    {
        var query = _db.InsuranceClaims.Include(c => c.Order).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
        return await query.OrderByDescending(c => c.CreatedAt).Take(take).ToListAsync();
    }

    /// <summary>
    /// Approve or reject a claim. Approval is capped at the order's declared value so a
    /// claim can never pay out more than what was insured.
    /// </summary>
    public async Task<InsuranceClaim> ReviewClaimAsync(long claimId, bool approve, decimal? approvedAmount = null,
        string? rejectionReason = null)
    {
        var claim = await _db.InsuranceClaims.Include(c => c.Order)
            .FirstOrDefaultAsync(c => c.Id == claimId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found");

        claim.ReviewedAt = DateTime.UtcNow;

        if (approve)
        {
            var cap = claim.Order?.DeclaredValue ?? claim.ClaimAmount;
            claim.ApprovedAmount = Math.Min(approvedAmount ?? claim.ClaimAmount, cap);
            claim.Status = "APPROVED";
        }
        else
        {
            claim.Status = "REJECTED";
            claim.RejectionReason = rejectionReason ?? "Tidak memenuhi syarat klaim.";
        }

        await _db.SaveChangesAsync();
        return claim;
    }
}

/// <summary>
/// Green logistics & carbon tracking
/// </summary>
public class GreenLogisticsService
{
    private readonly IConfiguration _config;
    public GreenLogisticsService(IConfiguration config) { _config = config; }

    /// <summary>
    /// Estimate shipment emissions from the real distance between the two cities.
    /// Emission scales with weight share of a nominal 10 kg consignment; eco-delivery applies
    /// the configured cleaner-vehicle discount.
    /// </summary>
    public Task<double> EstimateEmissionAsync(Order order)
    {
        var emissionFactor = _config.GetValue<double>("GreenLogistics:EmissionFactorGramCo2PerKm", 150);
        var ecoDiscount = _config.GetValue<double>("GreenLogistics:EcoVehicleDiscount", 0.1);

        var from = CityCoordinates.Resolve(order.SenderProvince, order.SenderCity);
        var to = CityCoordinates.Resolve(order.RecipientProvince, order.RecipientCity);
        var distance = Math.Max(
            RouteOptimizationService.HaversineDistance(from.Lat, from.Lng, to.Lat, to.Lng) * 1.3, 10);

        var weightShare = Math.Max(order.WeightKg, 0.5) / 10.0;
        var emission = distance * emissionFactor * weightShare;

        if (order.IsEcoDelivery) emission *= 1 - ecoDiscount;

        return Task.FromResult(Math.Round(emission, 2));
    }

    public double CalculateCarbonOffset(double emissionGram, decimal pricePerKg = 500) =>
        (emissionGram / 1000.0) * (double)pricePerKg;
}
