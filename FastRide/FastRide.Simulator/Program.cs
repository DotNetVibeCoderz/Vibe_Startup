using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build();
var apiBase = config["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
var riderCount = int.Parse(config["Simulation:RiderCount"] ?? "5");
var driverCount = int.Parse(config["Simulation:DriverCount"] ?? "3");
var connectApi = bool.Parse(config["Simulation:ConnectToApi"] ?? "true");
var seed = int.Parse(config["Simulation:RandomSeed"] ?? "42");
var random = new Random(seed);

var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
var http = new HttpClient(handler) { BaseAddress = new Uri(apiBase), Timeout = TimeSpan.FromSeconds(10) };

var pickLock = new SemaphoreSlim(1, 1);

AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("FastRide Ride-Hailing Simulator").RuleStyle("gold1").Centered());
AnsiConsole.WriteLine($"API  : {apiBase}");
AnsiConsole.WriteLine($"Mode : {(connectApi ? "API-Live" : "Local-Sim")}");

// ═══ FETCH USERS ═══
List<ApiUser> realRiders = new(), realDrivers = new();

if (connectApi)
{
    try
    {
        var regName = $"SimRider-{random.Next(1000, 9999)}";
        var regEmail = $"{regName.ToLower()}@sim.com";
        var regResp = await http.PostAsJsonAsync("/api/auth/register",
            new { fullName = regName, email = regEmail, phoneNumber = "0800-0000-0000", password = "SimPass123!", role = 1 });
        LoginData? login = null;
        if (regResp.IsSuccessStatusCode) login = await regResp.Content.ReadFromJsonAsync<LoginData>();
        else
        {
            var lr = await http.PostAsJsonAsync("/api/auth/login", new { email = regEmail, password = "SimPass123!" });
            if (lr.IsSuccessStatusCode) login = await lr.Content.ReadFromJsonAsync<LoginData>();
        }
        if (login == null) { AnsiConsole.WriteLine("[red]FAILED login. Local mode.[/]"); connectApi = false; }
        else
        {
            http.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);
            realRiders.Add(new ApiUser { Id = login.UserId, FullName = login.FullName });
            AnsiConsole.WriteLine($"  [green]✓[/] Rider : {login.FullName}");

            for (int i = 0; i < driverCount; i++)
            {
                var dName = $"SimDriver-{random.Next(1000, 9999)}";
                var dReg = await http.PostAsJsonAsync("/api/auth/register",
                    new { fullName = dName, email = $"{dName.ToLower()}@sim.com", phoneNumber = "0800-0000-0000",
                          password = "SimPass123!", role = 2, licenseNumber = $"SIM-{random.Next(100000, 999999)}",
                          vehicleType = "Toyota Avanza", vehiclePlate = $"B {random.Next(1000, 9999)} XYZ" });
                if (dReg.IsSuccessStatusCode) { var dd = await dReg.Content.ReadFromJsonAsync<LoginData>(); if (dd != null) realDrivers.Add(new ApiUser { Id = dd.UserId, FullName = dd.FullName }); }
            }
            AnsiConsole.WriteLine($"  [green]✓[/] Drivers: {realDrivers.Count}");
        }
    }
    catch (Exception ex) { AnsiConsole.WriteLine($"[red]API fail: {ex.Message}[/]"); connectApi = false; }
}
if (!connectApi || realRiders.Count == 0) { AnsiConsole.WriteLine("[yellow]Running in Local mode.[/]"); connectApi = false; }

AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule());
AnsiConsole.WriteLine("  [bold yellow][[S]][/] Stop  |  [bold cyan][[P]][/] Pause/Resume");
AnsiConsole.WriteLine();

// ═══ STATE ═══
var stateLock = new object();
bool _isPaused = false;
void SetPaused(bool v) { lock (stateLock) _isPaused = v; }
bool IsPaused() { lock (stateLock) return _isPaused; }

