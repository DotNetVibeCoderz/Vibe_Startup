using Microsoft.AspNetCore.SignalR;

namespace SoccerWizard.Hubs;

/// <summary>
/// SignalR Hub untuk update real-time skor dan prediksi
/// </summary>
public class MatchHub : Hub
{
    private static int _connectedUsers;

    /// <summary>
    /// Bergabung ke grup pertandingan spesifik
    /// </summary>
    public async Task JoinMatchGroup(int matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match-{matchId}");
    }
    
    /// <summary>
    /// Meninggalkan grup pertandingan
    /// </summary>
    public async Task LeaveMatchGroup(int matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match-{matchId}");
    }
    
    /// <summary>
    /// Update skor (dipanggil oleh admin/service)
    /// </summary>
    public async Task UpdateScore(int matchId, int homeScore, int awayScore, string status)
    {
        await Clients.Group($"match-{matchId}").SendAsync("ScoreUpdated", new
        {
            MatchId = matchId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Status = status,
            UpdatedAt = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// Update prediksi baru
    /// </summary>
    public async Task NewPrediction(int matchId, object prediction)
    {
        await Clients.Group($"match-{matchId}").SendAsync("PredictionUpdated", prediction);
    }
    
    /// <summary>
    /// Broadcast live match notification
    /// </summary>
    public async Task LiveMatchNotification(int matchId, string message)
    {
        await Clients.All.SendAsync("LiveNotification", new
        {
            MatchId = matchId,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// User online counter
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _connectedUsers);
        await Clients.All.SendAsync("UserCountChanged", _connectedUsers);
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _connectedUsers);
        await Clients.All.SendAsync("UserCountChanged", _connectedUsers);
        await base.OnDisconnectedAsync(exception);
    }
}
