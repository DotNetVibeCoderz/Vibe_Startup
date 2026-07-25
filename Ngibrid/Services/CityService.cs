using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Reads the <see cref="City"/> master table and keeps it in memory.
///
/// Registered as a singleton because the table is small (514 rows), read on nearly every page, and
/// effectively static — re-querying SQLite each time a province dropdown opens buys nothing. Being
/// a singleton it must scope its own <see cref="NgibridDbContext"/>, the same rule the simulator
/// services follow.
///
/// It also owns seeding: the master data has to exist before <see cref="DataSeeder"/> builds sample
/// orders on top of it, and it must be refilled on an existing database that predates the table —
/// so seeding is keyed on "the Cities table is empty", independently of DataSeeder's "no users yet".
/// </summary>
public class CityService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CityService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private List<City>? _cache;

    public CityService(IServiceScopeFactory scopes, ILogger<CityService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>
    /// Fill the table from <see cref="IndonesiaCities"/> when it is empty, then warm the cache and
    /// the coordinate index. Called once at startup, before the sample-data seeder runs.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NgibridDbContext>();

        if (!await db.Cities.AnyAsync())
        {
            db.Cities.AddRange(IndonesiaCities.Build());
            await db.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} kota/kabupaten into the city master table.",
                IndonesiaCities.Table.Length);
        }

        await RefreshAsync();
    }

    /// <summary>Reload the cache from the database and rebuild the shared coordinate index.</summary>
    public async Task RefreshAsync()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NgibridDbContext>();

        var all = await db.Cities.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Province).ThenBy(c => c.Name)
            .ToListAsync();

        await _gate.WaitAsync();
        try { _cache = all; }
        finally { _gate.Release(); }

        CityCoordinates.Load(all);
    }

    public async Task<IReadOnlyList<City>> GetAllAsync()
    {
        if (_cache is { } cached) return cached;

        await _gate.WaitAsync();
        try
        {
            if (_cache is null) await LoadUnderLockAsync();
            return _cache!;
        }
        finally { _gate.Release(); }
    }

    private async Task LoadUnderLockAsync()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NgibridDbContext>();
        _cache = await db.Cities.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Province).ThenBy(c => c.Name)
            .ToListAsync();
        CityCoordinates.Load(_cache);
    }

    /// <summary>Distinct province names, alphabetical — the first dropdown on the order form.</summary>
    public async Task<IReadOnlyList<string>> GetProvincesAsync() =>
        (await GetAllAsync()).Select(c => c.Province).Distinct().OrderBy(p => p).ToList();

    /// <summary>
    /// Cities in a province, ordered so the kota (which take the bulk of the shipping volume) come
    /// first and the kabupaten follow, each alphabetically.
    /// </summary>
    public async Task<IReadOnlyList<City>> GetCitiesAsync(string? province)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(province)) return Array.Empty<City>();

        return all
            .Where(c => string.Equals(c.Province, province, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Type == "KOTA" ? 0 : 1)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Look a city up by a loosely written name ("kab. bandung", "Bandung", "Soreang"), optionally
    /// scoped to a province. Resolution goes through the shared index so it matches exactly what
    /// the coordinate lookup would have picked.
    /// </summary>
    public async Task<City?> FindAsync(string? province, string? name)
    {
        if (CityCoordinates.CanonicalName(province, name) is not { } canonical) return null;
        // FullName is unique nationally: (Type, Name) is, and FullName is built from the pair.
        return (await GetAllAsync()).FirstOrDefault(c => c.FullName == canonical);
    }

    /// <summary>The province a stored city name belongs to, for back-filling older orders.</summary>
    public async Task<string?> ResolveProvinceAsync(string? cityName) =>
        (await FindAsync(null, cityName))?.Province;
}

/// <summary>
/// The shared name → coordinate index. Tracking, pricing, routing, and the GPS simulator all go
/// through it so one city means one point everywhere.
///
/// It starts from the compiled-in <see cref="IndonesiaCities"/> table so resolution works before the
/// database is up (startup, and any code path that runs outside a request), and
/// <see cref="CityService"/> replaces it with the live rows once they are loaded.
///
/// Lookup is deliberately forgiving because city names reach it from marketplace imports, the chat
/// bot, and hand-typed API calls: "KOTA BANDUNG", "Kab. Bandung", "bandung" and the seat name
/// "Soreang" all have to land somewhere sensible. Province-qualified lookup comes first — without it
/// "Bandung" is genuinely ambiguous (the kota and the kabupaten seat are ~15 km apart, and pairs
/// like Kota/Kabupaten Sorong sit in different provinces entirely).
/// </summary>
public static class CityCoordinates
{
    private sealed record Entry(string Province, string Name, string FullName, string Type,
        string? Seat, double Lat, double Lng);

