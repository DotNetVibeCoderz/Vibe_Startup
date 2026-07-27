// Reads and writes the AppConfiguration overrides behind the admin Settings page.
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Common;

namespace Joka.Services;

/// <summary>One editable setting, with the value currently in effect.</summary>
public record SettingItem(
    string Key,
    string Label,
    string Group,
    string Editor,          // text, number, decimal, bool, longtext, select
    string? Options,        // comma separated, for Editor = select
    string? Effective,      // what IConfiguration resolves to right now
    bool IsOverridden);     // true when a database row is winning

public class SettingsService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public SettingsService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// The keys exposed for editing. Deliberately a fixed list rather than a
    /// dump of appsettings: secrets and connection strings must not be editable
    /// from a web page.
    /// </summary>
    private static readonly (string Key, string Label, string Group, string Editor, string? Options)[] Editable =
    {
        ("AppSettings:AppName", "Nama aplikasi", "Umum", "text", null),
        ("AppSettings:AppTagline", "Tagline", "Umum", "text", null),
        ("AppSettings:DefaultLanguage", "Bahasa default", "Umum", "select", "id,en"),
        ("AppSettings:DefaultCurrency", "Mata uang default", "Umum", "select", "IDR,USD,SGD,EUR"),
        ("AppSettings:DefaultTheme", "Tema default", "Umum", "select", "light,dark"),
        ("AppSettings:ShowLanguageSwitcher", "Tampilkan pilihan bahasa", "Umum", "bool", null),
        ("AppSettings:ItemsPerPage", "Item per halaman", "Umum", "number", null),
        ("AppSettings:MaxSearchResults", "Maksimum hasil pencarian", "Umum", "number", null),

        ("AppSettings:Auth:AllowRegistration", "Izinkan pendaftaran", "Autentikasi", "bool", null),
        ("AppSettings:Auth:MaxFailedAttempts", "Batas gagal login", "Autentikasi", "number", null),
        ("AppSettings:Auth:LockoutMinutes", "Durasi kunci (menit)", "Autentikasi", "number", null),

        ("Storage:Provider", "Provider penyimpanan", "Penyimpanan", "select", "FileSystem,AzureBlob,S3,MinIO"),

        ("Payment:DefaultGateway", "Gateway default", "Pembayaran", "select", "Midtrans,Xendit"),
        ("Payment:PayLater:MaxTenorMonths", "Tenor maksimum (bulan)", "Pembayaran", "number", null),
        ("Payment:PayLater:MinAmount", "Minimum PayLater (Rp)", "Pembayaran", "decimal", null),
        ("Payment:PayLater:InterestRate", "Bunga per bulan (%)", "Pembayaran", "decimal", null),

        ("ChatBot:Name", "Nama chatbot", "Chatbot", "text", null),
        ("ChatBot:Provider", "Provider aktif", "Chatbot", "select", "OpenAI,Anthropic,Gemini,Ollama"),
        ("ChatBot:Temperature", "Temperature", "Chatbot", "decimal", null),
        ("ChatBot:MaxTokens", "Maksimum token", "Chatbot", "number", null),
        ("ChatBot:SystemPrompt", "System prompt", "Chatbot", "longtext", null)
    };

    public async Task<List<SettingItem>> GetAllAsync()
    {
        var overrides = await _db.AppConfigurations.AsNoTracking()
            .ToDictionaryAsync(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);

        return Editable.Select(e => new SettingItem(
            e.Key, e.Label, e.Group, e.Editor, e.Options,
            _config[e.Key],
            overrides.ContainsKey(e.Key))).ToList();
    }

    public async Task<(bool Success, string Message)> SaveAsync(
        Dictionary<string, string?> values, string actor)
    {
        foreach (var (key, raw) in values)
        {
            var definition = Editable.FirstOrDefault(e => e.Key == key);
            if (definition.Key is null) continue;      // not an editable key - ignore

            var error = Validate(definition.Editor, definition.Label, raw);
            if (error is not null) return (false, error);
        }

        foreach (var (key, raw) in values)
        {
            if (!Editable.Any(e => e.Key == key)) continue;

            var existing = await _db.AppConfigurations.FirstOrDefaultAsync(c => c.Key == key);

            // An empty box means "fall back to appsettings", not "set to blank".
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (existing is not null) _db.AppConfigurations.Remove(existing);
                continue;
            }

            if (existing is null)
            {
                _db.AppConfigurations.Add(new AppConfiguration
                {
                    Key = key, Value = raw.Trim(), UpdatedAt = DateTime.UtcNow,
                    Description = $"Diubah dari halaman Settings oleh {actor}"
                });
            }
            else if (existing.Value != raw.Trim())
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "AppConfiguration", EntityId = key, Action = "Update",
                    Changes = $"{existing.Value} -> {raw.Trim()}", UserId = actor, Timestamp = DateTime.UtcNow
                });

                existing.Value = raw.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        // Push the new values into IConfiguration so they take effect now.
        DatabaseConfigurationProvider.Current?.Reload();

        return (true, "Pengaturan disimpan dan langsung berlaku.");
    }

    public async Task<(bool Success, string Message)> ResetAsync(string key, string actor)
    {
        var existing = await _db.AppConfigurations.FirstOrDefaultAsync(c => c.Key == key);
        if (existing is null) return (false, "Setelan itu memang belum ditimpa.");

        _db.AppConfigurations.Remove(existing);
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = "AppConfiguration", EntityId = key, Action = "Reset",
            Changes = $"Kembali ke nilai appsettings (sebelumnya {existing.Value})",
            UserId = actor, Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        DatabaseConfigurationProvider.Current?.Reload();

        return (true, "Dikembalikan ke nilai appsettings.json.");
    }

    private static string? Validate(string editor, string label, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;   // empty = reset, always allowed

        switch (editor)
        {
            case "number":
                if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var i) || i < 0)
                    return $"\"{label}\" harus berupa angka bulat tidak negatif.";
                break;

            case "decimal":
                // Invariant on purpose: config values are written like "2.5",
                // and the app runs under id-ID where "." is a thousands separator.
                if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) || d < 0)
                    return $"\"{label}\" harus berupa angka, pakai titik sebagai desimal (contoh 2.5).";
                break;

            case "bool":
                if (!bool.TryParse(raw, out _))
                    return $"\"{label}\" harus true atau false.";
                break;
        }

        return null;
    }
}
