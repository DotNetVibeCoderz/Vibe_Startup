using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace PDA.Services.Database;

/// <summary>
/// Factory for creating database connections.
/// Supports: SQLite, SQLServer, MySQL, PostgreSQL
/// </summary>
public class DatabaseConnectorFactory
{
    private readonly ILogger<DatabaseConnectorFactory> _logger;
    public DatabaseConnectorFactory(ILogger<DatabaseConnectorFactory> logger) { _logger = logger; }

    /// <summary>
    /// Create the correct IDbConnection based on database type
    /// </summary>
    public IDbConnection CreateConnection(string databaseType, string connectionString, string? filePath = null)
    {
        var type = (databaseType ?? "sqlite").ToLower().Trim();
        
        return type switch
        {
            "sqlite" => new SqliteConnection(connectionString),
            "sqlserver" => new SqlConnection(connectionString),
            "mysql" => new MySqlConnection(connectionString),
            "postgresql" or "postgre" => new NpgsqlConnection(connectionString),
            _ => throw new NotSupportedException($"Database type '{databaseType}' is not supported. Supported: SQLite, SQLServer, MySQL, PostgreSQL.")
        };
    }

    /// <summary>
    /// Test a database connection and return result with duration
    /// </summary>
    public async Task<(bool Success, string Message, TimeSpan Duration)> TestConnectionAsync(
        string databaseType, string connectionString, string? filePath = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            using var connection = CreateConnection(databaseType, connectionString, filePath);
            if (connection is DbConnection dbConn)
                await dbConn.OpenAsync();
            else
                await Task.Run(() => connection.Open());
            
            connection.Close();
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Connection test SUCCESS: {Type} in {Duration}ms", databaseType, duration.TotalMilliseconds);
            return (true, "Connection successful!", duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Database connection test FAILED for {Type}", databaseType);
            return (false, $"Connection failed: {ex.Message}", duration);
        }
    }
}