var riders = new List<SimRider>();
var drivers = new List<SimDriver>();
for (int i = 0; i < riderCount; i++) { var r = (connectApi && realRiders.Count > 0) ? realRiders[i % realRiders.Count] : null; riders.Add(new SimRider { Id = r?.Id ?? Guid.NewGuid(), Name = r?.FullName ?? $"R-{i + 1}", Lat = -6.2 + random.NextDouble() * 0.2, Lng = 106.8 + random.NextDouble() * 0.2 }); }
for (int i = 0; i < driverCount; i++) { var d = (connectApi && realDrivers.Count > 0) ? realDrivers[i % realDrivers.Count] : null; drivers.Add(new SimDriver { Id = d?.Id ?? Guid.NewGuid(), Name = d?.FullName ?? $"D-{i + 1}", Lat = -6.2 + random.NextDouble() * 0.2, Lng = 106.8 + random.NextDouble() * 0.2, Status = "Online", Vehicle = "Economy" }); }

var orders = new List<SimOrder>();
var cts = new CancellationTokenSource();
var pauseEvent = new ManualResetEventSlim(true);
int totalCreated = 0, totalCompleted = 0, apiOk = 0, apiFail = 0;
var startTime = DateTime.UtcNow;

// ═══ RIDER LOOP ═══
async Task RiderLoop(SimRider r)
{
    while (!cts.Token.IsCancellationRequested)
    {
        pauseEvent.Wait(cts.Token);
        await Task.Delay(random.Next(500, 2500), cts.Token);
        pauseEvent.Wait(cts.Token);

        var pickupLat = r.Lat; var pickupLng = r.Lng;
        var dropoffLat = r.Lat + (random.NextDouble() - 0.5) * 0.05;
        var dropoffLng = r.Lng + (random.NextDouble() - 0.5) * 0.05;
        var fare = random.Next(10000, 80000);

        Guid realOrderId = Guid.Empty;
        bool apiCreated = false;

        if (connectApi)
        {
            try
            {
                var resp = await http.PostAsJsonAsync("/api/orders", new
                {
                    riderId = r.Id, pickupLatitude = pickupLat, pickupLongitude = pickupLng,
                    pickupAddress = "SimPickup", dropoffLatitude = dropoffLat,
                    dropoffLongitude = dropoffLng, dropoffAddress = "SimDropoff",
                    vehicleCategory = 1, paymentMethod = 1
                });
                if (resp.IsSuccessStatusCode)
                {
                    var c = await resp.Content.ReadFromJsonAsync<CreatedOrder>();
                    if (c != null) { realOrderId = c.Id; apiCreated = true; }
                    Interlocked.Increment(ref apiOk);
                }
                else Interlocked.Increment(ref apiFail);
            }
            catch { Interlocked.Increment(ref apiFail); }
        }

        var o = new SimOrder
        {
            Id = apiCreated ? realOrderId : Guid.NewGuid(),
            RiderId = r.Id, RiderName = r.Name,
            PickupLat = pickupLat, PickupLng = pickupLng,
            DropoffLat = dropoffLat, DropoffLng = dropoffLng,
            Status = "Requested", Fare = fare, CreatedAt = DateTime.UtcNow
        };
        r.Lat = dropoffLat; r.Lng = dropoffLng;
        lock (orders) { orders.Add(o); }
        Interlocked.Increment(ref totalCreated);
    }
}

