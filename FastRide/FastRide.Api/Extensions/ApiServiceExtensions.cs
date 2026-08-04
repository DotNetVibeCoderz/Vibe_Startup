using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FastRide.Api.Infrastructure;
using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.Models;
using FastRide.Shared.Storage;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;

namespace FastRide.Api.Extensions;

/// <summary>Composition root, split by concern so Program.cs reads as a table of contents.</summary>
public static class ApiServiceExtensions
{
    /// <summary>SQLite, SQL Server, PostgreSQL or MySQL, chosen by <c>Database:Provider</c>.</summary>
    public static IServiceCollection AddFastRideDatabase(this IServiceCollection services, IConfiguration config)
    {
        var provider = (config["Database:Provider"] ?? "sqlite").ToLowerInvariant();

        var connectionString = provider switch
        {
            "sqlserver" or "mssql" => config["Database:ConnectionStrings:SqlServer"],
            "postgresql" or "postgres" or "npgsql" => config["Database:ConnectionStrings:PostgreSQL"],
            "mysql" => config["Database:ConnectionStrings:MySQL"],
            _ => config["Database:ConnectionStrings:SQLite"]
        } ?? "Data Source=FastRide.db";

        // Pooling reuses context instances instead of allocating one per request.
        services.AddDbContextPool<FastRideDbContext>(options =>
        {
            switch (provider)
            {
                case "sqlserver" or "mssql":
                    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
                    break;
                case "postgresql" or "postgres" or "npgsql":
                    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
                    break;
                case "mysql":
                    options.UseMySQL(connectionString);
                    break;
                default:
                    options.UseSqlite(connectionString);
                    break;
            }

            // Reads dominate this API; tracking every projected row costs memory for nothing.
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

            // Several endpoints fetch a count and a sum in one round trip with
            // `GroupBy(_ => 1).Select(...).FirstOrDefaultAsync()`. That grouping yields at
            // most one row by construction, so EF's "First without OrderBy" warning is noise
            // here — silenced deliberately rather than left to bury real warnings.
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.FirstWithoutOrderByAndFilterWarning));
        });

        return services;
    }

    /// <summary>IMemoryCache by default, Redis when <c>Cache:Provider</c> says so.</summary>
    public static IServiceCollection AddFastRideCache(this IServiceCollection services, IConfiguration config)
    {
        var provider = (config["Cache:Provider"] ?? "memory").ToLowerInvariant();
        var connectionString = config["Cache:Redis:ConnectionString"];

        if (provider is "redis" && !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
                options.InstanceName = config["Cache:Redis:InstanceName"] ?? "fastride:";
            });

            services.AddSingleton<ICacheService, DistributedCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }

    public static IServiceCollection AddFastRideStorage(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IStorageProvider>(StorageProviderFactory.Create);
        return services;
    }

    public static IServiceCollection AddFastRideAuth(this IServiceCollection services, IConfiguration config)
    {
        var secret = config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be set to at least 32 characters. See docs/AUTH.md.");

        services.AddSingleton<TokenService>();

        services.AddAuthentication("Bearer").AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"] ?? "FastRide",
                ValidAudience = config["Jwt:Audience"] ?? "FastRide",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                // Default is five minutes of leeway, which makes short-lived tokens confusing to test.
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(Policies.DriverOnly, policy => policy.RequireRole(nameof(UserRole.Driver), nameof(UserRole.Admin)))
            .AddPolicy(Policies.RiderOnly, policy => policy.RequireRole(nameof(UserRole.Rider), nameof(UserRole.Admin)))
            .AddPolicy(Policies.StaffOrDriver, policy => policy.RequireRole(nameof(UserRole.Driver), nameof(UserRole.Admin)));

        return services;
    }

    public static IServiceCollection AddFastRideCors(this IServiceCollection services, IConfiguration config)
    {
        var origins = config.GetSection("ApiSettings:CorsOrigins").Get<string[]>()
                      ?? ["https://localhost:5002", "http://localhost:5003"];

        services.AddCors(options => options.AddPolicy("AllowClients", policy => policy
            .WithOrigins(origins)
            .AllowAnyMethod()
            .AllowAnyHeader()));

        return services;
    }

    /// <summary>
    /// Throttles the endpoints worth attacking. Login and password reset get a tight window;
    /// everything else gets a generous per-client cap so one misbehaving app cannot starve
    /// the rest.
    /// </summary>
    public static IServiceCollection AddFastRideRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var authPermits = config.GetValue("RateLimiting:AuthPermitPerMinute", 30);
        var globalPermits = config.GetValue("RateLimiting:GlobalPermitPerMinute", 300);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermits,
                    Window = TimeSpan.FromMinutes(1)
                }));

            // Provider callbacks are anonymous by necessity, so they get their own ceiling.
            // It has to be generous: a provider retrying a burst of legitimate callbacks must
            // not be turned away, or payments would go unrecorded.
            options.AddPolicy("webhook", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "webhook",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = config.GetValue("RateLimiting:WebhookPermitPerMinute", 600),
                    Window = TimeSpan.FromMinutes(1)
                }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalPermits,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ApiError("TooManyRequests", "Terlalu banyak permintaan. Coba lagi sebentar lagi."), ct);
            };
        });

        return services;
    }

    /// <summary>Partition by user when signed in, otherwise by remote address.</summary>
    private static string ClientKey(HttpContext context) =>
        context.User.UserId()?.ToString()
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    /// <summary>
    /// Enums travel as strings so payloads are readable and a reordered enum cannot silently
    /// change meaning. Nulls are dropped to keep mobile responses small.
    /// </summary>
    public static IServiceCollection AddFastRideJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }
}
