using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model untuk berita dan analisis sentimen
/// </summary>
public class NewsArticle
{
    [Key]
    public int Id { get; set; }
    
    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(5000)]
    public string Content { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string Source { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty; // Transfer, Injury, Match Preview, etc
    
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    
    public int? MatchId { get; set; }
    public Match? Match { get; set; }
    
    // Sentiment Analysis Results
    public double SentimentScore { get; set; } // -1 (negative) to 1 (positive)
    [MaxLength(50)]
    public string SentimentLabel { get; set; } = "NEUTRAL"; // POSITIVE, NEGATIVE, NEUTRAL
    
    [MaxLength(1000)]
    public string SentimentSummary { get; set; } = string.Empty;
    
    public DateTime PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
