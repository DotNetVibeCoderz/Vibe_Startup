using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoccerWizard.Data;
using SoccerWizard.Models;

namespace SoccerWizard.Api;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        // ==================== API ENDPOINTS ====================
        var api = app.MapGroup("/api").WithOpenApi().WithTags("SoccerWizard API");

        // ==================== AUTH (HTTP POST) ====================
        var auth = api.MapGroup("/auth").WithTags("Auth");
        auth.MapPost("/login", async (HttpContext http, SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users, string? email, string? password, bool? rememberMe) =>
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Results.BadRequest(new { success = false, error = "Email and password are required" });

            var user = await users.FindByEmailAsync(email);
            if (user is null)
                return Results.Unauthorized();

            var result = await signIn.PasswordSignInAsync(user, password, rememberMe ?? false, false);
            return result.Succeeded ? Results.Ok(new { success = true }) : Results.Unauthorized();
        }).DisableAntiforgery();

        var matches = api.MapGroup("/matches").WithTags("Matches");
        matches.MapGet("/", async (IDbContextFactory<AppDbContext> db, string? status, int? leagueId, int? teamId, int page = 1, int pageSize = 20) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var q = ctx.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.League).AsQueryable();
            if (!string.IsNullOrEmpty(status)) q = q.Where(m => m.Status == status);
            if (leagueId.HasValue) q = q.Where(m => m.LeagueId == leagueId);
            if (teamId.HasValue) q = q.Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(m => m.MatchDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Results.Ok(new { success = true, data = new { items, page, pageSize, totalCount = total, totalPages = (int)Math.Ceiling((double)total / pageSize) } });
        });

        matches.MapGet("/live", async (IDbContextFactory<AppDbContext> db) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new { success = true, data = await ctx.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.League).Where(m => m.Status == "LIVE").ToListAsync() });
        });

        matches.MapGet("/upcoming", async (IDbContextFactory<AppDbContext> db, int limit = 20) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new { success = true, data = await ctx.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.League).Where(m => m.Status == "SCHEDULED" && m.MatchDate > DateTime.UtcNow).OrderBy(m => m.MatchDate).Take(limit).ToListAsync() });
        });

        matches.MapGet("/{id:int}", async (int id, IDbContextFactory<AppDbContext> db) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var m = await ctx.Matches.Include(x => x.HomeTeam).Include(x => x.AwayTeam).Include(x => x.League).Include(x => x.Predictions).FirstOrDefaultAsync(x => x.Id == id);
            return m is not null ? Results.Ok(new { success = true, data = m }) : Results.NotFound(new { success = false, error = "Match not found" });
        });

        // Teams
        var teams = api.MapGroup("/teams").WithTags("Teams");
        teams.MapGet("/", async (IDbContextFactory<AppDbContext> db, int? leagueId) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var q = ctx.Teams.Include(t => t.League).AsQueryable();
            if (leagueId.HasValue) q = q.Where(t => t.LeagueId == leagueId);
            return Results.Ok(new { success = true, data = await q.OrderBy(t => t.Name).ToListAsync() });
        });
        teams.MapGet("/top", async (IDbContextFactory<AppDbContext> db, int count = 10) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new { success = true, data = await ctx.Teams.OrderByDescending(t => t.EloRating).Take(count).ToListAsync() });
        });
        teams.MapGet("/{id:int}", async (int id, IDbContextFactory<AppDbContext> db) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var t = await ctx.Teams.Include(x => x.League).Include(x => x.Players).FirstOrDefaultAsync(x => x.Id == id);
            return t is not null ? Results.Ok(new { success = true, data = t }) : Results.NotFound(new { success = false, error = "Team not found" });
        });

        // Players
        var players = api.MapGroup("/players").WithTags("Players");
        players.MapGet("/", async (IDbContextFactory<AppDbContext> db, int? teamId, string? position) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var q = ctx.Players.Include(p => p.Team).AsQueryable();
            if (teamId.HasValue) q = q.Where(p => p.TeamId == teamId);
            if (!string.IsNullOrEmpty(position)) q = q.Where(p => p.Position == position.ToUpper());
            return Results.Ok(new { success = true, data = await q.OrderByDescending(p => p.Rating).Take(50).ToListAsync() });
        });
        players.MapGet("/top-scorers", async (IDbContextFactory<AppDbContext> db, int count = 10) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new { success = true, data = await ctx.Players.Include(p => p.Team).OrderByDescending(p => p.Goals).Take(count).ToListAsync() });
        });

        // Leagues
        api.MapGet("/leagues", async (IDbContextFactory<AppDbContext> db) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new { success = true, data = await ctx.Leagues.OrderBy(l => l.Name).ToListAsync() });
        }).WithTags("Leagues");

        // Predictions
        var preds = api.MapGroup("/predictions").WithTags("Predictions");
        preds.MapGet("/", async (IDbContextFactory<AppDbContext> db) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new { success = true, data = await ctx.Predictions.Include(p => p.Match).ThenInclude(m => m.HomeTeam).Include(p => p.Match).ThenInclude(m => m.AwayTeam).OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync() });
        });
        preds.MapGet("/accuracy", async (IDbContextFactory<AppDbContext> db) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var total = await ctx.Predictions.CountAsync(p => p.IsCorrect.HasValue);
            var correct = await ctx.Predictions.CountAsync(p => p.IsCorrect == true);
            return Results.Ok(new { success = true, data = new { Total = total, Correct = correct, Accuracy = total > 0 ? Math.Round((double)correct / total * 100, 2) : 0 } });
        });

        // HeadToHead
        api.MapGet("/headtohead", async (IDbContextFactory<AppDbContext> db, int? team1Id, int? team2Id) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var q = ctx.HeadToHeads.Include(h => h.Team1).Include(h => h.Team2).AsQueryable();
            if (team1Id.HasValue && team2Id.HasValue)
                q = q.Where(h => (h.Team1Id == team1Id && h.Team2Id == team2Id) || (h.Team1Id == team2Id && h.Team2Id == team1Id));
            return Results.Ok(new { success = true, data = await q.ToListAsync() });
        }).WithTags("HeadToHead");

        // News
        var news = api.MapGroup("/news").WithTags("News");
        news.MapGet("/", async (IDbContextFactory<AppDbContext> db, string? sentiment) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            var q = ctx.NewsArticles.Include(n => n.Team).AsQueryable();
            if (!string.IsNullOrEmpty(sentiment)) q = q.Where(n => n.SentimentLabel == sentiment.ToUpper());
            return Results.Ok(new { success = true, data = await q.OrderByDescending(n => n.PublishedAt).Take(50).ToListAsync() });
        });

        // Stats
        var stats = api.MapGroup("/stats").WithTags("Stats");
        stats.MapGet("/dashboard", async (IDbContextFactory<AppDbContext> db) =>
        {
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    TotalMatches = await ctx.Matches.CountAsync(),
                    LiveMatches = await ctx.Matches.CountAsync(m => m.Status == "LIVE"),
                    UpcomingMatches = await ctx.Matches.CountAsync(m => m.Status == "SCHEDULED"),
                    TotalTeams = await ctx.Teams.CountAsync(),
                    TotalLeagues = await ctx.Leagues.CountAsync(),
                    TotalPredictions = await ctx.Predictions.CountAsync(),
                    CorrectPredictions = await ctx.Predictions.CountAsync(p => p.IsCorrect == true),
                    TotalNews = await ctx.NewsArticles.CountAsync()
                }
            });
        });

        // Search
        api.MapGet("/search", async (IDbContextFactory<AppDbContext> db, string? q) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { success = false, error = "Query required" });
            await using var ctx = await db.CreateDbContextAsync();
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    Teams = await ctx.Teams.Where(t => t.Name.Contains(q) || t.Country.Contains(q)).Take(10).ToListAsync(),
                    Players = await ctx.Players.Include(p => p.Team).Where(p => p.Name.Contains(q)).Take(10).ToListAsync(),
                }
            });
        });
    }
}
