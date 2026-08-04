using System.Collections.Concurrent;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Simulator;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

// ══════════════════════════════════════════════════════════════════════
// FastRide simulator
//
// Drives the real API end to end: riders quote a fare, book, wait for a
// driver, get picked up, ride, pay and review; drivers come online, push
// GPS, take offers and work the trip through its lifecycle.
//
// Every actor authenticates as itself, so this exercises the same
// authorization the mobile apps hit.
// ══════════════════════════════════════════════════════════════════════

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var options = SimulationOptions.Load(config, args);
var random = new Random(options.RandomSeed);
var metrics = new Metrics();

AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold]FastRide Simulator[/]").RuleStyle("orange1").Centered());
AnsiConsole.MarkupLine($"  API      : [grey]{options.BaseUrl.EscapeMarkup()}[/]");
AnsiConsole.MarkupLine($"  Aktor    : [cyan]{options.RiderCount}[/] penumpang · [green]{options.DriverCount}[/] driver");
AnsiConsole.MarkupLine($"  Durasi   : [grey]{(options.DurationSeconds > 0 ? $"{options.DurationSeconds} detik" : "sampai ditekan S")}[/]");
AnsiConsole.WriteLine();

using var cts = new CancellationTokenSource();
var pause = new ManualResetEventSlim(true);
var paused = false;

var riders = new List<RiderActor>();
var drivers = new List<DriverActor>();
var board = new ConcurrentDictionary<Guid, BoardEntry>();

// ─────────────────────────── bootstrap ───────────────────────────

var admin = SimulatorClient.Create(options.BaseUrl, metrics);

var health = await AnsiConsole.Status().StartAsync("Menghubungi API…", async _ => await admin.HealthAsync(cts.Token));
if (health is null)
{
    AnsiConsole.MarkupLine("[red]Tidak bisa menghubungi API.[/] Jalankan dulu:");
    AnsiConsole.MarkupLine("  [grey]dotnet run --project FastRide.Api[/]");
    return 1;
}

AnsiConsole.MarkupLine($"  [green]✓[/] API sehat — database {health.Database}, storage {health.StorageProvider}, cache {health.Cache}");

if (!await admin.LoginAsync(options.AdminEmail, options.AdminPassword, cts.Token))
{
    AnsiConsole.MarkupLine($"[red]Gagal masuk sebagai admin ({options.AdminEmail.EscapeMarkup()}).[/]");
    AnsiConsole.MarkupLine("  [grey]Simulator butuh akun admin untuk menyetujui dokumen driver.[/]");
    return 1;
}

AnsiConsole.MarkupLine("  [green]✓[/] Masuk sebagai admin");

var stamp = DateTime.UtcNow.ToString("HHmmss");

await AnsiConsole.Status().StartAsync("Menyiapkan aktor…", async ctx =>
{
    for (var i = 0; i < options.RiderCount; i++)
    {
        ctx.Status($"Mendaftarkan penumpang {i + 1}/{options.RiderCount}…");

        var client = SimulatorClient.Create(options.BaseUrl, metrics);
        var name = $"SimRider {stamp}-{i + 1:00}";
        var email = $"simrider.{stamp}.{i + 1:00}@sim.fastride";

        // Reuses an account from an earlier run, and waits out the auth rate limit.
        var ready = await client.SignUpOrSignInAsync(
            new RegisterRequest(name, email, $"0811{random.Next(1000000, 9999999)}", options.SimPassword, UserRole.Rider),
            cts.Token);

        if (!ready) continue;

        riders.Add(new RiderActor(client, random.Next()));
    }

    for (var i = 0; i < options.DriverCount; i++)
    {
        ctx.Status($"Menyiapkan driver {i + 1}/{options.DriverCount}…");

        var client = SimulatorClient.Create(options.BaseUrl, metrics);
        var name = $"SimDriver {stamp}-{i + 1:00}";
        var email = $"simdriver.{stamp}.{i + 1:00}@sim.fastride";
        var category = (VehicleCategory)random.Next(1, 6);

        var ready = await client.SignUpOrSignInAsync(
            new RegisterRequest(
                name, email, $"0812{random.Next(1000000, 9999999)}", options.SimPassword, UserRole.Driver,
                $"SIM-{random.Next(100000, 999999)}", "Toyota Avanza",
                $"B {random.Next(1000, 9999)} SIM", category),
            cts.Token);

        if (!ready) continue;

        // A new driver is unverified and cannot go online, so the simulator walks the real
        // verification flow: upload the three required documents, then approve them as admin.
        foreach (var type in new[] { DocumentType.DriverLicense, DocumentType.VehicleRegistration, DocumentType.IdentityCard })
        {
            var document = await client.UploadDocumentAsync(type, cts.Token);
            if (document is not null) await admin.ApproveDocumentAsync(client.UserId, document.Id, cts.Token);
        }

        var latitude = RandomLatitude(random);
        var longitude = RandomLongitude(random);

        await client.UpdateLocationAsync(latitude, longitude, random.Next(0, 360), cts.Token);
        await client.SetStatusAsync(DriverStatus.Online, cts.Token);

        drivers.Add(new DriverActor(client, latitude, longitude, random.Next()));
    }
});

