using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace SoccerWizard.Models;

/// <summary>
/// Model user profile yang terhubung dengan Identity User
/// </summary>
public class UserProfile
{
    [Key]
    public int Id { get; set; }
    
    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty; // FK ke IdentityUser
    
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string AvatarUrl { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string FavoriteTeam { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string FavoriteLeague { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string Bio { get; set; } = string.Empty;
    
    public int PredictionsCount { get; set; }
    public int CorrectPredictions { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; }
}
