using PadelHub.Data;
using PadelHub.Models;
using Microsoft.EntityFrameworkCore;

namespace PadelHub.Services;

/// <summary>
/// Service untuk mencatat audit log.
/// </summary>
public class AuditLogService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string userId, string action, string entityName, string? entityId = null, string? details = null)
    {
        var log = new AuditLog
        {
            UserId = userId, Action = action, EntityName = entityName,
            EntityId = entityId, Details = details,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public IQueryable<AuditLog> GetLogs(string? userId = null, string? action = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(userId)) query = query.Where(l => l.UserId == userId);
        if (!string.IsNullOrEmpty(action)) query = query.Where(l => l.Action == action);
        if (from.HasValue) query = query.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(l => l.CreatedAt <= to.Value);
        return query.OrderByDescending(l => l.CreatedAt);
    }
}

/// <summary>
/// Service untuk export data ke CSV dan Excel.
/// </summary>
public class ExportService
{
    public byte[] ExportToCsv<T>(IEnumerable<T> data)
    {
        using var writer = new StringWriter();
        using var csv = new CsvHelper.CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
        csv.WriteRecords(data);
        return System.Text.Encoding.UTF8.GetBytes(writer.ToString());
    }

    public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Data")
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        worksheet.Cell(1, 1).InsertTable(data);
        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

/// <summary>
/// Service untuk generate QR Code.
/// </summary>
public class QrCodeService
{
    public byte[] GenerateQrCode(string content, int pixelsPerModule = 20)
    {
        using var generator = new QRCoder.QRCodeGenerator();
        var qrCodeData = generator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    public string GenerateQrCodeBase64(string content, int pixelsPerModule = 20)
    {
        var bytes = GenerateQrCode(content, pixelsPerModule);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}

/// <summary>
/// Service IoT Simulator.
/// </summary>
public class IoTSimulatorService
{
    private readonly AppDbContext _db;
    private readonly Dictionary<int, CancellationTokenSource> _runningSimulators = new();

    public IoTSimulatorService(AppDbContext db) => _db = db;

    public bool IsRunning(int simulatorId) => _runningSimulators.ContainsKey(simulatorId);

    public async Task StartAsync(int simulatorId)
    {
        if (_runningSimulators.ContainsKey(simulatorId)) return;
        var simulator = await _db.IoTSimulators.FindAsync(simulatorId);
        if (simulator == null) return;
        var cts = new CancellationTokenSource();
        _runningSimulators[simulatorId] = cts;
        simulator.IsRunning = true; simulator.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var sd = new SensorData { CourtId = 1, SensorType = simulator.SensorType,
                        Value = GenerateSensorValue(simulator.SensorType), RecordedAt = DateTime.UtcNow };
                    _db.SensorData.Add(sd); await _db.SaveChangesAsync(cts.Token);
                    await Task.Delay(simulator.IntervalMs, cts.Token);
                }
                catch (TaskCanceledException) { break; }
            }
        }, cts.Token);
    }

    public async Task StopAsync(int simulatorId)
    {
        if (!_runningSimulators.TryGetValue(simulatorId, out var cts)) return;
        cts.Cancel(); _runningSimulators.Remove(simulatorId);
        var simulator = await _db.IoTSimulators.FindAsync(simulatorId);
        if (simulator != null) { simulator.IsRunning = false; simulator.StoppedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    private string GenerateSensorValue(string sensorType) => sensorType switch
    {
        "Temperature" => "{\"value\": " + (20 + Random.Shared.NextDouble() * 15).ToString("F1") + ", \"unit\": \"celsius\"}",
        "Humidity" => "{\"value\": " + (40 + Random.Shared.NextDouble() * 40).ToString("F1") + ", \"unit\": \"percent\"}",
        "Lighting" => "{\"value\": " + Random.Shared.Next(0, 100) + ", \"unit\": \"lux\"}",
        "BallTracking" => "{\"x\": " + Random.Shared.Next(0, 100) + ", \"y\": " + Random.Shared.Next(0, 100) + "}",
        _ => "{\"value\": " + Random.Shared.Next(0, 100) + "}"
    };
}

/// <summary>
/// Service untuk ranking.
/// </summary>
public class RankingService
{
    private readonly AppDbContext _db;
    public RankingService(AppDbContext db) => _db = db;

    public async Task RecalculateRankingsAsync()
    {
        var players = _db.PlayerProfiles.ToList();
        var sorted = players.OrderByDescending(p => p.Rating).ToList();
        for (int i = 0; i < sorted.Count; i++) { sorted[i].Ranking = i + 1; sorted[i].UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Service pembayaran.
/// </summary>
public class PaymentService
{
    private readonly AppDbContext _db;
    public PaymentService(AppDbContext db) => _db = db;

    public async Task<Payment> ProcessPaymentAsync(int reservationId, string paymentMethod, decimal amount, string userId)
    {
        var payment = new Payment
        {
            ReservationId = reservationId, UserId = userId, Amount = amount,
            PaymentMethod = paymentMethod, Status = "Success",
            TransactionId = "TXN-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString()[..8].ToUpper(),
            CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);
        var reservation = await _db.Reservations.FindAsync(reservationId);
        if (reservation != null) reservation.Status = "Confirmed";
        await _db.SaveChangesAsync();
        return payment;
    }
}

/// <summary>
/// Service notifikasi.
/// </summary>
public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    public NotificationService(ILogger<NotificationService> logger) => _logger = logger;
    public Task SendNotificationAsync(string userId, string title, string message, string type = "Info")
    {
        _logger.LogInformation("Notification to {UserId}: [{Type}] {Title} - {Message}", userId, type, title, message);
        return Task.CompletedTask;
    }
}
