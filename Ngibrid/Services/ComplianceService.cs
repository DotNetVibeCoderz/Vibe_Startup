using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Regulatory compliance: Indonesian VAT (PPN) records, customs declarations (bea cukai),
/// and the export/import document set that travels with a cross-border shipment.
/// </summary>
public class ComplianceService
{
    private readonly NgibridDbContext _db;
    private readonly IConfiguration _config;
    private readonly AuditService _audit;

    public ComplianceService(NgibridDbContext db, IConfiguration config, AuditService audit)
    { _db = db; _config = config; _audit = audit; }

    // ─── Tax ───

    /// <summary>
    /// Record VAT for an order. Idempotent per order + tax type so re-running invoicing
    /// doesn't double-count a reporting period.
    /// </summary>
    public async Task<TaxRecord> RecordTaxAsync(long orderId, string taxType = "PPN", string? taxpayerId = null)
    {
        var order = await _db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        var existing = await _db.TaxRecords
            .FirstOrDefaultAsync(t => t.OrderId == orderId && t.TaxType == taxType);
        if (existing != null) return existing;

        var rate = _config.GetValue<decimal>("Shipment:TaxRate", 0.11m);
        var taxable = order.BasePrice + order.InsuranceFee;

        var record = new TaxRecord
        {
            OrderId = orderId,
            TaxNumber = $"TAX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            TaxType = taxType,
            TaxpayerId = taxpayerId,
            TaxableAmount = taxable,
            TaxRate = rate,
            TaxAmount = Math.Round(taxable * rate, 0),
            Currency = order.Currency,
            Period = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = "RECORDED"
        };

        _db.TaxRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<List<TaxRecord>> GetTaxRecordsAsync(DateTime? period = null, int take = 200)
    {
        var query = _db.TaxRecords.Include(t => t.Order).AsQueryable();
        if (period.HasValue)
        {
            var start = new DateTime(period.Value.Year, period.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            query = query.Where(t => t.Period == start);
        }
        return await query.OrderByDescending(t => t.CreatedAt).Take(take).ToListAsync();
    }

    /// <summary>Monthly VAT summary for filing.</summary>
    public async Task<List<TaxPeriodSummary>> GetTaxSummaryAsync(int months = 12)
    {
        var since = DateTime.UtcNow.AddMonths(-months);
        var records = await _db.TaxRecords.Where(t => t.Period >= since).ToListAsync();

        return records
            .GroupBy(t => new { t.Period, t.TaxType })
            .Select(g => new TaxPeriodSummary
            {
                Period = g.Key.Period,
                TaxType = g.Key.TaxType,
                RecordCount = g.Count(),
                TaxableAmount = g.Sum(t => t.TaxableAmount),
                TaxAmount = g.Sum(t => t.TaxAmount),
                ReportedCount = g.Count(t => t.Status != "RECORDED")
            })
            .OrderByDescending(s => s.Period)
            .ToList();
    }

    public async Task<int> MarkPeriodReportedAsync(DateTime period, string taxType = "PPN")
    {
        var start = new DateTime(period.Year, period.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var records = await _db.TaxRecords
            .Where(t => t.Period == start && t.TaxType == taxType && t.Status == "RECORDED")
            .ToListAsync();

        foreach (var r in records)
        {
            r.Status = "REPORTED";
            r.ReportedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync("REPORT_TAX", "TaxRecord", null,
            notes: $"{records.Count} {taxType} records reported for {start:MMMM yyyy}");
        return records.Count;
    }

    // ─── Customs ───

    /// <summary>
    /// Create a customs declaration and the standard document set for a cross-border order.
    /// Duty and VAT follow the de-minimis rule: below the threshold only VAT applies.
    /// </summary>
    public async Task<CustomsDeclaration> CreateDeclarationAsync(long orderId, string declarationType,
        string destinationCountry, decimal declaredValue, string currency = "USD",
        string? hsCode = null, string incoterm = "DAP")
    {
        var order = await _db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        var dutyRate = _config.GetValue<decimal>("Compliance:Customs:DutyRate", 0.075m);
        var vatRate = _config.GetValue<decimal>("Compliance:Customs:ImportVatRate", 0.11m);
        var deMinimis = _config.GetValue<decimal>("Compliance:Customs:DeMinimisUsd", 3m);

        var duty = declaredValue > deMinimis ? Math.Round(declaredValue * dutyRate, 2) : 0m;
        var vat = Math.Round((declaredValue + duty) * vatRate, 2);

        var declaration = new CustomsDeclaration
        {
            OrderId = orderId,
            DeclarationNumber = $"CUS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            DeclarationType = declarationType.ToUpperInvariant(),
            OriginCountry = declarationType.Equals("EXPORT", StringComparison.OrdinalIgnoreCase) ? "ID" : destinationCountry,
            DestinationCountry = declarationType.Equals("EXPORT", StringComparison.OrdinalIgnoreCase) ? destinationCountry : "ID",
            HsCode = hsCode,
            GoodsDescription = order.PackageDescription,
            DeclaredValue = declaredValue,
            Currency = currency,
            DutyAmount = duty,
            VatAmount = vat,
            Incoterm = incoterm,
            Status = "DRAFT"
        };

        _db.CustomsDeclarations.Add(declaration);
        await _db.SaveChangesAsync();

        foreach (var docType in RequiredDocuments(declaration.DeclarationType))
        {
            _db.ComplianceDocuments.Add(new ComplianceDocument
            {
                CustomsDeclarationId = declaration.Id,
                DocumentType = docType,
                DocumentNumber = $"{DocPrefix(docType)}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpper()}",
                Status = "ISSUED"
            });
        }
        await _db.SaveChangesAsync();

        await _audit.LogAsync("CREATE_DECLARATION", "CustomsDeclaration", declaration.Id,
            notes: $"{declaration.DeclarationType} to {destinationCountry}, declared {currency} {declaredValue:N2}");

        return declaration;
    }

    public static string[] RequiredDocuments(string declarationType) =>
        declarationType.Equals("EXPORT", StringComparison.OrdinalIgnoreCase)
            ? new[] { "COMMERCIAL_INVOICE", "PACKING_LIST", "CERTIFICATE_OF_ORIGIN", "AWB", "EXPORT_PERMIT" }
            : new[] { "COMMERCIAL_INVOICE", "PACKING_LIST", "AWB", "IMPORT_PERMIT" };

    private static string DocPrefix(string docType) => docType switch
    {
        "COMMERCIAL_INVOICE" => "CI",
        "PACKING_LIST" => "PL",
        "CERTIFICATE_OF_ORIGIN" => "COO",
        "AWB" => "AWB",
        "EXPORT_PERMIT" => "PEB",   // Pemberitahuan Ekspor Barang
        "IMPORT_PERMIT" => "PIB",   // Pemberitahuan Impor Barang
        _ => "DOC"
    };

    public async Task<List<CustomsDeclaration>> GetDeclarationsAsync(string? status = null, int take = 100)
    {
        var query = _db.CustomsDeclarations
            .Include(c => c.Documents)
            .Include(c => c.Order)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
        return await query.OrderByDescending(c => c.CreatedAt).Take(take).ToListAsync();
    }

    public async Task<CustomsDeclaration?> GetDeclarationAsync(long id) =>
        await _db.CustomsDeclarations.Include(c => c.Documents).Include(c => c.Order)
            .FirstOrDefaultAsync(c => c.Id == id);

    /// <summary>
    /// Advance a declaration through DRAFT → SUBMITTED → CLEARED. Submitting requires every
    /// mandatory document to be present, which is the check that actually blocks a bad filing.
    /// </summary>
    public async Task<(bool Ok, string Message)> AdvanceDeclarationAsync(long declarationId, string newStatus)
    {
        var declaration = await _db.CustomsDeclarations.Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == declarationId);
        if (declaration == null) return (false, "Declaration not found");

        if (newStatus.Equals("SUBMITTED", StringComparison.OrdinalIgnoreCase))
        {
            var required = RequiredDocuments(declaration.DeclarationType);
            var present = declaration.Documents?.Select(d => d.DocumentType).ToHashSet() ?? new HashSet<string>();
            var missing = required.Where(r => !present.Contains(r)).ToList();
            if (missing.Count > 0)
                return (false, $"Dokumen belum lengkap: {string.Join(", ", missing)}");

            declaration.SubmittedAt = DateTime.UtcNow;
        }

        if (newStatus.Equals("CLEARED", StringComparison.OrdinalIgnoreCase))
        {
            if (declaration.Status != "SUBMITTED")
                return (false, "Deklarasi harus di-submit sebelum bisa clear.");
            declaration.ClearedAt = DateTime.UtcNow;
        }

        declaration.Status = newStatus.ToUpperInvariant();
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UPDATE_DECLARATION", "CustomsDeclaration", declarationId,
            "Status", null, declaration.Status);
        return (true, $"Status deklarasi: {declaration.Status}");
    }

    public async Task<ComplianceDocument> AddDocumentAsync(long declarationId, string documentType,
        string? fileUrl = null)
    {
        var doc = new ComplianceDocument
        {
            CustomsDeclarationId = declarationId,
            DocumentType = documentType.ToUpperInvariant(),
            DocumentNumber = $"{DocPrefix(documentType.ToUpperInvariant())}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpper()}",
            FileUrl = fileUrl,
            Status = "ISSUED"
        };
        _db.ComplianceDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    public class TaxPeriodSummary
    {
        public DateTime Period { get; set; }
        public string TaxType { get; set; } = "PPN";
        public int RecordCount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public int ReportedCount { get; set; }
    }
}

/// <summary>
/// Customer loyalty program — points earned per completed delivery, redeemable against shipping cost.
/// </summary>
public class LoyaltyService
{
    private readonly NgibridDbContext _db;
    private readonly IConfiguration _config;
    private readonly NotificationService _notifications;

    public LoyaltyService(NgibridDbContext db, IConfiguration config, NotificationService notifications)
    { _db = db; _config = config; _notifications = notifications; }

    /// <summary>Tier thresholds, ascending. Tier drives the earn multiplier.</summary>
    public static readonly (string Name, int MinPoints, double Multiplier, string Icon)[] Tiers =
    {
        ("Bronze", 0, 1.0, "🥉"),
        ("Silver", 1000, 1.25, "🥈"),
        ("Gold", 5000, 1.5, "🥇"),
        ("Platinum", 15000, 2.0, "💎")
    };

    public static (string Name, int MinPoints, double Multiplier, string Icon) GetTier(int points) =>
        Tiers.Last(t => points >= t.MinPoints);

    public static (string Name, int MinPoints, double Multiplier, string Icon)? GetNextTier(int points) =>
        Tiers.FirstOrDefault(t => t.MinPoints > points) is { MinPoints: > 0 } next ? next : null;

    /// <summary>
    /// Award points for a delivered order. Idempotent per order — a redelivery or a repeated
    /// status webhook won't grant points twice.
    /// </summary>
    public async Task<LoyaltyTransaction?> EarnForOrderAsync(long orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return null;

        var already = await _db.LoyaltyTransactions
            .AnyAsync(t => t.OrderId == orderId && t.TransactionType == "EARN");
        if (already) return null;

        var user = await _db.Users.FindAsync(order.CustomerId);
        if (user == null) return null;

        var perRupiah = _config.GetValue<decimal>("Loyalty:PointsPerRupiah", 0.0001m); // 10k IDR => 1 pt
        var tier = GetTier(user.LoyaltyPoints);
        var basePoints = (int)Math.Floor(order.TotalAmount * perRupiah);
        var points = Math.Max((int)Math.Round(basePoints * tier.Multiplier), 1);

        user.LoyaltyPoints += points;

        var tx = new LoyaltyTransaction
        {
            UserId = user.Id,
            OrderId = orderId,
            TransactionType = "EARN",
            Points = points,
            BalanceAfter = user.LoyaltyPoints,
            Description = $"Poin dari pengiriman {order.OrderNumber} (tier {tier.Name} ×{tier.Multiplier})",
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };
        _db.LoyaltyTransactions.Add(tx);
        await _db.SaveChangesAsync();

        var newTier = GetTier(user.LoyaltyPoints);
        if (newTier.Name != tier.Name)
        {
            await _notifications.SendAsync(user.Id, $"{newTier.Icon} Naik tier {newTier.Name}!",
                $"Selamat! Anda naik ke tier {newTier.Name} dan kini mendapat {newTier.Multiplier}× poin setiap pengiriman.",
                "SUCCESS", "/profile");
        }

        return tx;
    }

    /// <summary>
    /// Redeem points for a shipping discount. Fails when the balance is short rather than
    /// letting the balance go negative.
    /// </summary>
    public async Task<(bool Ok, string Message, decimal Discount)> RedeemAsync(long userId, int points)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User tidak ditemukan.", 0);
        if (points <= 0) return (false, "Jumlah poin tidak valid.", 0);
        if (user.LoyaltyPoints < points)
            return (false, $"Poin tidak cukup. Saldo Anda {user.LoyaltyPoints} poin.", 0);

        var rupiahPerPoint = _config.GetValue<decimal>("Loyalty:RupiahPerPoint", 100m);
        var discount = points * rupiahPerPoint;

        user.LoyaltyPoints -= points;
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            UserId = userId,
            TransactionType = "REDEEM",
            Points = -points,
            BalanceAfter = user.LoyaltyPoints,
            Description = $"Tukar {points} poin menjadi diskon Rp {discount:N0}"
        });
        await _db.SaveChangesAsync();

        return (true, $"Berhasil menukar {points} poin menjadi diskon Rp {discount:N0}.", discount);
    }

    public async Task<List<LoyaltyTransaction>> GetHistoryAsync(long userId, int take = 50) =>
        await _db.LoyaltyTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task<int> GetBalanceAsync(long userId) =>
        (await _db.Users.FindAsync(userId))?.LoyaltyPoints ?? 0;
}
