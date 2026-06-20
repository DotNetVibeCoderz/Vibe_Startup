using System.Collections.Concurrent;

namespace PDA.Services;

/// <summary>
/// In-memory monitoring service for tracking system metrics.
/// For production, consider using a time-series database or Redis.
/// </summary>
public class PdaMonitoringService
{
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentQueue<MetricPoint> _tokenUsage = new();
    private readonly ConcurrentQueue<MetricPoint> _queryExecutions = new();
    private readonly ConcurrentQueue<MetricPoint> _chatMessages = new();
    private readonly ConcurrentQueue<MetricPoint> _httpRequests = new();
    private readonly ConcurrentDictionary<string, int> _activeUsers = new();

    private long _totalTokens;
    private long _totalQueries;
    private long _totalChats;
    private long _totalRequests;

    public void RecordTokenUsage(string provider, int tokenCount)
    {
        Interlocked.Add(ref _totalTokens, tokenCount);
        _tokenUsage.Enqueue(new MetricPoint { Timestamp = DateTime.UtcNow, Value = tokenCount, Label = provider });
        IncrementCounter($"tokens:{provider}");
    }

    public void RecordQuery()
    {
        Interlocked.Increment(ref _totalQueries);
        _queryExecutions.Enqueue(new MetricPoint { Timestamp = DateTime.UtcNow, Value = 1 });
        IncrementCounter("queries:total");
    }

    public void RecordChatMessage(string provider, int tokens)
    {
        Interlocked.Increment(ref _totalChats);
        _chatMessages.Enqueue(new MetricPoint { Timestamp = DateTime.UtcNow, Value = tokens, Label = provider });
        IncrementCounter("chats:total");
    }

    public void RecordHttpRequest(string path)
    {
        Interlocked.Increment(ref _totalRequests);
        _httpRequests.Enqueue(new MetricPoint { Timestamp = DateTime.UtcNow, Value = 1, Label = path });
        IncrementCounter("requests:total");
    }

    public void RecordUserActivity(string userId)
    {
        _activeUsers[userId] = Environment.TickCount;
    }

    public MonitoringSnapshot GetSnapshot()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        while (_tokenUsage.TryPeek(out var p) && p.Timestamp < cutoff) _tokenUsage.TryDequeue(out _);
        while (_queryExecutions.TryPeek(out var p) && p.Timestamp < cutoff) _queryExecutions.TryDequeue(out _);
        while (_chatMessages.TryPeek(out var p) && p.Timestamp < cutoff) _chatMessages.TryDequeue(out _);
        while (_httpRequests.TryPeek(out var p) && p.Timestamp < cutoff) _httpRequests.TryDequeue(out _);

        var activeUsers = _activeUsers
            .Where(kv => Environment.TickCount - kv.Value < 15 * 60 * 1000)
            .Select(kv => kv.Key)
            .ToList();

        return new MonitoringSnapshot
        {
            TotalTokens = _totalTokens,
            TotalQueries = _totalQueries,
            TotalChats = _totalChats,
            TotalRequests = _totalRequests,
            ActiveUsers = activeUsers.Count,
            ActiveUserIds = activeUsers,
            TokenUsageLastHour = _tokenUsage.Count,
            QueriesLastHour = _queryExecutions.Count,
            ChatsLastHour = _chatMessages.Count,
            RequestsLastHour = _httpRequests.Count,
            Timestamp = DateTime.UtcNow
        };
    }

    private void IncrementCounter(string key)
    {
        _counters.AddOrUpdate(key, 1, (_, v) => v + 1);
    }
}

public class MetricPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string? Label { get; set; }
}

public class MonitoringSnapshot
{
    public long TotalTokens { get; set; }
    public long TotalQueries { get; set; }
    public long TotalChats { get; set; }
    public long TotalRequests { get; set; }
    public int ActiveUsers { get; set; }
    public List<string> ActiveUserIds { get; set; } = new();
    public int TokenUsageLastHour { get; set; }
    public int QueriesLastHour { get; set; }
    public int ChatsLastHour { get; set; }
    public int RequestsLastHour { get; set; }
    public DateTime Timestamp { get; set; }
}