if (riders.Count == 0 || drivers.Count == 0)
{
    AnsiConsole.MarkupLine($"[red]Aktor tidak lengkap[/] — {riders.Count} penumpang, {drivers.Count} driver.");
    AnsiConsole.MarkupLine("  [grey]Biasanya karena batas laju endpoint auth. Kurangi jumlah aktor, atau naikkan[/]");
    AnsiConsole.MarkupLine("  [grey]RateLimiting:AuthPermitPerMinute di FastRide.Api/appsettings.json.[/]");
    return 1;
}

AnsiConsole.MarkupLine($"  [green]✓[/] {riders.Count} penumpang, {drivers.Count} driver siap (dokumen disetujui)");
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("  [bold yellow]S[/] berhenti   [bold cyan]P[/] jeda/lanjut");
AnsiConsole.WriteLine();

var startedAt = DateTime.UtcNow;

// ─────────────────────────── actor loops ───────────────────────────

var riderTasks = riders.Select(RideLoop).ToList();
var driverTasks = drivers.Select(DriveLoop).ToList();

// Keyboard control runs on its own thread so a blocking read never stalls the actors.
// Console.KeyAvailable throws when stdin is redirected, which is exactly what happens when
// the simulator is piped or run from CI — so only start the reader on a real console.
if (!Console.IsInputRedirected)
{
    var keyboard = new Thread(() =>
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(80);
                    continue;
                }

                switch (char.ToUpperInvariant(Console.ReadKey(intercept: true).KeyChar))
                {
                    case 'S':
                        pause.Set();
                        cts.Cancel();
                        break;

                    case 'P':
                        paused = !paused;
                        if (paused) pause.Reset(); else pause.Set();
                        break;
                }
            }
        }
        catch (InvalidOperationException)
        {
            // No console attached after all; the run continues without keyboard control.
        }
    }) { IsBackground = true };

    keyboard.Start();
}
else if (options.DurationSeconds == 0)
{
    AnsiConsole.MarkupLine("[yellow]Input keyboard tidak tersedia — gunakan --duration agar simulasi berhenti sendiri.[/]");
}

// The duration cap is honoured now; it used to be present in appsettings and ignored.
if (options.DurationSeconds > 0) cts.CancelAfter(TimeSpan.FromSeconds(options.DurationSeconds));

await RenderLiveAsync();

cts.Cancel();
pause.Set();

try
{
    await Task.WhenAll(riderTasks.Concat(driverTasks));
}
catch (OperationCanceledException)
{
    // Expected on stop.
}

RenderSummary();
return 0;

// ─────────────────────────── rider behaviour ───────────────────────────

