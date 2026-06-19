using System.Collections.Concurrent;

namespace Comblang.Services.Chat;

/// <summary>
/// Scoped service that tracks user SignalR connection IDs for real-time messaging.
/// Uses ConcurrentDictionary for thread safety across concurrent hub invocations.
/// </summary>
public class SignalRConnectionManager
{
    /// <summary>
    /// Maps UserId → set of active connection IDs (one user can have multiple tabs/devices).
    /// </summary>
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();

    /// <summary>
    /// Reverse lookup: ConnectionId → UserId.
    /// </summary>
    private readonly ConcurrentDictionary<string, Guid> _connectionUsers = new();

    /// <summary>
    /// Registers a new connection for a user.
    /// Thread-safe: multiple connections from the same user are supported.
    /// </summary>
    public void AddConnection(Guid userId, string connectionId)
    {
        _connectionUsers[connectionId] = userId;

        _userConnections.AddOrUpdate(
            userId,
            _ => new HashSet<string> { connectionId },
            (_, existingSet) =>
            {
                lock (existingSet)
                {
                    existingSet.Add(connectionId);
                }
                return existingSet;
            });
    }

    /// <summary>
    /// Removes a connection and returns the associated user ID if found.
    /// If the user has no more connections, the user entry is cleaned up.
    /// </summary>
    public Guid? RemoveConnection(string connectionId)
    {
        if (!_connectionUsers.TryRemove(connectionId, out Guid userId))
            return null;

        if (_userConnections.TryGetValue(userId, out HashSet<string>? connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                }
            }
        }

        return userId;
    }

    /// <summary>
    /// Returns all active connection IDs for the given user.
    /// Returns an empty list if the user is not connected.
    /// </summary>
    public List<string> GetConnectionIds(Guid userId)
    {
        if (_userConnections.TryGetValue(userId, out HashSet<string>? connections))
        {
            lock (connections)
            {
                return connections.ToList();
            }
        }

        return new List<string>();
    }

    /// <summary>
    /// Returns the user ID associated with a connection ID, or null if not tracked.
    /// </summary>
    public Guid? GetUserId(string connectionId)
    {
        if (_connectionUsers.TryGetValue(connectionId, out Guid userId))
            return userId;

        return null;
    }

    /// <summary>
    /// Checks whether the given user has at least one active connection.
    /// </summary>
    public bool IsUserOnline(Guid userId)
    {
        if (_userConnections.TryGetValue(userId, out HashSet<string>? connections))
        {
            lock (connections)
            {
                return connections.Count > 0;
            }
        }

        return false;
    }
}
