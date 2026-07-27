// Layers the AppConfiguration table on top of appsettings.json.
//
// Registered last, so a row in the database wins over the file. Every existing
// `_config["Some:Key"]` read therefore honours an override with no code change,
// which is what makes the admin Settings page actually mean something.
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Joka.Data;

public class DatabaseConfigurationSource : IConfigurationSource
{
    private readonly string _connectionString;
    private readonly string _provider;

    public DatabaseConfigurationSource(string provider, string connectionString)
    {
        _provider = provider;
        _connectionString = connectionString;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DatabaseConfigurationProvider(_provider, _connectionString);
}

public class DatabaseConfigurationProvider : ConfigurationProvider
{
    private readonly string _connectionString;
    private readonly string _provider;

    /// <summary>
    /// Set once the provider is built so the settings page can push a reload
    /// after saving, without reaching into the configuration internals.
    /// </summary>
    public static DatabaseConfigurationProvider? Current { get; private set; }

    public DatabaseConfigurationProvider(string provider, string connectionString)
    {
        _provider = provider;
        _connectionString = connectionString;
        Current = this;
    }

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Only SQLite is read directly here; on other providers the overrides
        // simply do not load and appsettings stays authoritative.
        if (!_provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            Data = data;
            return;
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Key, Value FROM AppConfigurations";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(key))
                    data[key] = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }
        catch
        {
            // First run: the table does not exist until EnsureCreated has run.
            // Falling back to an empty set is correct, not an error.
        }

        Data = data;
    }

    /// <summary>Re-reads the table and notifies everything bound to IConfiguration.</summary>
    public void Reload()
    {
        Load();
        OnReload();
    }
}
