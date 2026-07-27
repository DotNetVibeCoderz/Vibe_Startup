// KAI schedule integration (F-2).
//
// Honest framing, because it matters for anyone picking this up: KAI has no
// public schedule API. What exists here is the integration seam - a provider
// interface, an HTTP client that speaks a documented request/response shape
// against Integrations:KAI:BaseUrl, and a local provider that reads our own
// tables. The HTTP provider only activates when an API key is configured, and
// it falls back to local data on any failure, so a partner outage degrades the
// page instead of emptying it.
//
// When real credentials appear, the only thing that should need changing is
// MapResponse - the rest of the app talks to ITrainScheduleProvider.
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Trains;

namespace Joka.Services.Trains;

/// <summary>
/// One departure, whatever produced it. Deliberately not TrainSchedule: a
/// remote result has no row in our database and no Guid to book against.
/// </summary>
public record TrainDeparture(
    Guid? ScheduleId,
    string TrainName,
    string TrainNumber,
    string Class,
    string DepartureStationCode,
    string DepartureStationName,
    string ArrivalStationCode,
    string ArrivalStationName,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    int DurationMinutes,
    decimal Price,
    int AvailableSeats,
    string Source)
{
    /// <summary>Only locally-backed departures can be booked through Joka today.</summary>
    public bool IsBookable => ScheduleId is not null;
}

public interface ITrainScheduleProvider
{
    string Name { get; }
    bool IsConfigured { get; }

