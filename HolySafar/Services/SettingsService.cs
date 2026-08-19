using System.Collections.Concurrent;
using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Konfigurasi aplikasi yang bisa di-override dari UI admin.
/// Urutan resolusi: tabel Pengaturan (DB) -> appsettings.json -> default.
/// Kunci memakai path config yang sama dengan appsettings, mis. "Chatbot:Provider".
/// </summary>
public class SettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private static readonly ConcurrentDictionary<string, string> _cache = new();
    private static bool _loaded;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(IServiceScopeFactory scopeFactory, IConfiguration config)
    { _scopeFactory = scopeFactory; _config = config; }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var p in await db.Pengaturan.AsNoTracking().ToListAsync())
                _cache[p.Kunci] = p.Nilai;
            _loaded = true;
        }
        catch { /* DB belum siap saat startup pertama */ }
        finally { _lock.Release(); }
    }

    /// <summary>Ambil nilai; jatuh ke appsettings.json bila belum di-override dari UI.</summary>
    public string Get(string key, string fallback = "")
    {
        if (_cache.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
        return _config[key] ?? fallback;
    }

    public int GetInt(string key, int fallback) => int.TryParse(Get(key), out var v) ? v : fallback;
    public double GetDouble(string key, double fallback) => double.TryParse(Get(key), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    public bool GetBool(string key, bool fallback) => bool.TryParse(Get(key), out var v) ? v : fallback;

    public async Task SetAsync(string key, string value, string grup = "Umum", string keterangan = "", bool isSecret = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Pengaturan.FirstOrDefaultAsync(p => p.Kunci == key);
        if (row == null)
        {
            row = new PengaturanAplikasi { Kunci = key, Grup = grup, Keterangan = keterangan, IsSecret = isSecret };
            db.Pengaturan.Add(row);
        }
        row.Nilai = value; row.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(grup)) row.Grup = grup;
        if (!string.IsNullOrEmpty(keterangan)) row.Keterangan = keterangan;
        row.IsSecret = isSecret || row.IsSecret;
        await db.SaveChangesAsync();
        _cache[key] = value;
    }

    public async Task<List<PengaturanAplikasi>> GetAllAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Pengaturan.OrderBy(p => p.Grup).ThenBy(p => p.Kunci).ToListAsync();
    }

    /// <summary>Buang override dari DB sehingga nilai kembali mengikuti appsettings.json.</summary>
    public async Task ResetAsync(string key)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Pengaturan.FirstOrDefaultAsync(p => p.Kunci == key);
        if (row != null) { db.Pengaturan.Remove(row); await db.SaveChangesAsync(); }
        _cache.TryRemove(key, out _);
    }
}
