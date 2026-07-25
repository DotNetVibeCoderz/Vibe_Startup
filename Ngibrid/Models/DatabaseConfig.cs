namespace Ngibrid.Models;

/// <summary>
/// Database configuration model for binding from appsettings.json
/// </summary>
public class DatabaseConfig
{
    public string Provider { get; set; } = "SQLite";
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();
}
