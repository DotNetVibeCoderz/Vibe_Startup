using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using PCHub.Shared.DTOs;
using PCHub.Shared.Interfaces;

namespace PCHub.Shared.Services;

/// <summary>
/// Cache service dengan fallback: Redis → In-Memory.
/// Gunakan IDistributedCache (Redis via StackExchange.Redis) jika tersedia,
/// jika tidak fallback ke IMemoryCache.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly IMemoryCache? _memoryCache;

    public CacheService(IDistributedCache? distributedCache = null, IMemoryCache? memoryCache = null)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        // Try Redis first
        if (_distributedCache != null)
        {
            var data = await _distributedCache.GetStringAsync(key);
            if (data != null)
                return JsonSerializer.Deserialize<T>(data);
        }

        // Fallback to MemoryCache
        if (_memoryCache != null && _memoryCache.TryGetValue(key, out T? value))
            return value;

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        expiry ??= TimeSpan.FromMinutes(10);

        // Redis
        if (_distributedCache != null)
        {
            await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            });
        }

        // Memory fallback
        _memoryCache?.Set(key, value, expiry.Value);
    }

    public async Task RemoveAsync(string key)
    {
        if (_distributedCache != null)
            await _distributedCache.RemoveAsync(key);
        _memoryCache?.Remove(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (_distributedCache != null)
        {
            var data = await _distributedCache.GetStringAsync(key);
            return data != null;
        }
        return _memoryCache?.TryGetValue(key, out _) ?? false;
    }

    public Task<CacheStats> GetStatsAsync()
    {
        return Task.FromResult(new CacheStats(0, 0, 0, 0));
    }
}
