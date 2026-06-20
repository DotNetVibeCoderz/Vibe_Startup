using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PDA.Models;

/// <summary>
/// Extended Application User with additional profile fields
/// </summary>
public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    [MaxLength(50)]
    public string? ThemePreference { get; set; } = "light";

    // Navigation properties
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<DatabaseConnection> DatabaseConnections { get; set; } = new List<DatabaseConnection>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