    // Declared before the index fields on purpose: static field initialisers run in declaration
    // order, and building the index calls Normalize(), which reads this. Below the index it is
    // still null at that point and every lookup dies in the type initialiser.
    // Longest prefix first — "kabupaten administrasi " has to be tried before "kabupaten ".
    private static readonly (string Prefix, string Type)[] Prefixes =
    {
        ("kabupaten administrasi ", "KABUPATEN"), ("kota administrasi ", "KOTA"),
        ("kabupaten ", "KABUPATEN"), ("kotamadya ", "KOTA"), ("kota ", "KOTA"),
        ("kab ", "KABUPATEN"), ("kb ", "KABUPATEN")
    };

    /// <summary>Colloquial name → the key of the area it should resolve to. Both sides normalised.</summary>
    private static readonly (string Alias, string Target)[] Colloquial =
    {
        ("jakarta", "KOTA|jakarta pusat"),
        ("dki jakarta", "KOTA|jakarta pusat"),
        ("jakarta raya", "KOTA|jakarta pusat"),
        ("jogja", "KOTA|yogyakarta"),
        ("jogjakarta", "KOTA|yogyakarta"),
        ("yogya", "KOTA|yogyakarta"),
        ("solo", "KOTA|surakarta")
    };

    // Replaced wholesale on load; readers take a local reference, so no lock is needed.
    private static volatile List<Entry> _entries = IndonesiaCities.Table
        .Select(r => new Entry(r.Province, r.Name, FullNameOf(r.Type, r.Name), r.Type,
            string.IsNullOrEmpty(r.Seat) ? null : r.Seat, r.Lat, r.Lng))
        .ToList();

    private static volatile Dictionary<string, Entry> _index = Build(_entries);

    /// <summary>Rebuild the index from the database rows.</summary>
    public static void Load(IEnumerable<City> cities)
    {
        var entries = cities
            .Select(c => new Entry(c.Province, c.Name, c.FullName, c.Type, c.SeatName,
                c.Latitude, c.Longitude))
            .ToList();
        if (entries.Count == 0) return; // never blank out a working index

        _index = Build(entries);
        _entries = entries;
    }

    private static string FullNameOf(string type, string name) =>
        type == "KOTA" ? $"Kota {name}" : $"Kabupaten {name}";

