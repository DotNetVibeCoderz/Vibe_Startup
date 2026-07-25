using Microsoft.EntityFrameworkCore;

namespace Ngibrid.Data;

/// <summary>
/// Factory to configure DbContext based on provider selection in appsettings.json
/// </summary>
public static class DbProviderFactory
{
    public static DbContextOptionsBuilder ConfigureProvider(
        this DbContextOptionsBuilder options, 
        IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SQLite";
        var connectionString = configuration[$"Database:ConnectionStrings:{provider}"]
            ?? configuration["Database:ConnectionStrings:SQLite"];

        return provider.ToLowerInvariant() switch
        {
            "sqlite" => options.UseSqlite(connectionString!,
                x => x.MigrationsAssembly("Ngibrid")),
            
            "sqlserver" => options.UseSqlServer(connectionString!,
                x => x.MigrationsAssembly("Ngibrid")),
            
            "mysql" => options.UseMySql(connectionString!,
                ServerVersion.AutoDetect(connectionString!),
                x => x.MigrationsAssembly("Ngibrid")),
            
            "postgre" => options.UseNpgsql(connectionString!,
                x => x.MigrationsAssembly("Ngibrid")),
            
            _ => options.UseSqlite(connectionString!,
                x => x.MigrationsAssembly("Ngibrid"))
        };
    }

    /// <summary>
    /// Get provider-specific data directory if needed
    /// </summary>
    public static void EnsureDatabaseReady(this IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SQLite";
        if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            var connStr = configuration["Database:ConnectionStrings:SQLite"] ?? "Data Source=Data/ngibrid.db";
            var dataSource = connStr.Replace("Data Source=", "").Trim();
            var dir = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
