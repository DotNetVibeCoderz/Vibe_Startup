using ClosedXML.Excel;

namespace SoccerWizard.Services;

/// <summary>
/// Builder untuk template excel sync manual.
/// </summary>
public static class ManualSyncExcelTemplate
{
    public const string TemplateFileName = "SoccerWizard-ManualSyncTemplate.xlsx";

    public static byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        BuildLeagueSheet(workbook);
        BuildTeamSheet(workbook);
        BuildMatchSheet(workbook);
        BuildStandingsSheet(workbook);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildLeagueSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("Leagues");
        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(1, 2).Value = "Country";
        sheet.Cell(1, 3).Value = "LogoUrl";
        sheet.Cell(1, 4).Value = "Season";
        sheet.Cell(1, 5).Value = "TotalTeams";
        sheet.Cell(1, 6).Value = "TotalRounds";
        sheet.Cell(1, 7).Value = "Description";

        sheet.Cell(2, 1).Value = "Premier League";
        sheet.Cell(2, 2).Value = "England";
        sheet.Cell(2, 3).Value = "https://example.com/premier-league.png";
        sheet.Cell(2, 4).Value = "2024/2025";
        sheet.Cell(2, 5).Value = 20;
        sheet.Cell(2, 6).Value = 38;
        sheet.Cell(2, 7).Value = "League contoh";

        FormatHeader(sheet, 1, 7);
    }

    private static void BuildTeamSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("Teams");
        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(1, 2).Value = "ShortName";
        sheet.Cell(1, 3).Value = "Code";
        sheet.Cell(1, 4).Value = "LogoUrl";
        sheet.Cell(1, 5).Value = "Country";
        sheet.Cell(1, 6).Value = "City";
        sheet.Cell(1, 7).Value = "Stadium";
        sheet.Cell(1, 8).Value = "FoundedYear";
        sheet.Cell(1, 9).Value = "Description";
        sheet.Cell(1, 10).Value = "LeagueName";
        sheet.Cell(1, 11).Value = "EloRating";
        sheet.Cell(1, 12).Value = "AttackStrength";
        sheet.Cell(1, 13).Value = "DefenseStrength";
        sheet.Cell(1, 14).Value = "MidfieldStrength";
        sheet.Cell(1, 15).Value = "Momentum";

        sheet.Cell(2, 1).Value = "Manchester City";
        sheet.Cell(2, 2).Value = "Man City";
        sheet.Cell(2, 3).Value = "MCI";
        sheet.Cell(2, 4).Value = "https://example.com/mci.png";
        sheet.Cell(2, 5).Value = "England";
        sheet.Cell(2, 6).Value = "Manchester";
        sheet.Cell(2, 7).Value = "Etihad Stadium";
        sheet.Cell(2, 8).Value = 1880;
        sheet.Cell(2, 9).Value = "Tim contoh";
        sheet.Cell(2, 10).Value = "Premier League";
        sheet.Cell(2, 11).Value = 1700;
        sheet.Cell(2, 12).Value = 1.2;
        sheet.Cell(2, 13).Value = 1.1;
        sheet.Cell(2, 14).Value = 1.15;
        sheet.Cell(2, 15).Value = 0.6;

        FormatHeader(sheet, 1, 15);
    }

    private static void BuildMatchSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("Matches");
        sheet.Cell(1, 1).Value = "HomeTeam";
        sheet.Cell(1, 2).Value = "AwayTeam";
        sheet.Cell(1, 3).Value = "LeagueName";
        sheet.Cell(1, 4).Value = "MatchDateUtc";
        sheet.Cell(1, 5).Value = "Status";
        sheet.Cell(1, 6).Value = "Venue";
        sheet.Cell(1, 7).Value = "Referee";
        sheet.Cell(1, 8).Value = "HomeScore";
        sheet.Cell(1, 9).Value = "AwayScore";
        sheet.Cell(1, 10).Value = "HomeHalfTimeScore";
        sheet.Cell(1, 11).Value = "AwayHalfTimeScore";
        sheet.Cell(1, 12).Value = "Round";

        sheet.Cell(2, 1).Value = "Manchester City";
        sheet.Cell(2, 2).Value = "Liverpool";
        sheet.Cell(2, 3).Value = "Premier League";
        sheet.Cell(2, 4).Value = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd HH:mm");
        sheet.Cell(2, 5).Value = "SCHEDULED";
        sheet.Cell(2, 6).Value = "Etihad Stadium";
        sheet.Cell(2, 7).Value = "John Doe";
        sheet.Cell(2, 8).Value = string.Empty;
        sheet.Cell(2, 9).Value = string.Empty;
        sheet.Cell(2, 10).Value = string.Empty;
        sheet.Cell(2, 11).Value = string.Empty;
        sheet.Cell(2, 12).Value = "Matchday 1";

        FormatHeader(sheet, 1, 12);
    }

    private static void BuildStandingsSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("Standings");
        sheet.Cell(1, 1).Value = "TeamName";
        sheet.Cell(1, 2).Value = "LeagueName";
        sheet.Cell(1, 3).Value = "MatchesPlayed";
        sheet.Cell(1, 4).Value = "Wins";
        sheet.Cell(1, 5).Value = "Draws";
        sheet.Cell(1, 6).Value = "Losses";
        sheet.Cell(1, 7).Value = "GoalsFor";
        sheet.Cell(1, 8).Value = "GoalsAgainst";
        sheet.Cell(1, 9).Value = "AvgGoalsScored";
        sheet.Cell(1, 10).Value = "AvgGoalsConceded";

        sheet.Cell(2, 1).Value = "Manchester City";
        sheet.Cell(2, 2).Value = "Premier League";
        sheet.Cell(2, 3).Value = 3;
        sheet.Cell(2, 4).Value = 2;
        sheet.Cell(2, 5).Value = 1;
        sheet.Cell(2, 6).Value = 0;
        sheet.Cell(2, 7).Value = 6;
        sheet.Cell(2, 8).Value = 2;
        sheet.Cell(2, 9).Value = 2.0;
        sheet.Cell(2, 10).Value = 0.67;

        FormatHeader(sheet, 1, 10);
    }

    private static void FormatHeader(IXLWorksheet sheet, int headerRow, int columnCount)
    {
        var headerRange = sheet.Range(headerRow, 1, headerRow, columnCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");
        sheet.Columns(1, columnCount).AdjustToContents();
    }
}
