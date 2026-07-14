using System.Collections.Concurrent;
using System.Diagnostics;
using BlazorViz.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorViz.Services;

/// <summary>Writes audit trail entries; every notable user/system action goes through here.</summary>
public sealed class AuditService(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<AuditService> log)
{
    public async Task LogAsync(string category, string action, string? details = null, string? userName = null, string? ip = null)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            db.AuditLogs.Add(new AuditLog { Category = category, Action = action, Details = details, UserName = userName, IpAddress = ip });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Audit write failed for {Category}/{Action}", category, action);
        }
    }
}

/// <summary>Counts usage events (queries, tokens, chats, API calls) for the analytics dashboard.</summary>
public sealed class UsageService(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<UsageService> log)
{
    public async Task RecordAsync(string kind, double value = 1, string? userName = null, string? meta = null)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            db.UsageMetrics.Add(new UsageMetric { Kind = kind, Value = value, UserName = userName, Meta = meta });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Usage write failed for {Kind}", kind);
        }
    }

    public void Record(string kind, double value = 1, string? userName = null, string? meta = null) =>
        _ = RecordAsync(kind, value, userName, meta);
}

/// <summary>In-memory rolling metrics for the performance dashboard (requests, response times, resources).</summary>
public sealed class PerfMonitor
{
    public sealed record RequestSample(DateTime TimestampUtc, string Path, int StatusCode, double DurationMs);

    private readonly ConcurrentQueue<RequestSample> _samples = new();
    private long _totalRequests;
    private long _totalErrors;

    public void Record(string path, int statusCode, double durationMs)
    {
        Interlocked.Increment(ref _totalRequests);
        if (statusCode >= 500) Interlocked.Increment(ref _totalErrors);
        _samples.Enqueue(new RequestSample(DateTime.UtcNow, path, statusCode, durationMs));
        while (_samples.Count > 5000 && _samples.TryDequeue(out _)) { }
    }

    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long TotalErrors => Interlocked.Read(ref _totalErrors);
    public List<RequestSample> Recent(int max = 5000) => _samples.TakeLast(max).ToList();

    public (double CpuSeconds, double MemoryMb, double UptimeMinutes, int Threads) Resources()
    {
        var p = Process.GetCurrentProcess();
        return (Math.Round(p.TotalProcessorTime.TotalSeconds, 1),
                Math.Round(p.WorkingSet64 / 1024.0 / 1024.0, 1),
                Math.Round((DateTime.Now - p.StartTime).TotalMinutes, 1),
                p.Threads.Count);
    }
}

/// <summary>Middleware that feeds PerfMonitor with request timing.</summary>
public sealed class PerfMiddleware(RequestDelegate next, PerfMonitor monitor)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var path = context.Request.Path.Value ?? "/";
            if (!path.StartsWith("/_blazor") && !path.Contains('.'))
                monitor.Record(path, context.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
        }
    }
}
