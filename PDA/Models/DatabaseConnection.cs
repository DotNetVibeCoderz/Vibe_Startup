using System.ComponentModel.DataAnnotations;

namespace PDA.Models;

/// <summary>
/// Database connection configuration saved by user
/// </summary>
public class DatabaseConnection
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(50)]
    public string DatabaseType { get; set; } = "SQLite"; // SQLite, SQLServer, MySQL, PostgreSQL, Oracle, MsAccess, Excel, CSV

    [MaxLength(2000)]
    public string ConnectionString { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? FilePath { get; set; } // For file-based DBs (SQLite, Excel, CSV, Access)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Foreign key
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    // Navigation
    public ApplicationUser? User { get; set; }
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
