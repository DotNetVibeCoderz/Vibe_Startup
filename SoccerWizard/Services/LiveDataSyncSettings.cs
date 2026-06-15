namespace SoccerWizard.Services;

/// <summary>
/// Konfigurasi sinkronisasi live data yang bisa diubah via UI.
/// </summary>
public class LiveDataSyncSettings
{
    public bool SyncLeagues { get; set; } = true;
    public bool SyncTeams { get; set; } = true;
    public bool SyncMatches { get; set; } = true;
    public bool SyncStandings { get; set; } = true;

    public int PastDays { get; set; } = 7;
    public int FutureDays { get; set; } = 7;

    public bool EnableBackgroundSync { get; set; } = false;
    public int BackgroundIntervalMinutes { get; set; } = 60;

    public DateTime? LastRunUtc { get; set; }

    public void ApplyFrom(LiveDataSyncSettings other)
    {
        SyncLeagues = other.SyncLeagues;
        SyncTeams = other.SyncTeams;
        SyncMatches = other.SyncMatches;
        SyncStandings = other.SyncStandings;
        PastDays = other.PastDays;
        FutureDays = other.FutureDays;
        EnableBackgroundSync = other.EnableBackgroundSync;
        BackgroundIntervalMinutes = other.BackgroundIntervalMinutes;
    }

    public LiveDataSyncSettings Clone() => new()
    {
        SyncLeagues = SyncLeagues,
        SyncTeams = SyncTeams,
        SyncMatches = SyncMatches,
        SyncStandings = SyncStandings,
        PastDays = PastDays,
        FutureDays = FutureDays,
        EnableBackgroundSync = EnableBackgroundSync,
        BackgroundIntervalMinutes = BackgroundIntervalMinutes,
        LastRunUtc = LastRunUtc
    };
}