    Task<List<TrainDeparture>> SearchAsync(
        string? fromCode, string? toCode, DateTime? date, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Local: our own seeded/merchant-managed schedules
// ---------------------------------------------------------------------------
public class LocalTrainScheduleProvider : ITrainScheduleProvider
{
    private readonly AppDbContext _db;

    public LocalTrainScheduleProvider(AppDbContext db) => _db = db;

    public string Name => "Joka";
    public bool IsConfigured => true;

    public async Task<List<TrainDeparture>> SearchAsync(
        string? fromCode, string? toCode, DateTime? date, CancellationToken ct = default)
    {
        var query = _db.TrainSchedules.AsNoTracking()
            .Include(t => t.Train)
            .Include(t => t.DepartureStation)
            .Include(t => t.ArrivalStation)
            .Where(t => t.IsActive);

        if (!string.IsNullOrEmpty(fromCode))
            query = query.Where(t => t.DepartureStation!.Code == fromCode);

        if (!string.IsNullOrEmpty(toCode))
            query = query.Where(t => t.ArrivalStation!.Code == toCode);

        if (date is DateTime d)
        {
            var start = d.Date;
            var end = start.AddDays(1);
            query = query.Where(t => t.DepartureTime >= start && t.DepartureTime < end);
        }

        var rows = await query.OrderBy(t => t.BasePrice).Take(30).ToListAsync(ct);

        return rows.Select(t => new TrainDeparture(
            t.Id,
            t.Train?.Name ?? "—",
            t.Train?.TrainNumber ?? "—",
            t.Train?.Class ?? "—",
            t.DepartureStation?.Code ?? "—",
            t.DepartureStation?.Name ?? "—",
            t.ArrivalStation?.Code ?? "—",
            t.ArrivalStation?.Name ?? "—",
            t.DepartureTime,
            t.ArrivalTime,
            t.DurationMinutes,
            t.BasePrice,
            t.AvailableSeats,
            Name)).ToList();
    }
}

// ---------------------------------------------------------------------------
// KAI over HTTP
// ---------------------------------------------------------------------------
public class KaiTrainScheduleProvider : ITrainScheduleProvider
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<KaiTrainScheduleProvider> _log;

    public KaiTrainScheduleProvider(
        IHttpClientFactory http, IConfiguration config, ILogger<KaiTrainScheduleProvider> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    public string Name => "KAI";

    private string ApiKey => _config["Integrations:KAI:ApiKey"] ?? "";
    private string BaseUrl => (_config["Integrations:KAI:BaseUrl"] ?? "https://api.kai.id").TrimEnd('/');

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task<List<TrainDeparture>> SearchAsync(
        string? fromCode, string? toCode, DateTime? date, CancellationToken ct = default)
    {
        if (!IsConfigured) return new();

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(12);

            var query = new List<string>();
            if (!string.IsNullOrEmpty(fromCode)) query.Add($"origin={Uri.EscapeDataString(fromCode)}");
            if (!string.IsNullOrEmpty(toCode)) query.Add($"destination={Uri.EscapeDataString(toCode)}");
            query.Add($"date={(date ?? DateTime.UtcNow.Date):yyyy-MM-dd}");

            var url = $"{BaseUrl}/v1/schedules?{string.Join("&", query)}";

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.SendAsync(message, ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("KAI menolak permintaan jadwal: {Status}", (int)response.StatusCode);
                return new();
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return MapResponse(json.RootElement);
        }
        catch (Exception ex)
        {
            // Never bubble up: the caller degrades to local data instead.
            _log.LogWarning(ex, "Gagal menghubungi KAI, jatuh ke jadwal lokal.");
            return new();
        }
    }

    /// <summary>
    /// Maps KAI's payload to our shape. The field names below follow the shape
    /// documented for their partner schedule endpoint; adjust here - and only
    /// here - if the real contract differs.
    /// </summary>
    private static List<TrainDeparture> MapResponse(JsonElement root)
    {
        var container = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("data", out var data) ? data : default;

        if (container.ValueKind != JsonValueKind.Array) return new();

        var departures = new List<TrainDeparture>();

        foreach (var item in container.EnumerateArray())
        {
            string Text(string name) =>
                item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() ?? "" : "";

            decimal Number(string name) =>
                item.TryGetProperty(name, out var v) && v.TryGetDecimal(out var d) ? d : 0m;

            int Integer(string name) =>
                item.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;

            if (!DateTime.TryParse(Text("departure_time"), out var departure)) continue;
            if (!DateTime.TryParse(Text("arrival_time"), out var arrival)) arrival = departure;

            departures.Add(new TrainDeparture(
                // No local row, so no id: these show as reference-only.
                ScheduleId: null,
                TrainName: Text("train_name"),
                TrainNumber: Text("train_number"),
                Class: Text("class"),
                DepartureStationCode: Text("origin_code"),
                DepartureStationName: Text("origin_name"),
                ArrivalStationCode: Text("destination_code"),
                ArrivalStationName: Text("destination_name"),
                DepartureTime: departure,
                ArrivalTime: arrival,
                DurationMinutes: (int)(arrival - departure).TotalMinutes,
                Price: Number("price"),
                AvailableSeats: Integer("available_seats"),
                Source: "KAI"));
        }

        return departures;
    }
}

// ---------------------------------------------------------------------------
// Facade the pages actually use
// ---------------------------------------------------------------------------
public record TrainSearchResult(List<TrainDeparture> Departures, string Source, bool UsedFallback);

public class TrainScheduleService
{
    private readonly LocalTrainScheduleProvider _local;
    private readonly KaiTrainScheduleProvider _kai;

    public TrainScheduleService(LocalTrainScheduleProvider local, KaiTrainScheduleProvider kai)
    {
        _local = local;
        _kai = kai;
    }

    public bool KaiEnabled => _kai.IsConfigured;

    /// <summary>
    /// Asks KAI first when it is configured, and falls back to local schedules
    /// when it is not, errors, or returns nothing. The result says which source
    /// answered so the page can be honest about it.
    /// </summary>
    public async Task<TrainSearchResult> SearchAsync(
        string? fromCode, string? toCode, DateTime? date, CancellationToken ct = default)
    {
        if (_kai.IsConfigured)
        {
            var remote = await _kai.SearchAsync(fromCode, toCode, date, ct);

            if (remote.Count > 0)
                return new(remote, _kai.Name, UsedFallback: false);

            var local = await _local.SearchAsync(fromCode, toCode, date, ct);
            return new(local, _local.Name, UsedFallback: true);
        }

        var own = await _local.SearchAsync(fromCode, toCode, date, ct);
        return new(own, _local.Name, UsedFallback: false);
    }
}
