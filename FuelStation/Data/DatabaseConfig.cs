using Microsoft.EntityFrameworkCore;

namespace FuelStation.Data;

/// <summary>
/// Database configuration — supports SQLite, SQL Server, MySQL, PostgreSQL.
/// Registers both AddDbContext (scoped, for DI) and AddDbContextFactory (scoped, for Blazor).
/// </summary>
public static class DatabaseConfig
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider", "SQLite");
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Helper: configure provider
        Action<DbContextOptionsBuilder> configure = provider switch
        {
            "SQLServer" => o => o.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly("FuelStation");
                sql.EnableRetryOnFailure(3);
            }),
            "MySQL" => o => o.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySql =>
            {
                mySql.MigrationsAssembly("FuelStation");
                mySql.EnableRetryOnFailure(3);
            }),
            "PostgreSQL" => o => o.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsAssembly("FuelStation");
                npg.EnableRetryOnFailure(3);
            }),
            _ => o => o.UseSqlite(connectionString ?? "Data Source=FuelStation.db", sqlite =>
            {
                sqlite.MigrationsAssembly("FuelStation");
            })
        };

        // Reg 1: Scoped DbContext (for DI in controllers/services)
        services.AddDbContext<AppDbContext>(configure, ServiceLifetime.Scoped);

        // Reg 2: DbContextFactory with Scoped lifetime (for Blazor components)
        // 🔑 Must use ServiceLifetime.Scoped so DbContextOptions resolves correctly
        services.AddDbContextFactory<AppDbContext>(configure, ServiceLifetime.Scoped);

        return services;
    }
}
