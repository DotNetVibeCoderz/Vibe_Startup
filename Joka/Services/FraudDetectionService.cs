// Rule-based fraud scoring, run when a transaction is created.
// Before this, FraudAlert rows only ever came from the seeder.
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Backoffice;
using Joka.Models.Payments;

namespace Joka.Services;

public class FraudDetectionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<FraudDetectionService> _logger;

    public FraudDetectionService(AppDbContext db, ILogger<FraudDetectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private record RuleHit(string Rule, string Reason, int Score);

    /// <summary>
    /// Scores a transaction and records an alert when it crosses the threshold.
    /// Never throws into the payment path - a scoring failure must not lose a
    /// customer's payment.
    /// </summary>
    public async Task<FraudAlert?> ScreenAsync(PaymentTransaction tx, string? userEmail)
    {
        try
        {
            var hits = new List<RuleHit>();
            var since = DateTime.UtcNow.AddMinutes(-10);

            // Velocity: several payments from one account in a short window.
            if (tx.UserId != Guid.Empty)
            {
                var recent = await _db.PaymentTransactions
                    .CountAsync(t => t.UserId == tx.UserId && t.CreatedAt >= since && t.Id != tx.Id);

                if (recent >= 4)
                    hits.Add(new("VelocityCheck", $"{recent + 1} transaksi dalam 10 menit dari satu akun", 45));
                else if (recent >= 2)
                    hits.Add(new("VelocityCheck", $"{recent + 1} transaksi dalam 10 menit dari satu akun", 20));
            }

            // Amount anomaly against the account's own history.
            if (tx.UserId != Guid.Empty)
            {
                var history = await _db.PaymentTransactions
                    .Where(t => t.UserId == tx.UserId && t.Id != tx.Id)
                    .Select(t => t.FinalAmount)
                    .ToListAsync();

                if (history.Count >= 2)
                {
                    var average = history.Average();
                    if (average > 0 && tx.FinalAmount > average * 5)
                        hits.Add(new("AmountAnomaly",
                            $"Nominal {tx.FinalAmount / average:0.#}x lebih tinggi dari rata-rata akun", 35));
                }
            }

            // Large single payment - worth a look regardless of history.
            if (tx.FinalAmount >= 25_000_000m)
                hits.Add(new("HighValue", $"Transaksi bernilai {tx.FinalAmount:N0}", 30));
            else if (tx.FinalAmount >= 10_000_000m)
                hits.Add(new("HighValue", $"Transaksi bernilai {tx.FinalAmount:N0}", 15));

            // An account already blocked should not be transacting at all.
            if (tx.UserId != Guid.Empty)
            {
                var blocked = await _db.Users.IgnoreQueryFilters()
                    .AnyAsync(u => u.Id == tx.UserId && u.IsBlocked);

                if (blocked)
                    hits.Add(new("BlockedAccount", "Transaksi dari akun yang sedang diblokir", 60));
            }

            // A discount that eats most of the order suggests voucher abuse.
            if (tx.DiscountAmount is decimal discount && tx.Amount > 0 && discount / tx.Amount >= 0.7m)
                hits.Add(new("VoucherAbuse",
                    $"Diskon menutup {discount / tx.Amount:P0} dari nilai pesanan", 25));

            if (hits.Count == 0) return null;

            var score = Math.Min(100, hits.Sum(h => h.Score));

            // Below this the signal is too weak to be worth an operator's time.
            if (score < 30) return null;

            var alert = new FraudAlert
            {
                TransactionCode = tx.TransactionCode,
                UserId = tx.UserId == Guid.Empty ? null : tx.UserId,
                UserEmail = userEmail,
                Rule = string.Join(" + ", hits.Select(h => h.Rule)),
                Reason = string.Join("; ", hits.Select(h => h.Reason)),
                RiskScore = score,
                Amount = tx.FinalAmount,
                Severity = score switch
                {
                    >= 80 => "Critical",
                    >= 60 => "High",
                    >= 40 => "Medium",
                    _ => "Low"
                },
                Status = "Open"
            };

            _db.FraudAlerts.Add(alert);
            await _db.SaveChangesAsync();

            _logger.LogWarning("Fraud alert {Code} score {Score}: {Reason}",
                tx.TransactionCode, score, alert.Reason);

            return alert;
        }
        catch (Exception ex)
        {
            // Screening is advisory. Losing it must not fail the payment.
            _logger.LogError(ex, "Fraud screening gagal untuk {Code}", tx.TransactionCode);
            return null;
        }
    }
}
