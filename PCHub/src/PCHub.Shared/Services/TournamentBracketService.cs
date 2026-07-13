using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;

namespace PCHub.Shared.Services;

/// <summary>
/// Tournament Bracket System - Single Elimination, Double Elimination, Round Robin.
/// Generate bracket otomatis saat turnamen dimulai.
/// </summary>
public class TournamentBracketService
{
    /// <summary>Generate bracket untuk turnamen</summary>
    public TournamentBracketDto GenerateBracket(string tournamentName, Guid tournamentId, List<string> participants, TournamentBracketType type = TournamentBracketType.SingleElimination)
    {
        var rounds = new List<BracketRoundDto>();
        var playerCount = participants.Count;

        if (type == TournamentBracketType.SingleElimination)
        {
            rounds = GenerateSingleElimination(participants);
        }
        else if (type == TournamentBracketType.RoundRobin)
        {
            rounds = GenerateRoundRobin(participants);
        }

        return new TournamentBracketDto(tournamentId, tournamentName, rounds);
    }

    private static List<BracketRoundDto> GenerateSingleElimination(List<string> participants)
    {
        var rounds = new List<BracketRoundDto>();
        var players = participants.ToList();

        // Pad ke power of 2
        var targetSize = 1;
        while (targetSize < players.Count) targetSize *= 2;
        while (players.Count < targetSize) players.Add("BYE");

        var roundNum = 1;
        var currentRound = players;
        var roundNames = new[] { "Final", "Semi Final", "Quarter Final", "Round of 16", "Round of 32", "Round of 64" };

        while (currentRound.Count >= 2)
        {
            var matches = new List<BracketMatchDto>();
            for (int i = 0; i < currentRound.Count; i += 2)
            {
                matches.Add(new BracketMatchDto(
                    Guid.NewGuid(),
                    currentRound[i] == "BYE" ? null : currentRound[i],
                    currentRound[i + 1] == "BYE" || i + 1 >= currentRound.Count ? null : currentRound[i + 1],
                    null, null, null, false
                ));
            }

            var roundNameIdx = Math.Min(roundNum - 1, roundNames.Length - 1);
            var roundName = roundNames[^(roundNameIdx + 1)];

            rounds.Insert(0, new BracketRoundDto(roundNum, roundName, matches));

            // Simulasi winner untuk bracket yang sudah diisi
            var winners = new List<string>();
            foreach (var m in matches)
            {
                if (m.Player1 == "BYE" || m.Player2 == null) winners.Add(m.Player1 ?? "TBD");
                else if (m.Player2 == "BYE") winners.Add(m.Player1 ?? "TBD");
                else winners.Add("TBD");
            }

            currentRound = winners;
            roundNum++;
        }

        return rounds;
    }

    private static List<BracketRoundDto> GenerateRoundRobin(List<string> participants)
    {
        var rounds = new List<BracketRoundDto>();
        var matches = new List<BracketMatchDto>();

        // Semua vs semua
        for (int i = 0; i < participants.Count; i++)
        {
            for (int j = i + 1; j < participants.Count; j++)
            {
                matches.Add(new BracketMatchDto(
                    Guid.NewGuid(),
                    participants[i], participants[j],
                    null, null, null, false
                ));
            }
        }

        rounds.Add(new BracketRoundDto(1, "Round Robin", matches));
        return rounds;
    }

    /// <summary>Update hasil pertandingan</summary>
    public BracketMatchDto UpdateMatchResult(BracketMatchDto match, int score1, int score2)
    {
        var winnerId = score1 > score2 ? Guid.NewGuid() : Guid.NewGuid();
        return match with
        {
            Score1 = score1,
            Score2 = score2,
            WinnerId = winnerId,
            IsCompleted = true
        };
    }
}
