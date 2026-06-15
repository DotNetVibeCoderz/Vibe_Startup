using System.Text.Json.Serialization;

namespace SoccerWizard.Services;

public class FootballDataCompetitionResponse
{
    [JsonPropertyName("competitions")]
    public List<FootballDataCompetitionDto> Competitions { get; set; } = new();
}

public class FootballDataCompetitionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("emblem")]
    public string? Emblem { get; set; }

    [JsonPropertyName("area")]
    public FootballDataAreaDto? Area { get; set; }

    [JsonPropertyName("currentSeason")]
    public FootballDataSeasonDto? CurrentSeason { get; set; }
}

public class FootballDataSeasonDto
{
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("currentMatchday")]
    public int? CurrentMatchday { get; set; }
}

public class FootballDataAreaDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class FootballDataTeamsResponse
{
    [JsonPropertyName("teams")]
    public List<FootballDataTeamDto> Teams { get; set; } = new();
}

public class FootballDataTeamDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }

    [JsonPropertyName("tla")]
    public string? Tla { get; set; }

    [JsonPropertyName("crest")]
    public string? Crest { get; set; }

    [JsonPropertyName("venue")]
    public string? Venue { get; set; }

    [JsonPropertyName("founded")]
    public int? Founded { get; set; }

    [JsonPropertyName("area")]
    public FootballDataAreaDto? Area { get; set; }
}

public class FootballDataMatchesResponse
{
    [JsonPropertyName("matches")]
    public List<FootballDataMatchDto> Matches { get; set; } = new();
}

public class FootballDataMatchDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("utcDate")]
    public DateTime UtcDate { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "SCHEDULED";

    [JsonPropertyName("stage")]
    public string? Stage { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("matchday")]
    public int? Matchday { get; set; }

    [JsonPropertyName("venue")]
    public string? Venue { get; set; }

    [JsonPropertyName("competition")]
    public FootballDataCompetitionDto? Competition { get; set; }

    [JsonPropertyName("homeTeam")]
    public FootballDataMatchTeamDto HomeTeam { get; set; } = new();

    [JsonPropertyName("awayTeam")]
    public FootballDataMatchTeamDto AwayTeam { get; set; } = new();

    [JsonPropertyName("score")]
    public FootballDataScoreDto? Score { get; set; }

    [JsonPropertyName("referees")]
    public List<FootballDataRefereeDto>? Referees { get; set; }
}

public class FootballDataMatchTeamDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class FootballDataScoreDto
{
    [JsonPropertyName("fullTime")]
    public FootballDataScoreTimeDto? FullTime { get; set; }

    [JsonPropertyName("halfTime")]
    public FootballDataScoreTimeDto? HalfTime { get; set; }
}

public class FootballDataScoreTimeDto
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}

public class FootballDataStandingsResponse
{
    [JsonPropertyName("standings")]
    public List<FootballDataStandingDto> Standings { get; set; } = new();
}

public class FootballDataStandingDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("table")]
    public List<FootballDataStandingRowDto> Table { get; set; } = new();
}

public class FootballDataStandingRowDto
{
    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("playedGames")]
    public int PlayedGames { get; set; }

    [JsonPropertyName("won")]
    public int Won { get; set; }

    [JsonPropertyName("draw")]
    public int Draw { get; set; }

    [JsonPropertyName("lost")]
    public int Lost { get; set; }

    [JsonPropertyName("goalsFor")]
    public int GoalsFor { get; set; }

    [JsonPropertyName("goalsAgainst")]
    public int GoalsAgainst { get; set; }

    [JsonPropertyName("goalDifference")]
    public int GoalDifference { get; set; }

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("team")]
    public FootballDataMatchTeamDto Team { get; set; } = new();
}

public class FootballDataRefereeDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
