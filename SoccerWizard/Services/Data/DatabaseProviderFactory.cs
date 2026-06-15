using Microsoft.EntityFrameworkCore;

namespace SoccerWizard.Services.Data;

/// <summary>
/// Factory untuk memilih database provider secara dinamis
/// berdasarkan konfigurasi appsettings.json.
/// 
/// Supported: Sqlite | SqlServer | MySql | PostgreSql
/// </summary>
public static class DatabaseProviderFactory
{
    /// <summary>
    /// Konfigurasi DbContextOptionsBuilder sesuai provider yang dipilih.
    /// </summary>
    public static void Configure(IConfiguration config, DbContextOptionsBuilder options)
    {
        var provider = config["DatabaseProvider"] ?? "Sqlite";
        var connectionString = provider switch
        {
            "SqlServer" => config.GetConnectionString("SqlServer"),
            "MySql" => config.GetConnectionString("MySql"),
            "PostgreSql" => config.GetConnectionString("PostgreSql"),
            _ => config.GetConnectionString("Sqlite") ?? "Data Source=SoccerWizard.db"
        };

        switch (provider)
        {
            case "SqlServer":
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("SoccerWizard");
                    sqlOptions.EnableRetryOnFailure(3);
                });
                break;

            case "MySql":
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
                {
                    mySqlOptions.MigrationsAssembly("SoccerWizard");
                    mySqlOptions.EnableRetryOnFailure(3);
                });
                break;

            case "PostgreSql":
                options.UseNpgsql(connectionString, npgOptions =>
                {
                    npgOptions.MigrationsAssembly("SoccerWizard");
                    npgOptions.EnableRetryOnFailure(3);
                });
                break;

            default: // Sqlite
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly("SoccerWizard");
                });
                break;
        }
    }

    /// <summary>
    /// Mendapatkan nama provider yang sedang aktif.
    /// </summary>
    public static string GetActiveProvider(IConfiguration config)
        => config["DatabaseProvider"] ?? "Sqlite";
}
