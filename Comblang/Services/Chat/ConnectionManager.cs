using System.Collections.Concurrent;

namespace Comblang.Services.Chat;

/// <summary>
/// Tracks online users across SignalR connections. Thread-safe singleton.
/// </summary>
public class ConnectionManager
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

    public void AddConnection(string userId, string connectionId)
    {
        _userConnections.AddOrUpdate(userId,
            _ => new HashSet<string> { connectionId },
            (_, connections) =>
            {
                lock (connections) { connections.Add(connectionId); }
                return connections;
            });
    }

    public void RemoveConnection(string userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                    _userConnections.TryRemove(userId, out _);
            }
        }
    }

    public bool IsOnline(string userId)
    {
        return _userConnections.ContainsKey(userId) && _userConnections[userId].Count > 0;
    }

    public HashSet<string>? GetConnections(string userId)
    {
        return _userConnections.TryGetValue(userId, out var connections) ? connections : null;
    }

    public IReadOnlyList<string> GetOnlineUsers()
    {
        return _userConnections.Keys.ToList().AsReadOnly();
    }
}