async Task RideLoop(RiderActor rider)
{
    var local = new Random(rider.Seed);

    while (!cts.IsCancellationRequested)
    {
        try
        {
            pause.Wait(cts.Token);
            await Task.Delay(local.Next(700, 3200), cts.Token);

            var pickupLat = RandomLatitude(local);
            var pickupLon = RandomLongitude(local);
            var dropLat = RandomLatitude(local);
            var dropLon = RandomLongitude(local);
            var category = (VehicleCategory)local.Next(1, 6);

            // Riders check the price before committing, exactly like the app does.
            var quote = await rider.Client.QuoteAsync(
                new FareQuoteRequest(pickupLat, pickupLon, dropLat, dropLon, category), cts.Token);

            if (quote is null) continue;

            var order = await rider.Client.BookAsync(new CreateOrderRequest(
                rider.Client.UserId,
                pickupLat, pickupLon, RandomAddress(local),
                dropLat, dropLon, RandomAddress(local),
                category,
                PickPaymentMethod(local)), cts.Token);

            if (order is null) continue;

            metrics.OrderCreated();
            board[order.Id] = new BoardEntry(order.Code, rider.Client.FullName, null, order.FinalFare, OrderStatus.Requested, DateTime.UtcNow);

            var abandon = local.NextDouble() < options.CancelRate;
            await FollowOrderAsync(rider, order.Id, abandon, local);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
}

/// <summary>Poll a booking until it reaches a terminal state, cancelling it if this rider is impatient.</summary>
async Task FollowOrderAsync(RiderActor rider, Guid orderId, bool abandon, Random local)
{
    var deadline = DateTime.UtcNow.AddMinutes(3);

    while (!cts.IsCancellationRequested && DateTime.UtcNow < deadline)
    {
        pause.Wait(cts.Token);
        await Task.Delay(local.Next(900, 2000), cts.Token);

        var detail = await rider.Client.GetOrderAsync(orderId, cts.Token);
        if (detail is null) return;

        if (board.TryGetValue(orderId, out var entry))
        {
            board[orderId] = entry with
            {
                Driver = detail.Driver?.FullName,
                Status = detail.Status,
                Fare = detail.FinalFare
            };
        }

        if (abandon && detail.Status is OrderStatus.Requested or OrderStatus.Accepted)
        {
            var cancelled = await rider.Client.CancelOrderAsync(orderId, "Penumpang berubah rencana", cts.Token);
            if (cancelled is not null) metrics.OrderCancelled();
            return;
        }

        if (detail.Status == OrderStatus.Completed)
        {
            metrics.OrderCompleted();

            // Cash was settled by the driver; anything else still needs paying from the app.
            await PayIfOwedAsync(rider, orderId, detail.PaymentMethod, local);

            if (detail.Driver is not null && local.NextDouble() < 0.7)
            {
                var rating = local.NextDouble() < 0.8 ? 5 : local.Next(3, 5);
                var review = await rider.Client.ReviewAsync(orderId, detail.Driver.Id, rating, "Simulasi otomatis", cts.Token);
                if (review is not null) metrics.ReviewSubmitted();
            }

            return;
        }

        if (detail.Status is OrderStatus.Cancelled or OrderStatus.Expired)
        {
            metrics.OrderCancelled();
            return;
        }
    }
}

/// <summary>
/// Settle a trip the rider still owes money on: open a charge, then act as the payer.
///
/// This drives the same provider callback path a live gateway would, so the smoke run covers
/// the payment code rather than stopping at the end of the ride.
/// </summary>
async Task PayIfOwedAsync(RiderActor rider, Guid orderId, PaymentMethod method, Random local)
{
    if (method == PaymentMethod.Cash) return;

    var payment = await rider.Client.GetPaymentAsync(orderId, cts.Token);
    if (payment is null || payment.IsSettled) return;

    // Open the charge if one is not already live.
    if (!payment.IsInFlight || payment.PaymentPayload is null)
    {
        payment = await rider.Client.ChargeAsync(orderId, method, cts.Token);
        if (payment is null) return;
    }

    // A slice of riders abandon the payment screen, which leaves the charge outstanding —
    // a state the dashboard should show rather than one the simulator hides.
    if (local.NextDouble() < 0.1) return;

    pause.Wait(cts.Token);
    await Task.Delay(local.Next(300, 1200), cts.Token);

    var settled = await rider.Client.SettlePaymentAsync(orderId, cts.Token);

    if (settled is { IsSettled: true }) metrics.PaymentSettled();
    else metrics.PaymentFailed();
}

// ─────────────────────────── driver behaviour ───────────────────────────

async Task DriveLoop(DriverActor driver)
{
    var local = new Random(driver.Seed);

    while (!cts.IsCancellationRequested)
    {
        try
        {
            pause.Wait(cts.Token);
            await Task.Delay(local.Next(400, 1500), cts.Token);

            // Drift a little each round so the GPS ping stays fresh and matching keeps working.
            driver.Latitude += (local.NextDouble() - 0.5) * 0.004;
            driver.Longitude += (local.NextDouble() - 0.5) * 0.004;
            await driver.Client.UpdateLocationAsync(driver.Latitude, driver.Longitude, local.Next(0, 360), cts.Token);

            var offers = await driver.Client.AvailableOrdersAsync(cts.Token);
            if (offers.Count == 0) continue;

            var offer = offers[0];

            // Losing this race to another driver is normal; the API answers 409 and we move on.
            if (!await driver.Client.AcceptOrderAsync(offer.OrderId, cts.Token)) continue;

            metrics.OrderAccepted();

            await StepAsync(() => driver.Client.ArriveAsync(offer.OrderId, cts.Token), local, 600, 1800);
            await StepAsync(() => driver.Client.StartAsync(offer.OrderId, cts.Token), local, 500, 1500);
            await StepAsync(() => driver.Client.CompleteAsync(offer.OrderId, cts.Token), local, 1200, 3500);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
}

async Task StepAsync(Func<Task<OrderDetailResponse?>> action, Random local, int minDelay, int maxDelay)
{
    pause.Wait(cts.Token);
    await Task.Delay(local.Next(minDelay, maxDelay), cts.Token);
    await action();
}

// ─────────────────────────── live display ───────────────────────────

async Task RenderLiveAsync()
{
    await AnsiConsole.Live(new Rows(new Text("Memulai…")))
        .StartAsync(async ctx =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(paused ? 400 : 250, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                ctx.UpdateTarget(BuildDisplay());
            }
        });
}

Rows BuildDisplay()
{
    var elapsed = DateTime.UtcNow - startedAt;
    var (p50, p95, max) = metrics.Latency();

    var orders = new Table().Border(TableBorder.Rounded).Expand()
        .AddColumn("Kode").AddColumn("Penumpang").AddColumn("Driver").AddColumn("Tarif").AddColumn("Status");

    foreach (var entry in board.Values.OrderByDescending(e => e.CreatedAt).Take(12).Reverse())
    {
        orders.AddRow(
            $"[grey]{entry.Code.EscapeMarkup()}[/]",
            Trim(entry.Rider, 14),
            entry.Driver is null ? "[grey]—[/]" : Trim(entry.Driver, 14),
            $"Rp {entry.Fare:N0}",
            $"[{StatusColour(entry.Status)}]{Display.Label(entry.Status)}[/]");
    }

    var throughput = elapsed.TotalMinutes > 0 ? metrics.OrdersCreated / elapsed.TotalMinutes : 0;
    var failureRate = metrics.Requests > 0 ? metrics.Failures * 100.0 / metrics.Requests : 0;

    var stats = new Table().Border(TableBorder.Rounded)
        .AddColumn("Metrik").AddColumn("Nilai");

    stats.AddRow("Waktu", $"{elapsed:mm\\:ss}" + (options.DurationSeconds > 0 ? $" / {options.DurationSeconds}s" : ""));
    stats.AddRow("Dibuat", $"[cyan]{metrics.OrdersCreated}[/]");
    stats.AddRow("Diterima", $"[blue]{metrics.OrdersAccepted}[/]");
    stats.AddRow("Selesai", $"[green]{metrics.OrdersCompleted}[/]");
    stats.AddRow("Dibatalkan", $"[red]{metrics.OrdersCancelled}[/]");
    stats.AddRow("Dibayar", $"[green]{metrics.PaymentsSettled}[/]");
    stats.AddRow("Ulasan", $"{metrics.Reviews}");
    stats.AddRow("Order/menit", $"[yellow]{throughput:F1}[/]");
    stats.AddRow("Request", $"{metrics.Requests} ([red]{failureRate:F1}% gagal[/])");
    stats.AddRow("Latensi p50", $"{p50:F0} ms");
    stats.AddRow("Latensi p95", $"[yellow]{p95:F0} ms[/]");
    stats.AddRow("Latensi max", $"{max:F0} ms");

    var header = paused
        ? new Panel("[bold yellow]DIJEDA — tekan P untuk lanjut, S untuk berhenti[/]")
            .Border(BoxBorder.Double).BorderColor(Color.Yellow).Expand()
        : new Panel($"[grey]{riders.Count} penumpang · {drivers.Count} driver aktif[/]")
            .Border(BoxBorder.Rounded).BorderColor(Color.Grey35).Expand();

    return new Rows(header, new Columns(orders, stats));
}

void RenderSummary()
{
    var elapsed = DateTime.UtcNow - startedAt;
    var (p50, p95, max) = metrics.Latency();
    var completionRate = metrics.OrdersCreated > 0 ? metrics.OrdersCompleted * 100.0 / metrics.OrdersCreated : 0;

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[green]Simulasi selesai[/]").RuleStyle("green"));

    var summary = new Table().Border(TableBorder.Rounded).AddColumn("Metrik").AddColumn("Nilai");
    summary.AddRow("Durasi", $"{elapsed:hh\\:mm\\:ss}");
    summary.AddRow("Order dibuat", metrics.OrdersCreated.ToString());
    summary.AddRow("Order selesai", $"{metrics.OrdersCompleted} ({completionRate:F0}%)");
    summary.AddRow("Order dibatalkan", metrics.OrdersCancelled.ToString());
    summary.AddRow("Pembayaran lunas", metrics.PaymentsSettled.ToString());

    if (metrics.PaymentsFailed > 0)
        summary.AddRow("Pembayaran gagal", $"[red]{metrics.PaymentsFailed}[/]");

    summary.AddRow("Ulasan terkirim", metrics.Reviews.ToString());
    summary.AddRow("Total request", metrics.Requests.ToString());
    summary.AddRow("Request gagal", $"{metrics.Failures}");
    summary.AddRow("Latensi p50 / p95 / max", $"{p50:F0} / {p95:F0} / {max:F0} ms");

    AnsiConsole.Write(summary);
    AnsiConsole.MarkupLine("[grey]Akun simulasi tetap ada di database. Hapus FastRide.db untuk mengosongkan.[/]");
    AnsiConsole.WriteLine();
}

// ─────────────────────────── helpers ───────────────────────────

static double RandomLatitude(Random random) => -6.30 + (random.NextDouble() * 0.25);
static double RandomLongitude(Random random) => 106.72 + (random.NextDouble() * 0.28);

/// <summary>
/// Weighted the way Indonesians actually pay: QRIS leads, cash is still common, cards trail.
/// Enumerating the whole enum by index would silently miss any method added later.
/// </summary>
static PaymentMethod PickPaymentMethod(Random random) => random.NextDouble() switch
{
    < 0.40 => PaymentMethod.Qris,
    < 0.62 => PaymentMethod.Cash,
    < 0.80 => PaymentMethod.EWallet,
    < 0.92 => PaymentMethod.VirtualAccount,
    _ => PaymentMethod.CreditCard
};

static string RandomAddress(Random random)
{
    string[] streets =
    [
        "Jl. Sudirman", "Jl. Thamrin", "Jl. Gatot Subroto", "Jl. Rasuna Said", "Jl. Hayam Wuruk",
        "Jl. Gajah Mada", "Jl. Cikini Raya", "Jl. Matraman", "Jl. Pemuda", "Jl. Daan Mogot"
    ];

    return $"{streets[random.Next(streets.Length)]} No. {random.Next(1, 200)}";
}

static string StatusColour(OrderStatus status) => status switch
{
    OrderStatus.Requested => "yellow",
    OrderStatus.Accepted or OrderStatus.DriverArrived => "blue",
    OrderStatus.Started => "cyan",
    OrderStatus.Completed => "green",
    _ => "red"
};

static string Trim(string value, int max) =>
    (value.Length <= max ? value : value[..max]).EscapeMarkup();

// ─────────────────────────── actor records ───────────────────────────

internal sealed record RiderActor(SimulatorClient Client, int Seed);

internal sealed class DriverActor(SimulatorClient client, double latitude, double longitude, int seed)
{
    public SimulatorClient Client { get; } = client;
    public double Latitude { get; set; } = latitude;
    public double Longitude { get; set; } = longitude;
    public int Seed { get; } = seed;
}

internal sealed record BoardEntry(
    string Code, string Rider, string? Driver, decimal Fare, OrderStatus Status, DateTime CreatedAt);
