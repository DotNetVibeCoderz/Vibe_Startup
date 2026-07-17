using WashUp.Data;
using WashUp.Models;
using Microsoft.EntityFrameworkCore;

namespace WashUp.Services;

/// <summary>
/// Simulator GPS kurir. Berjalan di thread terpisah dan bisa start/stop.
///
/// Cara kerja per tick (3 detik):
/// 1. Tugas "Assigned" diinisialisasi: posisi awal = lokasi cabang order
///    (atau dekat tujuan bila cabang tanpa koordinat), status → InTransit.
/// 2. Tugas "InTransit" bergerak menuju tujuan dengan kecepatan realistis
///    (18–45 km/j, dihitung haversine), plus sedikit jitter agar jalurnya
///    tidak lurus sempurna. ETA dihitung ulang dari sisa jarak.
/// 3. Sampai dalam radius ~60 m → status Arrived.
/// 4. 45 detik setelah Arrived → status Completed; order Delivery otomatis
///    ditandai "Dikirim".
/// </summary>
public class GpsSimulatorService
{
    private const double TickSeconds = 3.0;
    private const double ArriveRadiusKm = 0.06;
    private static readonly TimeSpan CompleteAfterArrival = TimeSpan.FromSeconds(45);

    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly object _lock = new();

    public bool IsRunning => _isRunning;

    public GpsSimulatorService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _cts = new CancellationTokenSource();
            _isRunning = true;
        }

        await Task.Run(async () =>
        {
            var random = new Random();
            while (!_cts!.Token.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var activeAssignments = await db.CourierAssignments
                        .Include(c => c.Order).ThenInclude(o => o!.Branch)
                        .Where(c => c.Status == "InTransit" || c.Status == "Assigned" || c.Status == "Arrived")
                        .ToListAsync();

                    foreach (var assignment in activeAssignments)
                    {
                        switch (assignment.Status)
                        {
                            case "Assigned":
                                InitializePosition(assignment, random);
                                break;

                            case "InTransit" when assignment.CurrentLatitude.HasValue && assignment.DestinationLatitude.HasValue:
                                MoveTowardDestination(db, assignment, random);
                                break;

                            case "Arrived":
                                if (assignment.CompletedAt.HasValue && DateTime.UtcNow - assignment.CompletedAt.Value >= CompleteAfterArrival)
                                {
                                    assignment.Status = "Completed";
                                    // Order delivery otomatis dianggap terkirim
                                    if (assignment.AssignmentType == "Delivery" && assignment.Order != null && assignment.Order.Status != "Dikirim")
                                    {
                                        assignment.Order.Status = "Dikirim";
                                        assignment.Order.DeliveredAt = DateTime.UtcNow;
                                        assignment.Order.UpdatedAt = DateTime.UtcNow;
                                        db.OrderStatusLogs.Add(new OrderStatusLog
                                        {
                                            OrderId = assignment.Order.Id,
                                            OldStatus = "Selesai",
                                            NewStatus = "Dikirim",
                                            Notes = "Otomatis: kurir tiba di tujuan (GPS simulator)",
                                            ChangedAt = DateTime.UtcNow
                                        });
                                    }
                                }
                                break;
                        }
                    }

                    await db.SaveChangesAsync();

                    // Jaga tabel log tetap ringan: buang titik GPS lebih tua dari 1 jam
                    var cutoff = DateTime.UtcNow.AddHours(-1);
                    await db.GpsTrackingLogs.Where(l => l.Timestamp < cutoff).ExecuteDeleteAsync();

                    await Task.Delay(TimeSpan.FromSeconds(TickSeconds), _cts.Token);
                }
                catch (TaskCanceledException) { break; }
                catch { /* Log and continue */ }
            }
        }, _cts.Token);
    }

    private static void InitializePosition(CourierAssignment assignment, Random random)
    {
        if (!assignment.DestinationLatitude.HasValue) return;

        // Berangkat dari cabang; kalau cabang tak punya koordinat, mulai ±2 km dari tujuan
        var branch = assignment.Order?.Branch;
        if (branch?.Latitude != null && branch.Longitude != null)
        {
            assignment.CurrentLatitude = branch.Latitude + (random.NextDouble() - 0.5) * 0.002;
            assignment.CurrentLongitude = branch.Longitude + (random.NextDouble() - 0.5) * 0.002;
        }
        else
        {
            assignment.CurrentLatitude = assignment.DestinationLatitude - 0.01 - random.NextDouble() * 0.01;
            assignment.CurrentLongitude = assignment.DestinationLongitude - 0.01 - random.NextDouble() * 0.01;
        }

        assignment.Status = "InTransit";
        assignment.StartedAt = DateTime.UtcNow;

        var distanceKm = HaversineKm(
            assignment.CurrentLatitude!.Value, assignment.CurrentLongitude!.Value,
            assignment.DestinationLatitude.Value, assignment.DestinationLongitude!.Value);
        assignment.EstimatedArrival = DateTime.UtcNow.AddHours(distanceKm / 28.0); // asumsi rata-rata 28 km/j
    }

    private static void MoveTowardDestination(AppDbContext db, CourierAssignment assignment, Random random)
    {
        var lat = assignment.CurrentLatitude!.Value;
        var lng = assignment.CurrentLongitude!.Value;
        var dLat = assignment.DestinationLatitude!.Value;
        var dLng = assignment.DestinationLongitude!.Value;

        var distanceKm = HaversineKm(lat, lng, dLat, dLng);
        var speedKmh = 18 + random.NextDouble() * 27; // 18–45 km/j (lalu lintas kota)
        var stepKm = speedKmh * TickSeconds / 3600.0;

        if (distanceKm <= Math.Max(stepKm, ArriveRadiusKm))
        {
            // Tiba di tujuan
            assignment.CurrentLatitude = dLat;
            assignment.CurrentLongitude = dLng;
            assignment.Status = "Arrived";
            assignment.CompletedAt = DateTime.UtcNow;
            assignment.EstimatedArrival = DateTime.UtcNow;
            speedKmh = 0;
        }
        else
        {
            // Maju sepanjang garis lurus ke tujuan + jitter kecil (belokan jalan)
            var fraction = stepKm / distanceKm;
            assignment.CurrentLatitude = lat + (dLat - lat) * fraction + (random.NextDouble() - 0.5) * 0.0004;
            assignment.CurrentLongitude = lng + (dLng - lng) * fraction + (random.NextDouble() - 0.5) * 0.0004;

            var remainingKm = HaversineKm(assignment.CurrentLatitude.Value, assignment.CurrentLongitude.Value, dLat, dLng);
            assignment.EstimatedArrival = DateTime.UtcNow.AddHours(remainingKm / Math.Max(speedKmh, 5));
        }

        db.GpsTrackingLogs.Add(new GpsTrackingLog
        {
            CourierAssignmentId = assignment.Id,
            Latitude = assignment.CurrentLatitude!.Value,
            Longitude = assignment.CurrentLongitude!.Value,
            SpeedKmh = Math.Round(speedKmh, 1),
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>Jarak lingkaran besar antara dua koordinat dalam kilometer.</summary>
    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            _isRunning = false;
        }
    }

    public async Task ToggleAsync()
    {
        if (_isRunning)
            Stop();
        else
            await StartAsync();
    }
}