    /// <summary>
    /// Four key shapes, from most specific to least. The type has to be part of the key rather than
    /// stripped away: "Kabupaten Bandung" and "Kota Bandung" both fold to "bandung", so a
    /// province+name key alone would map them to whichever row happened to be indexed last, and the
    /// kabupaten's seat at Soreang is ~15 km from the kota.
    /// </summary>
    private static Dictionary<string, Entry> Build(List<Entry> entries)
    {
        var index = new Dictionary<string, Entry>(StringComparer.Ordinal);

        // Exact: province + type + name, and type + name — (Type, Name) is unique nationally.
        foreach (var e in entries)
        {
            var name = Normalize(e.Name);
            index[$"{Normalize(e.Province)}|{e.Type}|{name}"] = e;
            index[$"{e.Type}|{name}"] = e;
        }

        // Ambiguous: name without a type. 26 names are carried by both a kota and a kabupaten;
        // for those the kota wins, because that is what someone typing a city into a shipping
        // form means. Province-scoped first so it stays right even for names shared across provinces.
        foreach (var e in entries)
        {
            Offer(index, $"{Normalize(e.Province)}|{Normalize(e.Name)}", e);
            Offer(index, Normalize(e.Name), e);
        }

        // Seat names as aliases, so an address written "Purwokerto" or "Cibinong" still resolves.
        // They never displace a real city of the same name — "Martapura" is the seat of Kab. Banjar
        // and also of Kab. OKU Timur, and neither may shadow an actual kota/kabupaten entry.
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.Seat)) continue;
            var key = Normalize(e.Seat);
            if (key.Length > 0 && !index.ContainsKey(key)) index[key] = e;
        }

        // Everyday names that are not administrative areas at all. "Jakarta" is the big one: DKI is
        // five kota and no row is called just "Jakarta", so without this the most common city name
        // in Indonesian addresses fell through to the made-up fallback coordinate.
        foreach (var (alias, target) in Colloquial)
        {
            if (index.ContainsKey(alias)) continue;
            if (index.TryGetValue(target, out var e)) index[alias] = e;
        }

        return index;
    }

    private static void Offer(Dictionary<string, Entry> index, string key, Entry candidate)
    {
        if (key.Length == 0) return;
        if (!index.TryGetValue(key, out var existing)) { index[key] = candidate; return; }
        if (existing.Type != "KOTA" && candidate.Type == "KOTA") index[key] = candidate;
    }

    /// <summary>
    /// Fold a written name to a comparison key: lowercase, punctuation and administrative
    /// prefixes stripped, whitespace collapsed. The prefix that was stripped is reported
    /// separately by <see cref="Split"/>.
    /// </summary>
    public static string Normalize(string? value) => Split(value).Name;

    /// <summary>
    /// Split a written city into the administrative type it names, if any, and the bare name:
    /// "Kab. Bandung" → (KABUPATEN, "bandung"), "bandung" → (null, "bandung").
    /// </summary>
    private static (string? Type, string Name) Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, string.Empty);

        var chars = new char[value.Length];
        var n = 0;
        var lastSpace = true;
        foreach (var raw in value)
        {
            var ch = char.ToLowerInvariant(raw);
            if (char.IsLetterOrDigit(ch)) { chars[n++] = ch; lastSpace = false; }
            else if (!lastSpace) { chars[n++] = ' '; lastSpace = true; }
        }
        var text = new string(chars, 0, n).Trim();

        foreach (var (prefix, type) in Prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length > prefix.Length)
                return (type, text[prefix.Length..].Trim());
        }
        return (null, text);
    }

    /// <summary>
    /// Resolve a city name to coordinates, preferring the province-qualified match.
    /// Unknown names fall back to a stable pseudo-location so a quote or a map pin at least stays
    /// put between page loads; use <see cref="TryResolve"/> when you need to know it was a guess.
    /// </summary>
    public static (double Lat, double Lng) Resolve(string? city) => Resolve(null, city);

    public static (double Lat, double Lng) Resolve(string? province, string? city)
    {
        if (TryResolve(province, city, out var found)) return found;

        if (string.IsNullOrWhiteSpace(city)) return Resolve(null, "Jakarta Pusat");

        var hash = Math.Abs(StableHash(Normalize(city)));
        return (-6.2 - hash % 200 / 100.0, 106.8 + hash % 500 / 100.0);
    }

    /// <summary>Coordinate lookup that reports whether the city is actually in the master data.</summary>
    public static bool TryResolve(string? province, string? city, out (double Lat, double Lng) coords)
    {
        coords = default;
        if (Lookup(province, city) is not { } entry) return false;
        coords = (entry.Lat, entry.Lng);
        return true;
    }

    /// <summary>
    /// Narrow from the most specific key to the least: province+type+name, province+name,
    /// type+name, name. Each step drops one piece of information, so the first hit is the best
    /// interpretation of what the caller wrote.
    /// </summary>
    private static Entry? Lookup(string? province, string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return null;

        var index = _index;
        var (type, name) = Split(city);
        if (name.Length == 0) return null;

        var hasProvince = !string.IsNullOrWhiteSpace(province);
        var prov = hasProvince ? Normalize(province) : string.Empty;

        if (hasProvince && type is not null && index.TryGetValue($"{prov}|{type}|{name}", out var a)) return a;
        if (hasProvince && index.TryGetValue($"{prov}|{name}", out var b)) return b;
        if (type is not null && index.TryGetValue($"{type}|{name}", out var c)) return c;
        return index.TryGetValue(name, out var d) ? d : null;
    }

    public static bool IsKnown(string? province, string? city) => TryResolve(province, city, out _);

    /// <summary>
    /// The province a city name belongs to, or null when it isn't in the master data. Used to
    /// back-fill imported orders, which arrive from marketplaces with a city but no province.
    /// </summary>
    public static string? ProvinceOf(string? city) => Lookup(null, city)?.Province;

    /// <summary>The canonical stored name ("Kota Bandung") for a loosely written city, if known.</summary>
    public static string? CanonicalName(string? province, string? city) =>
        Lookup(province, city)?.FullName;

    /// <summary>Every indexed city as (FullName, Province, Lat, Lng) — used by the map pickers.</summary>
    public static IReadOnlyList<(string FullName, string Province, double Lat, double Lng)> All =>
        _entries.Select(e => (e.FullName, e.Province, e.Lat, e.Lng)).ToList();

    /// <summary>
    /// FNV-1a. <see cref="string.GetHashCode()"/> is randomised per process, which made the
    /// fallback coordinates of an unknown city move on every restart.
    /// </summary>
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var ch in value) hash = (hash ^ ch) * 16777619;
            return hash;
        }
    }
}
