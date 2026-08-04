using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace FastRide.Api.Services;

/// <summary>
/// Thin cache abstraction so the rest of the code does not care whether it is talking to
/// IMemoryCache (single node) or Redis (multiple nodes). Selected by <c>Cache:Provider</c>.
/// </summary>
public interface ICacheService
{
    string Provider { get; }

    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Return the cached value, or run <paramref name="factory"/> and cache its result.</summary>
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default);
}

/// <summary>Cache keys, gathered in one place so invalidation is not guesswork.</summary>
public static class CacheKeys
{
    public const string FareConfigs = "fare:configs";
    public const string DashboardOverview = "dashboard:overview";
    public const string DashboardStats = "dashboard:stats";
    public const string PaymentProviders = "payments:providers";

    public static string SecurityStamp(Guid userId) => $"user:stamp:{userId}";
    public static string PasswordReset(string email) => $"auth:reset:{email.ToLowerInvariant()}";
    public static string DriverProfile(Guid userId) => $"driver:profile:{userId}";
}

/// <summary>In-process cache. Default, and all a single-node deployment needs.</summary>
public sealed class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public string Provider => "Memory";

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
        Task.FromResult(cache.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        if (cache.TryGetValue(key, out var cached) && cached is T hit) return hit;

        var value = await factory(ct);
        cache.Set(key, value, ttl);
        return value;
    }
}

/// <summary>
/// Redis-backed cache. A cache outage must never take the API down, so every failure is
/// logged and treated as a miss.
/// </summary>
public sealed class DistributedCacheService(
    IDistributedCache cache,
    ILogger<DistributedCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Provider => "Redis";

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var payload = await cache.GetStringAsync(key, ct);
            return payload is null ? default : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}; treating as a miss.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            await cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(value, SerializerOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache eviction failed for {Key}.", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory(ct);
        await SetAsync(key, value, ttl, ct);
        return value;
    }
}
