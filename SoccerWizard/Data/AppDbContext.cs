using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SoccerWizard.Models;

namespace SoccerWizard.Data;

/// <summary>
/// Application Database Context - menggunakan Identity + Entity Framework Core
/// </summary>
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    // Domain Tables
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchStatistic> MatchStatistics => Set<MatchStatistic>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<HeadToHead> HeadToHeads => Set<HeadToHead>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Team -> League
        modelBuilder.Entity<Team>()
            .HasOne(t => t.League)
            .WithMany(l => l.Teams)
            .HasForeignKey(t => t.LeagueId)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Player -> Team
        modelBuilder.Entity<Player>()
            .HasOne(p => p.Team)
            .WithMany(t => t.Players)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Match -> HomeTeam
        modelBuilder.Entity<Match>()
            .HasOne(m => m.HomeTeam)
            .WithMany(t => t.HomeMatches)
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Match -> AwayTeam
        modelBuilder.Entity<Match>()
            .HasOne(m => m.AwayTeam)
            .WithMany(t => t.AwayMatches)
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Match -> League
        modelBuilder.Entity<Match>()
            .HasOne(m => m.League)
            .WithMany(l => l.Matches)
            .HasForeignKey(m => m.LeagueId)
            .OnDelete(DeleteBehavior.SetNull);
        
        // MatchStatistic -> Match, Team
        modelBuilder.Entity<MatchStatistic>()
            .HasOne(ms => ms.Match)
            .WithMany(m => m.Statistics)
            .HasForeignKey(ms => ms.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<MatchStatistic>()
            .HasOne(ms => ms.Team)
            .WithMany()
            .HasForeignKey(ms => ms.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Prediction -> Match
        modelBuilder.Entity<Prediction>()
            .HasOne(p => p.Match)
            .WithMany(m => m.Predictions)
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // NewsArticle -> Team, Match
        modelBuilder.Entity<NewsArticle>()
            .HasOne(n => n.Team)
            .WithMany()
            .HasForeignKey(n => n.TeamId)
            .OnDelete(DeleteBehavior.SetNull);
        
        modelBuilder.Entity<NewsArticle>()
            .HasOne(n => n.Match)
            .WithMany()
            .HasForeignKey(n => n.MatchId)
            .OnDelete(DeleteBehavior.SetNull);
        
        // HeadToHead -> Team1, Team2
        modelBuilder.Entity<HeadToHead>()
            .HasOne(h => h.Team1)
            .WithMany()
            .HasForeignKey(h => h.Team1Id)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<HeadToHead>()
            .HasOne(h => h.Team2)
            .WithMany()
            .HasForeignKey(h => h.Team2Id)
            .OnDelete(DeleteBehavior.Restrict);
        
        // UserProfile -> IdentityUser
        modelBuilder.Entity<UserProfile>()
            .HasIndex(up => up.UserId)
            .IsUnique();
        
        // Indexes for performance
        modelBuilder.Entity<Match>()
            .HasIndex(m => m.MatchDate);
        
        modelBuilder.Entity<Match>()
            .HasIndex(m => m.Status);
        
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.Position);
        
        modelBuilder.Entity<NewsArticle>()
            .HasIndex(n => n.PublishedAt);
        
        modelBuilder.Entity<NewsArticle>()
            .HasIndex(n => n.SentimentLabel);
    }
}
