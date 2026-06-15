using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoccerWizard.Data;
using SoccerWizard.Models;

namespace SoccerWizard.Services;

/// <summary>
/// Service untuk manajemen pertandingan dan data sepak bola
/// </summary>
public class MatchService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LiveDataSyncSettings _syncSettings;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MatchService(IDbContextFactory<AppDbContext> dbFactory, IHttpClientFactory httpClientFactory, LiveDataSyncSettings syncSettings)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _syncSettings = syncSettings;
    }

    /// <summary>
    /// Mendapatkan semua pertandingan dengan filter opsional
    /// </summary>
    public async Task<List<Match>> GetMatchesAsync(string? status = null, int? leagueId = null, int limit = 20)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.League)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(m => m.Status == status);

        if (leagueId.HasValue)
            query = query.Where(m => m.LeagueId == leagueId.Value);

        return await query
            .OrderByDescending(m => m.MatchDate)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Mendapatkan live matches
    /// </summary>
    public async Task<List<Match>> GetLiveMatchesAsync()
    {
        return await GetMatchesAsync(status: "LIVE", limit: 50);
    }

    /// <summary>
    /// Mendapatkan upcoming matches
    /// </summary>
    public async Task<List<Match>> GetUpcomingMatchesAsync(int limit = 20)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.League)
            .Where(m => m.Status == "SCHEDULED" && m.MatchDate > DateTime.UtcNow)
            .OrderBy(m => m.MatchDate)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Mendapatkan semua tim
    /// </summary>
    public async Task<List<Team>> GetTeamsAsync(int? leagueId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Teams.Include(t => t.League).AsQueryable();

        if (leagueId.HasValue)
            query = query.Where(t => t.LeagueId == leagueId.Value);

        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    /// <summary>
    /// Mendapatkan tim berdasarkan ID
    /// </summary>
    public async Task<Team?> GetTeamAsync(int teamId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Teams
            .Include(t => t.League)
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == teamId);
    }

    /// <summary>
    /// Mendapatkan semua liga
    /// </summary>
    public async Task<List<League>> GetLeaguesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Leagues.OrderBy(l => l.Name).ToListAsync();
    }

    /// <summary>
    /// Mendapatkan pemain berdasarkan tim
    /// </summary>
    public async Task<List<Player>> GetPlayersByTeamAsync(int teamId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Players
            .Where(p => p.TeamId == teamId)
            .OrderByDescending(p => p.Rating)
            .ToListAsync();
    }

    /// <summary>
    /// Mendapatkan head-to-head antara dua tim
    /// </summary>
    public async Task<HeadToHead?> GetHeadToHeadAsync(int team1Id, int team2Id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.HeadToHeads
            .Include(h => h.Team1)
            .Include(h => h.Team2)
            .FirstOrDefaultAsync(h =>
                (h.Team1Id == team1Id && h.Team2Id == team2Id) ||
                (h.Team1Id == team2Id && h.Team2Id == team1Id));
    }

    /// <summary>
    /// Mendapatkan berita terbaru
    /// </summary>
    public async Task<List<NewsArticle>> GetLatestNewsAsync(int limit = 10)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.NewsArticles
            .Include(n => n.Team)
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Mendapatkan berita berdasarkan tim
    /// </summary>
    public async Task<List<NewsArticle>> GetNewsByTeamAsync(int teamId, int limit = 10)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.NewsArticles
            .Include(n => n.Team)
            .Where(n => n.TeamId == teamId)
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Mendapatkan statistik dashboard
    /// </summary>
    public async Task<Dictionary<string, object>> GetDashboardStatsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return new Dictionary<string, object>
        {
            ["TotalMatches"] = await db.Matches.CountAsync(),
            ["LiveMatches"] = await db.Matches.CountAsync(m => m.Status == "LIVE"),
            ["UpcomingMatches"] = await db.Matches.CountAsync(m => m.Status == "SCHEDULED"),
            ["TotalTeams"] = await db.Teams.CountAsync(),
            ["TotalLeagues"] = await db.Leagues.CountAsync(),
            ["TotalPredictions"] = await db.Predictions.CountAsync(),
            ["CorrectPredictions"] = await db.Predictions.CountAsync(p => p.IsCorrect == true),
            ["TotalNews"] = await db.NewsArticles.CountAsync(),
            ["TopTeam"] = (await db.Teams.OrderByDescending(t => t.EloRating).FirstOrDefaultAsync())?.Name ?? "N/A",
            ["AvgGoalsPerMatch"] = await db.Matches
                .Where(m => m.Status == "FINISHED")
                .AverageAsync(m => (m.HomeScore ?? 0) + (m.AwayScore ?? 0))
        };
    }

    /// <summary>
    /// Sinkronisasi data live dari api.football-data.org
    /// </summary>
    public async Task<string> SyncLiveDataAsync(LiveDataSyncSettings? overrideSettings = null)
    {
        var settings = overrideSettings ?? _syncSettings;
        if (!settings.SyncLeagues && !settings.SyncTeams && !settings.SyncMatches && !settings.SyncStandings)
            return "Sync skipped: pilih minimal satu jenis data.";

        var client = _httpClientFactory.CreateClient("FootballData");
        var competitionsResponse = await client.GetFromJsonAsync<FootballDataCompetitionResponse>("competitions", _jsonOptions);

        if (competitionsResponse?.Competitions is null || competitionsResponse.Competitions.Count == 0)
            return "Sync failed: data kompetisi tidak tersedia dari API.";

        await using var db = await _dbFactory.CreateDbContextAsync();

        var leagueByName = await db.Leagues.ToDictionaryAsync(l => l.Name, StringComparer.OrdinalIgnoreCase);
        var teamByName = await db.Teams.Include(t => t.League).ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        int leaguesUpserted = 0;
        int teamsUpserted = 0;
        int matchesUpserted = 0;
        int standingsUpdated = 0;

        foreach (var competition in competitionsResponse.Competitions)
        {
            var league = EnsureLeague(db, leagueByName, competition, settings.SyncLeagues, ref leaguesUpserted);

            if (settings.SyncTeams)
            {
                var teamsResponse = await client.GetFromJsonAsync<FootballDataTeamsResponse>($"competitions/{competition.Id}/teams", _jsonOptions);
                if (teamsResponse?.Teams is not null)
                {
                    foreach (var teamDto in teamsResponse.Teams)
                    {
                        var team = EnsureTeam(db, teamByName, teamDto, league, ref teamsUpserted);
                        if (league is not null)
                            team.League = league;
                    }

                    if (league is not null)
                        league.TotalTeams = teamsResponse.Teams.Count;
                }
            }

            if (settings.SyncStandings)
            {
                var standingsResponse = await client.GetFromJsonAsync<FootballDataStandingsResponse>($"competitions/{competition.Id}/standings", _jsonOptions);
                var table = standingsResponse?.Standings
                    ?.FirstOrDefault(s => string.Equals(s.Type, "TOTAL", StringComparison.OrdinalIgnoreCase))
                    ?.Table ?? standingsResponse?.Standings?.FirstOrDefault()?.Table;

                if (table is not null)
                {
                    foreach (var row in table)
                    {
                        if (!teamByName.TryGetValue(row.Team.Name, out var team))
                            team = EnsureTeam(db, teamByName, new FootballDataTeamDto { Name = row.Team.Name }, league, ref teamsUpserted);

                        team.MatchesPlayed = row.PlayedGames;
                        team.Wins = row.Won;
                        team.Draws = row.Draw;
                        team.Losses = row.Lost;
                        team.GoalsFor = row.GoalsFor;
                        team.GoalsAgainst = row.GoalsAgainst;
                        team.AvgGoalsScored = row.PlayedGames > 0 ? Math.Round((double)row.GoalsFor / row.PlayedGames, 2) : 0;
                        team.AvgGoalsConceded = row.PlayedGames > 0 ? Math.Round((double)row.GoalsAgainst / row.PlayedGames, 2) : 0;
                        team.UpdatedAt = DateTime.UtcNow;
                        standingsUpdated++;
                    }
                }
            }

            if (settings.SyncMatches)
            {
                var dateFrom = DateTime.UtcNow.Date.AddDays(-Math.Abs(settings.PastDays));
                var dateTo = DateTime.UtcNow.Date.AddDays(Math.Abs(settings.FutureDays));
                var matchesResponse = await client.GetFromJsonAsync<FootballDataMatchesResponse>(
                    $"competitions/{competition.Id}/matches?dateFrom={dateFrom:yyyy-MM-dd}&dateTo={dateTo:yyyy-MM-dd}",
                    _jsonOptions);

                if (matchesResponse?.Matches is not null)
                {
                    foreach (var matchDto in matchesResponse.Matches)
                    {
                        var homeTeam = EnsureTeam(db, teamByName, new FootballDataTeamDto { Name = matchDto.HomeTeam.Name }, league, ref teamsUpserted);
                        var awayTeam = EnsureTeam(db, teamByName, new FootballDataTeamDto { Name = matchDto.AwayTeam.Name }, league, ref teamsUpserted);

                        Match? matchEntity = null;
                        if (homeTeam.Id > 0 && awayTeam.Id > 0)
                        {
                            var date = matchDto.UtcDate.Date;
                            matchEntity = await db.Matches.FirstOrDefaultAsync(m =>
                                m.HomeTeamId == homeTeam.Id &&
                                m.AwayTeamId == awayTeam.Id &&
                                m.MatchDate >= date &&
                                m.MatchDate < date.AddDays(1));
                        }

                        if (matchEntity == null)
                        {
                            matchEntity = new Match
                            {
                                HomeTeam = homeTeam,
                                AwayTeam = awayTeam,
                                League = league,
                                CreatedAt = DateTime.UtcNow
                            };
                            db.Matches.Add(matchEntity);
                        }

                        matchEntity.MatchDate = matchDto.UtcDate;
                        matchEntity.Status = MapMatchStatus(matchDto.Status);
                        matchEntity.HomeScore = matchDto.Score?.FullTime?.Home ?? matchDto.Score?.HalfTime?.Home;
                        matchEntity.AwayScore = matchDto.Score?.FullTime?.Away ?? matchDto.Score?.HalfTime?.Away;
                        matchEntity.HomeHalfTimeScore = matchDto.Score?.HalfTime?.Home;
                        matchEntity.AwayHalfTimeScore = matchDto.Score?.HalfTime?.Away;
                        matchEntity.Venue = matchDto.Venue ?? matchEntity.Venue;
                        matchEntity.Referee = matchDto.Referees?.FirstOrDefault()?.Name ?? matchEntity.Referee;
                        matchEntity.Round = BuildRound(matchDto);
                        matchEntity.UpdatedAt = DateTime.UtcNow;
                        matchesUpserted++;
                    }
                }
            }
        }

        await db.SaveChangesAsync();
        settings.LastRunUtc = DateTime.UtcNow;

        var summary = new List<string>();
        if (settings.SyncLeagues) summary.Add($"{leaguesUpserted} league(s)");
        if (settings.SyncTeams) summary.Add($"{teamsUpserted} team(s)");
        if (settings.SyncMatches) summary.Add($"{matchesUpserted} match(es)");
        if (settings.SyncStandings) summary.Add($"{standingsUpdated} standings row(s)");

        return summary.Count == 0
            ? "Sync selesai, tidak ada perubahan."
            : $"Sync selesai: {string.Join(", ", summary)}.";
    }

    /// <summary>
    /// Sinkronisasi manual data dari file excel yang diupload admin.
    /// </summary>
    public async Task<string> SyncManualDataFromExcelAsync(Stream excelStream)
    {
        using var workbook = new XLWorkbook(excelStream);
        await using var db = await _dbFactory.CreateDbContextAsync();

        var leagueByName = await db.Leagues.ToDictionaryAsync(l => l.Name, StringComparer.OrdinalIgnoreCase);
        var teamByName = await db.Teams.Include(t => t.League).ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        int leaguesUpserted = 0;
        int teamsUpserted = 0;
        int matchesUpserted = 0;
        int standingsUpdated = 0;

        if (workbook.TryGetWorksheet("Leagues", out var leaguesSheet))
        {
            foreach (var row in leaguesSheet.RowsUsed().Skip(1))
            {
                var name = GetString(row.Cell(1));
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!leagueByName.TryGetValue(name, out var league))
                {
                    league = new League
                    {
                        Name = name,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Leagues.Add(league);
                    leagueByName[name] = league;
                }

                league.Country = GetStringOrFallback(row.Cell(2), league.Country);
                league.LogoUrl = GetStringOrFallback(row.Cell(3), league.LogoUrl);
                league.Season = GetStringOrFallback(row.Cell(4), league.Season);
                league.TotalTeams = GetIntOrFallback(row.Cell(5), league.TotalTeams);
                league.TotalRounds = GetIntOrFallback(row.Cell(6), league.TotalRounds);
                league.Description = GetStringOrFallback(row.Cell(7), league.Description);

                leaguesUpserted++;
            }
        }

        if (workbook.TryGetWorksheet("Teams", out var teamsSheet))
        {
            foreach (var row in teamsSheet.RowsUsed().Skip(1))
            {
                var name = GetString(row.Cell(1));
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var leagueName = GetString(row.Cell(10));
                League? league = null;
                if (!string.IsNullOrWhiteSpace(leagueName) && !leagueByName.TryGetValue(leagueName, out league))
                {
                    league = new League { Name = leagueName, CreatedAt = DateTime.UtcNow };
                    db.Leagues.Add(league);
                    leagueByName[leagueName] = league;
                }

                var team = EnsureTeamManual(db, teamByName, name, league, ref teamsUpserted);
                team.ShortName = GetStringOrFallback(row.Cell(2), team.ShortName);
                team.Code = GetStringOrFallback(row.Cell(3), team.Code);
                team.LogoUrl = GetStringOrFallback(row.Cell(4), team.LogoUrl);
                team.Country = GetStringOrFallback(row.Cell(5), team.Country);
                team.City = GetStringOrFallback(row.Cell(6), team.City);
                team.Stadium = GetStringOrFallback(row.Cell(7), team.Stadium);
                team.FoundedYear = GetIntOrFallback(row.Cell(8), team.FoundedYear);
                team.Description = GetStringOrFallback(row.Cell(9), team.Description);
                team.EloRating = GetDoubleOrFallback(row.Cell(11), team.EloRating);
                team.AttackStrength = GetDoubleOrFallback(row.Cell(12), team.AttackStrength);
                team.DefenseStrength = GetDoubleOrFallback(row.Cell(13), team.DefenseStrength);
                team.MidfieldStrength = GetDoubleOrFallback(row.Cell(14), team.MidfieldStrength);
                team.Momentum = GetDoubleOrFallback(row.Cell(15), team.Momentum);
                team.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (workbook.TryGetWorksheet("Matches", out var matchesSheet))
        {
            foreach (var row in matchesSheet.RowsUsed().Skip(1))
            {
                var homeTeamName = GetString(row.Cell(1));
                var awayTeamName = GetString(row.Cell(2));
                if (string.IsNullOrWhiteSpace(homeTeamName) || string.IsNullOrWhiteSpace(awayTeamName))
                    continue;

                var leagueName = GetString(row.Cell(3));
                League? league = null;
                if (!string.IsNullOrWhiteSpace(leagueName) && !leagueByName.TryGetValue(leagueName, out league))
                {
                    league = new League { Name = leagueName, CreatedAt = DateTime.UtcNow };
                    db.Leagues.Add(league);
                    leagueByName[leagueName] = league;
                }

                var homeTeam = EnsureTeamManual(db, teamByName, homeTeamName, league, ref teamsUpserted);
                var awayTeam = EnsureTeamManual(db, teamByName, awayTeamName, league, ref teamsUpserted);

                var matchDate = GetDateTime(row.Cell(4));
                if (!matchDate.HasValue)
                    continue;

                var matchEntity = await db.Matches.FirstOrDefaultAsync(m =>
                    m.HomeTeamId == homeTeam.Id &&
                    m.AwayTeamId == awayTeam.Id &&
                    m.MatchDate >= matchDate.Value.Date &&
                    m.MatchDate < matchDate.Value.Date.AddDays(1));

                if (matchEntity == null)
                {
                    matchEntity = new Match
                    {
                        HomeTeam = homeTeam,
                        AwayTeam = awayTeam,
                        League = league,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Matches.Add(matchEntity);
                }

                matchEntity.MatchDate = matchDate.Value;
                matchEntity.Status = GetStringOrFallback(row.Cell(5), matchEntity.Status);
                matchEntity.Venue = GetStringOrFallback(row.Cell(6), matchEntity.Venue);
                matchEntity.Referee = GetStringOrFallback(row.Cell(7), matchEntity.Referee);
                matchEntity.HomeScore = GetNullableInt(row.Cell(8)) ?? matchEntity.HomeScore;
                matchEntity.AwayScore = GetNullableInt(row.Cell(9)) ?? matchEntity.AwayScore;
                matchEntity.HomeHalfTimeScore = GetNullableInt(row.Cell(10)) ?? matchEntity.HomeHalfTimeScore;
                matchEntity.AwayHalfTimeScore = GetNullableInt(row.Cell(11)) ?? matchEntity.AwayHalfTimeScore;
                matchEntity.Round = GetStringOrFallback(row.Cell(12), matchEntity.Round);
                matchEntity.UpdatedAt = DateTime.UtcNow;

                matchesUpserted++;
            }
        }

        if (workbook.TryGetWorksheet("Standings", out var standingsSheet))
        {
            foreach (var row in standingsSheet.RowsUsed().Skip(1))
            {
                var teamName = GetString(row.Cell(1));
                if (string.IsNullOrWhiteSpace(teamName))
                    continue;

                var leagueName = GetString(row.Cell(2));
                League? league = null;
                if (!string.IsNullOrWhiteSpace(leagueName) && !leagueByName.TryGetValue(leagueName, out league))
                {
                    league = new League { Name = leagueName, CreatedAt = DateTime.UtcNow };
                    db.Leagues.Add(league);
                    leagueByName[leagueName] = league;
                }

                var team = EnsureTeamManual(db, teamByName, teamName, league, ref teamsUpserted);
                team.MatchesPlayed = GetIntOrFallback(row.Cell(3), team.MatchesPlayed);
                team.Wins = GetIntOrFallback(row.Cell(4), team.Wins);
                team.Draws = GetIntOrFallback(row.Cell(5), team.Draws);
                team.Losses = GetIntOrFallback(row.Cell(6), team.Losses);
                team.GoalsFor = GetIntOrFallback(row.Cell(7), team.GoalsFor);
                team.GoalsAgainst = GetIntOrFallback(row.Cell(8), team.GoalsAgainst);
                team.AvgGoalsScored = GetDoubleOrFallback(row.Cell(9), team.AvgGoalsScored);
                team.AvgGoalsConceded = GetDoubleOrFallback(row.Cell(10), team.AvgGoalsConceded);
                team.UpdatedAt = DateTime.UtcNow;

                standingsUpdated++;
            }
        }

        await db.SaveChangesAsync();

        var summary = new List<string>();
        if (leaguesUpserted > 0) summary.Add($"{leaguesUpserted} league(s)");
        if (teamsUpserted > 0) summary.Add($"{teamsUpserted} team(s)");
        if (matchesUpserted > 0) summary.Add($"{matchesUpserted} match(es)");
        if (standingsUpdated > 0) summary.Add($"{standingsUpdated} standings row(s)");

        return summary.Count == 0
            ? "Sync manual selesai, tidak ada perubahan."
            : $"Sync manual selesai: {string.Join(", ", summary)}.";
    }

    private static League? EnsureLeague(AppDbContext db, Dictionary<string, League> leagueByName, FootballDataCompetitionDto competition, bool allowCreate, ref int leaguesUpserted)
    {
        if (!leagueByName.TryGetValue(competition.Name, out var league))
        {
            if (!allowCreate)
                return null;

            league = new League
            {
                Name = competition.Name,
                Country = competition.Area?.Name ?? string.Empty,
                LogoUrl = competition.Emblem ?? string.Empty,
                Season = FormatSeason(competition.CurrentSeason) ?? string.Empty,
                TotalRounds = competition.CurrentSeason?.CurrentMatchday ?? 0,
                Description = $"Synced from football-data.org - {competition.Name}",
                CreatedAt = DateTime.UtcNow
            };
            db.Leagues.Add(league);
            leagueByName[competition.Name] = league;
            leaguesUpserted++;
            return league;
        }

        if (allowCreate)
        {
            league.Country = competition.Area?.Name ?? league.Country;
            league.LogoUrl = competition.Emblem ?? league.LogoUrl;
            league.Season = FormatSeason(competition.CurrentSeason) ?? league.Season;
            league.TotalRounds = competition.CurrentSeason?.CurrentMatchday ?? league.TotalRounds;
            leaguesUpserted++;
        }

        return league;
    }

    private static Team EnsureTeam(AppDbContext db, Dictionary<string, Team> teamByName, FootballDataTeamDto teamDto, League? league, ref int teamsUpserted)
    {
        if (teamByName.TryGetValue(teamDto.Name, out var team))
        {
            team.ShortName = teamDto.ShortName ?? team.ShortName;
            team.Code = teamDto.Tla ?? team.Code;
            team.LogoUrl = teamDto.Crest ?? team.LogoUrl;
            team.Country = teamDto.Area?.Name ?? team.Country;
            team.Stadium = teamDto.Venue ?? team.Stadium;
            team.FoundedYear = teamDto.Founded ?? team.FoundedYear;
            if (league is not null)
                team.League = league;
            team.UpdatedAt = DateTime.UtcNow;
            teamsUpserted++;
            return team;
        }

        team = new Team
        {
            Name = teamDto.Name,
            ShortName = teamDto.ShortName ?? teamDto.Name,
            Code = teamDto.Tla ?? string.Empty,
            LogoUrl = teamDto.Crest ?? string.Empty,
            Country = teamDto.Area?.Name ?? string.Empty,
            Stadium = teamDto.Venue ?? string.Empty,
            FoundedYear = teamDto.Founded ?? 0,
            League = league,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Teams.Add(team);
        teamByName[team.Name] = team;
        teamsUpserted++;
        return team;
    }

    private static Team EnsureTeamManual(AppDbContext db, Dictionary<string, Team> teamByName, string teamName, League? league, ref int teamsUpserted)
    {
        if (teamByName.TryGetValue(teamName, out var team))
        {
            if (league is not null)
                team.League = league;
            team.UpdatedAt = DateTime.UtcNow;
            teamsUpserted++;
            return team;
        }

        team = new Team
        {
            Name = teamName,
            ShortName = teamName,
            League = league,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Teams.Add(team);
        teamByName[teamName] = team;
        teamsUpserted++;
        return team;
    }

    private static string MapMatchStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "IN_PLAY" or "PAUSED" or "LIVE" => "LIVE",
            "FINISHED" => "FINISHED",
            "POSTPONED" => "POSTPONED",
            "CANCELED" or "CANCELLED" => "CANCELLED",
            "SUSPENDED" => "SUSPENDED",
            _ => "SCHEDULED"
        };
    }

    private static string BuildRound(FootballDataMatchDto matchDto)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(matchDto.Stage)) parts.Add(matchDto.Stage);
        if (!string.IsNullOrWhiteSpace(matchDto.Group)) parts.Add(matchDto.Group);
        if (matchDto.Matchday.HasValue) parts.Add($"MD {matchDto.Matchday}");
        return parts.Count == 0 ? string.Empty : string.Join(" - ", parts);
    }

    private static string? FormatSeason(FootballDataSeasonDto? season)
    {
        if (season is null) return null;
        if (DateTime.TryParse(season.StartDate, out var start) && DateTime.TryParse(season.EndDate, out var end))
            return start.Year == end.Year ? start.Year.ToString() : $"{start.Year}/{end.Year}";
        if (!string.IsNullOrWhiteSpace(season.StartDate) && season.StartDate.Length >= 4)
            return season.StartDate[..4];
        return null;
    }

    /// <summary>
    /// Update skor pertandingan (simulasi real-time)
    /// </summary>
    public async Task UpdateMatchScoreAsync(int matchId, int homeScore, int awayScore, string status = "LIVE")
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var match = await db.Matches.FindAsync(matchId);
        if (match != null)
        {
            match.HomeScore = homeScore;
            match.AwayScore = awayScore;
            match.Status = status;
            match.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private static string GetString(IXLCell cell)
    {
        return cell.GetValue<string>().Trim();
    }

    private static string GetStringOrFallback(IXLCell cell, string fallback)
    {
        var value = GetString(cell);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int GetIntOrFallback(IXLCell cell, int fallback)
    {
        var value = GetNullableInt(cell);
        return value ?? fallback;
    }

    private static int? GetNullableInt(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.TryGetValue(out int value))
            return value;

        if (int.TryParse(cell.GetValue<string>(), out value))
            return value;

        return null;
    }

    private static double GetDoubleOrFallback(IXLCell cell, double fallback)
    {
        var value = GetNullableDouble(cell);
        return value ?? fallback;
    }

    private static double? GetNullableDouble(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.TryGetValue(out double value))
            return value;

        if (double.TryParse(cell.GetValue<string>(), out value))
            return value;

        return null;
    }

    private static DateTime? GetDateTime(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.TryGetValue(out DateTime value))
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        if (DateTime.TryParse(cell.GetValue<string>(), out value))
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return null;
    }
}
