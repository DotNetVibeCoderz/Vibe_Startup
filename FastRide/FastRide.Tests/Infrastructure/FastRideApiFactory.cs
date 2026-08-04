using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FastRide.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory.
///
/// Nothing about the pipeline is faked: the same authentication, authorization, rate
/// limiting, caching and EF configuration run as in production. Only the settings differ —
/// a throwaway SQLite file, no sample data, and rate limits high enough that the tests are
/// not throttled by each other.
/// </summary>
public sealed class FastRideApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"fastride-tests-{Guid.NewGuid():N}.db");

    private readonly string _uploadsPath =
        Path.Combine(Path.GetTempPath(), $"fastride-uploads-{Guid.NewGuid():N}");

    /// <summary>Requests per minute allowed on /api/auth. Overridden by the rate limit tests.</summary>
    public int AuthPermitPerMinute { get; init; } = 100_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development so /auth/forgot-password returns the reset code, which is the only way
        // to exercise the reset flow without an SMTP server.
        builder.UseEnvironment("Development");

        // UseSetting, not ConfigureAppConfiguration.
        //
        // Under minimal hosting, WebApplication.CreateBuilder reads configuration eagerly and
        // the API decides its database, cache and storage providers from it while services
        // are being registered. ConfigureAppConfiguration callbacks run after that point, so
        // their values arrive too late and every test would quietly share the developer's
        // FastRide.db. UseSetting lands in the host configuration before any of it is read.
        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SQLite",
            ["Database:ConnectionStrings:SQLite"] = $"Data Source={_databasePath}",
            ["Database:AutoSeed"] = "false",

            ["Cache:Provider"] = "Memory",

            ["Storage:Provider"] = "FileSystem",
            ["Storage:FileSystem:Path"] = _uploadsPath,
            ["Storage:FileSystem:BaseUrl"] = "/uploads",

            ["Jwt:Secret"] = "FastRide-Integration-Test-Secret-Key-32-Plus",
            ["Jwt:Issuer"] = "FastRide",
            ["Jwt:Audience"] = "FastRide",
            ["Jwt:AccessTokenExpirationMinutes"] = "60",

            ["RateLimiting:AuthPermitPerMinute"] = AuthPermitPerMinute.ToString(),
            ["RateLimiting:GlobalPermitPerMinute"] = "1000000",

            // The test client follows redirects, and a scheme change costs it the
            // Authorization header — every authenticated call would arrive anonymous.
            ["ApiSettings:UseHttpsRedirection"] = "false",

            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Error"
        };

        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);
    }

    /// <summary>Guards against the settings above silently not being applied.</summary>
    public void AssertIsIsolated()
    {
        var config = (IConfiguration)Services.GetRequiredService(typeof(IConfiguration));
        var connectionString = config["Database:ConnectionStrings:SQLite"];

        Assert.Equal($"Data Source={_databasePath}", connectionString);
        Assert.False(config.GetValue("Database:AutoSeed", true));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // SQLite keeps the file handle until its pools are cleared.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        TryDelete(() => File.Delete(_databasePath));
        TryDelete(() => Directory.Delete(_uploadsPath, recursive: true));
    }

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception)
        {
            // Leftover files in the temp directory are not worth failing a test run over.
        }
    }
}