// ═══ DRIVER LOOP ═══
async Task DriverLoop(SimDriver d)
{
    while (!cts.Token.IsCancellationRequested)
    {
        pauseEvent.Wait(cts.Token);
        await Task.Delay(random.Next(150, 1000), cts.Token);
        pauseEvent.Wait(cts.Token);

        SimOrder? t = null;
        await pickLock.WaitAsync(cts.Token);
        try
        {
            lock (orders)
            {
                t = orders.FirstOrDefault(o => o.Status == "Requested" && o.DriverId == null);
                if (t != null) { t.DriverId = d.Id; t.DriverName = d.Name; t.Status = "Accepted"; }
            }
        }
        finally { pickLock.Release(); }

        if (t != null)
        {
            d.Status = "OnTrip"; Guid apiOid = t.Id; bool acceptOk = false;
            if (connectApi)
            {
                for (int retry = 0; retry < 2 && !acceptOk; retry++)
                {
                    try
                    {
                        var ar = await http.PutAsJsonAsync($"/api/mobile/driver/{d.Id}/accept-order", new { orderId = apiOid });
                        if (ar.IsSuccessStatusCode) { acceptOk = true; Interlocked.Increment(ref apiOk); }
                        else if (retry == 1) Interlocked.Increment(ref apiFail);
                    }
                    catch { if (retry == 1) Interlocked.Increment(ref apiFail); else await Task.Delay(100); }
                }
            }
            else acceptOk = true;

            await Task.Delay(random.Next(400, 2000));
            lock (orders) { t.Status = "Completed"; t.CompletedAt = DateTime.UtcNow; }
            d.CompletedOrders++; d.TotalEarnings += t.Fare; d.Status = "Online";
            Interlocked.Increment(ref totalCompleted);

            if (connectApi && acceptOk)
            {
                try
                {
                    var cr = await http.PutAsJsonAsync($"/api/mobile/driver/{d.Id}/complete-order", new { orderId = apiOid });
                    if (cr.IsSuccessStatusCode) Interlocked.Increment(ref apiOk);
                    else Interlocked.Increment(ref apiFail);
                }
                catch { Interlocked.Increment(ref apiFail); }
            }
        }
    }
}

var riderTasks = riders.Select(RiderLoop).ToList();
var driverTasks = drivers.Select(DriverLoop).ToList();

// ═══ KEYBOARD LISTENER ═══
_ = Task.Run(() =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        if (!Console.KeyAvailable) { Thread.Sleep(100); continue; }
        var key = Console.ReadKey(intercept: true);
        switch (char.ToUpperInvariant(key.KeyChar))
        {
            case 'S':
                SetPaused(false); pauseEvent.Set(); cts.Cancel(); break;
            case 'P':
                if (IsPaused()) { SetPaused(false); pauseEvent.Set(); }
                else { SetPaused(true); pauseEvent.Reset(); }
                break;
        }
    }
}, cts.Token);

// ═══ LIVE DISPLAY ═══
await AnsiConsole.Live(new Table()).StartAsync(async ctx =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        var delayMs = IsPaused() ? 500 : 200;
        await Task.Delay(delayMs);

        // Order Table
        var ot = new Table().Border(TableBorder.Rounded)
            .AddColumn("Order").AddColumn("Rider").AddColumn("Driver").AddColumn("Fare").AddColumn("Status");
        List<SimOrder> snap;
        lock (orders) { snap = orders.OrderByDescending(x => x.CreatedAt).Take(12).Reverse().ToList(); }
        foreach (var o in snap)
        {
            var c = o.Status switch { "Requested" => "cyan", "Accepted" => "magenta", "Completed" => "green", _ => "grey" };
            ot.AddRow($"#{o.Id.ToString()[..6]}", Safe(o.RiderName, 8),
                o.DriverName != null ? Safe(o.DriverName, 8) : "[dim]-[/]",
                $"Rp {o.Fare:N0}", $"[{c}]{o.Status}[/]");
        }

        // Driver Table
        var dt = new Table().Border(TableBorder.Rounded)
            .AddColumn("Driver").AddColumn("Status").AddColumn("Trips").AddColumn("Earn");
        foreach (var d in drivers)
        {
            var sColor = d.Status switch { "Online" => "green", "OnTrip" => "orange1", _ => "dim" };
            dt.AddRow(Safe(d.Name, 10), $"[{sColor}]{d.Status}[/]", d.CompletedOrders.ToString(), $"Rp {d.TotalEarnings:N0}");
        }

        // Stats bar
        var elapsed = DateTime.UtcNow - startTime;
        var elapsedStr = elapsed.TotalHours >= 1
            ? $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
            : $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        var compRate = totalCreated > 0 ? (int)(100.0 * totalCompleted / totalCreated) : 0;
        var totalRevenue = drivers.Sum(d => d.TotalEarnings);
        var ordersPerMin = elapsed.TotalMinutes > 0 ? (totalCreated / elapsed.TotalMinutes).ToString("F0") : "0";

        var infoText = $"  [bold]⏱ {elapsedStr}[/]  |  " +
            $"📦 Created: [cyan]{totalCreated}[/]  |  " +
            $"✅ Done: [green]{totalCompleted}[/] ([green]{compRate}%[/])  |  " +
            $"💰 Revenue: [lime]Rp {totalRevenue:N0}[/]  |  " +
            $"📊 Rate: [yellow]{ordersPerMin}/min[/]";
        if (connectApi) infoText += $"  |  API: [green]{apiOk}[/]/[red]{apiFail}[/]";

        if (IsPaused())
        {
            // ★ FIX: Double brackets [[P]] and [[S]] to escape Spectre.Console markup
            var pauseBanner = new Panel("[bold yellow]⏸  PAUSED  —  Press [[P]] to resume, [[S]] to stop[/]")
                .Border(BoxBorder.Double).BorderColor(Color.Yellow);
            ctx.UpdateTarget(new Rows(pauseBanner, new Markup(infoText), new Columns(ot, dt)));
        }
        else
        {
            ctx.UpdateTarget(new Rows(new Markup(infoText), new Columns(ot, dt)));
        }
    }
});

