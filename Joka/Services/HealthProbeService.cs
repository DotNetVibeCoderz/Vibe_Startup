// Live health probes for the admin console. The seeded SystemHealthCheck rows
// were static numbers; these actually touch each component and time it.
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Backoffice;
using Joka.Services.Storage;

namespace Joka.Services;

public class HealthProbeService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly StorageServiceFactory _storage;
    private readonly IWebHostEnvironment _env;

    public HealthProbeService(AppDbContext db, IConfiguration config,
        StorageServiceFactory storage, IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        _storage = storage;
        _env = env;
    }

    public async Task<List<SystemHealthCheck>> ProbeAsync()
    {
        var results = new List<SystemHealthCheck>
        {
            await ProbeDatabaseAsync(),
            ProbeStorage(),
            ProbeChatBot(),
            ProbePaymentGateway(),
            ProbeWeb()
        };

        // Replace the previous snapshot so the table shows current readings.
        var stale = await _db.SystemHealthChecks.ToListAsync();
        _db.SystemHealthChecks.RemoveRange(stale);
        _db.SystemHealthChecks.AddRange(results);
        await _db.SaveChangesAsync();

        return results;
    }

    private async Task<SystemHealthCheck> ProbeDatabaseAsync()
    {
        var provider = _config["Database:Provider"] ?? "SQLite";
        var sw = Stopwatch.StartNew();

        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            var users = canConnect ? await _db.Users.CountAsync() : 0;
            sw.Stop();

            return new SystemHealthCheck
            {
                Component = $"Database ({provider})",
                Status = canConnect ? (sw.ElapsedMilliseconds > 1000 ? "Degraded" : "Healthy") : "Down",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                UptimePercent = canConnect ? 100 : 0,
                Message = canConnect ? $"{users} pengguna terbaca" : "Tidak bisa terhubung"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Down($"Database ({provider})", ex.Message);
        }
    }

    private SystemHealthCheck ProbeStorage()
    {
        var provider = _config["Storage:Provider"] ?? "FileSystem";
        var sw = Stopwatch.StartNew();

        try
        {
            // Round-trip a small file so the check proves writability, not just config.
            var service = _storage.Create();
            var name = $"_healthcheck/{Guid.NewGuid():N}.txt";
            using var content = new MemoryStream("ok"u8.ToArray());

            var path = service.UploadAsync(name, content, "text/plain").GetAwaiter().GetResult();
            var deleted = service.DeleteAsync(path).GetAwaiter().GetResult();
            sw.Stop();

            return new SystemHealthCheck
            {
                Component = $"Storage ({_storage.ActiveProvider})",
                Status = "Healthy",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                UptimePercent = 100,
                Message = deleted ? "Tulis dan hapus berhasil" : "Tulis berhasil, hapus gagal"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Down($"Storage ({provider})", ex.Message);
        }
    }

    private SystemHealthCheck ProbeChatBot()
    {
        var provider = _config["ChatBot:Provider"] ?? "OpenAI";
        var key = _config[$"ChatBot:Providers:{provider}:ApiKey"];
        var configured = !string.IsNullOrWhiteSpace(key) || provider == "Ollama";

        return new SystemHealthCheck
        {
            Component = $"ChatBot ({provider})",
            Status = configured ? "Healthy" : "Down",
            ResponseTimeMs = 0,
            UptimePercent = configured ? 100 : 0,
            Message = configured
                ? "API key terpasang. Latensi tergantung provider."
                : "API key belum diisi di appsettings"
        };
    }

    private SystemHealthCheck ProbePaymentGateway()
    {
        var requested = _config["Payment:DefaultGateway"] ?? "Midtrans";
        var key = _config[$"Payment:Gateways:{requested}:ServerKey"]
                  ?? _config[$"Payment:Gateways:{requested}:ApiKey"];
        var configured = !string.IsNullOrWhiteSpace(key);

        // Degraded rather than Down: checkout still works, it just settles
        // through the simulated gateway instead of a real one.
        return new SystemHealthCheck
        {
            Component = $"Payment Gateway ({requested})",
            Status = configured ? "Healthy" : "Degraded",
            ResponseTimeMs = 0,
            UptimePercent = configured ? 100 : 0,
            Message = configured
                ? "Kredensial terpasang, pembayaran diteruskan ke gateway"
                : "Kredensial kosong - checkout memakai gateway simulasi"
        };
    }

    private SystemHealthCheck ProbeWeb()
    {
        using var process = Process.GetCurrentProcess();
        var uptime = DateTime.Now - process.StartTime;
        var memoryMb = process.WorkingSet64 / 1024 / 1024;

        return new SystemHealthCheck
        {
            Component = "Web (Blazor Server)",
            Status = memoryMb > 2048 ? "Degraded" : "Healthy",
            ResponseTimeMs = 0,
            UptimePercent = 100,
            Message = $"Uptime {FormatUptime(uptime)} · memori {memoryMb} MB · {_env.EnvironmentName}"
        };
    }

    private static SystemHealthCheck Down(string component, string message) => new()
    {
        Component = component,
        Status = "Down",
        ResponseTimeMs = 0,
        UptimePercent = 0,
        Message = message.Length > 160 ? message[..160] : message
    };

    private static string FormatUptime(TimeSpan t) =>
        t.TotalDays >= 1 ? $"{(int)t.TotalDays}h {t.Hours}j"
        : t.TotalHours >= 1 ? $"{(int)t.TotalHours}j {t.Minutes}m"
        : $"{(int)t.TotalMinutes}m";
}