// ═══ SHUTDOWN ═══
cts.Cancel(); pauseEvent.Set();
try { await Task.WhenAll(riderTasks.Concat(driverTasks)); } catch { }

// ═══ SUMMARY ═══
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[green]Simulation Complete[/]").RuleStyle("green"));
var sum = new Table().Border(TableBorder.Rounded).AddColumn("Metric").AddColumn("Value");
var elapsedFinal = DateTime.UtcNow - startTime;
var elapsedFinalStr = elapsedFinal.TotalHours >= 1
    ? $"{elapsedFinal.Hours:D2}h {elapsedFinal.Minutes:D2}m {elapsedFinal.Seconds:D2}s"
    : $"{elapsedFinal.Minutes:D2}m {elapsedFinal.Seconds:D2}s";
var compPct = totalCreated > 0 ? (int)(100.0 * totalCompleted / totalCreated) : 0;
sum.AddRow("Total Runtime", elapsedFinalStr);
sum.AddRow("Orders Created", totalCreated.ToString());
sum.AddRow("Orders Completed", $"{totalCompleted} ({compPct}%)");
sum.AddRow("Orders Pending", orders.Count(o => o.Status is "Requested" or "Accepted").ToString());
sum.AddRow("Total Revenue", $"Rp {drivers.Sum(d => d.TotalEarnings):N0}");
if (connectApi) sum.AddRow("API OK / Fail", $"[green]{apiOk}[/] / [red]{apiFail}[/]");
AnsiConsole.Write(sum);

// ═══ HELPERS ═══
static string Safe(string s, int max) => (s.Length <= max ? s : s[..max]).Replace("[", "").Replace("]", "");

class SimRider { public Guid Id; public string Name = ""; public double Lat, Lng; }
class SimDriver { public Guid Id; public string Name = ""; public double Lat, Lng; public string Status = "Offline"; public string Vehicle = ""; public int CompletedOrders; public decimal TotalEarnings; }
class SimOrder { public Guid Id; public Guid RiderId; public string RiderName = ""; public Guid? DriverId; public string? DriverName; public double PickupLat, PickupLng, DropoffLat, DropoffLng; public string Status = "Requested"; public decimal Fare; public DateTime CreatedAt; public DateTime? CompletedAt; }
class LoginData { public Guid UserId { get; set; } public string FullName { get; set; } = ""; public string Token { get; set; } = ""; }
class ApiUser { public Guid Id { get; set; } public string FullName { get; set; } = ""; }
class CreatedOrder { public Guid Id { get; set; } }
