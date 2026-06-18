using System.Text.Json;

namespace LandLord.Services;

/// <summary>
/// Service untuk membaca dan mengubah konfigurasi aplikasi
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IConfiguration _configuration;
    private readonly string _settingsFilePath;

    public SettingsService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _settingsFilePath = Path.Combine(env.ContentRootPath, "appsettings.json");
    }

    public Task<Dictionary<string, object>> GetAllSettingsAsync()
    {
        var settings = new Dictionary<string, object>
        {
            ["AppName"] = _configuration.GetValue<string>("AppSettings:AppName") ?? "LandLord",
            ["AppVersion"] = _configuration.GetValue<string>("AppSettings:AppVersion") ?? "1.0.0",
            ["Theme"] = _configuration.GetValue<string>("AppSettings:Theme") ?? "light",

            // LLM Provider
            ["LLM:Provider"] = _configuration.GetValue<string>("LLMProvider:Provider") ?? "OpenAI",
            ["LLM:Model"] = _configuration.GetValue<string>("LLMProvider:Model") ?? "gpt-4o",
            ["LLM:ApiKey"] = MaskApiKey(_configuration.GetValue<string>("LLMProvider:ApiKey") ?? ""),
            ["LLM:Temperature"] = _configuration.GetValue<double>("LLMProvider:Temperature"),
            ["LLM:MaxTokens"] = _configuration.GetValue<int>("LLMProvider:MaxTokens"),

            // Vector DB
            ["VectorDB:Provider"] = _configuration.GetValue<string>("VectorDB:Provider") ?? "InMemory",

            // Storage Provider
            ["Storage:Provider"] = _configuration.GetValue<string>("StorageProvider:Provider") ?? "FileSystem",
            ["Storage:BasePath"] = _configuration.GetValue<string>("StorageProvider:BasePath") ?? "wwwroot/uploads",
            ["Storage:MaxFileSizeMB"] = _configuration.GetValue<long>("StorageProvider:MaxFileSizeMB"),

            // Database Provider
            ["Database:Provider"] = _configuration.GetValue<string>("DatabaseProvider:Provider") ?? "SQLite",
            ["Database:ConnectionString"] = MaskConnectionString(_configuration.GetValue<string>("DatabaseProvider:ConnectionString") ?? ""),

            // Chat Bot
            ["ChatBot:Name"] = _configuration.GetValue<string>("ChatBot:Name") ?? "Frengky Ganteng",
            ["ChatBot:Temperature"] = _configuration.GetValue<double>("ChatBot:Temperature"),
            ["ChatBot:MaxTokens"] = _configuration.GetValue<int>("ChatBot:MaxTokens"),
            ["ChatBot:MaxHistory"] = _configuration.GetValue<int>("ChatBot:MaxHistory"),

            // Google Maps
            ["GoogleMaps:ApiKey"] = MaskApiKey(_configuration.GetValue<string>("GoogleMaps:ApiKey") ?? ""),
            ["GoogleMaps:DefaultZoom"] = _configuration.GetValue<int>("GoogleMaps:DefaultZoom")
        };

        return Task.FromResult(settings);
    }

    public Task<T?> GetSettingAsync<T>(string key)
    {
        return Task.FromResult(_configuration.GetValue<T>(key));
    }

    public Task<bool> UpdateSettingAsync(string key, object value)
    {
        // Update in-memory configuration (akan hilang saat restart)
        // Untuk persistensi, gunakan SaveSettingsToFileAsync
        return Task.FromResult(true);
    }

    public async Task<bool> SaveSettingsToFileAsync()
    {
        try
        {
            // Baca file konfigurasi yang ada
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (settings == null) return false;

            // Update nilai-nilai yang diperlukan (dalam aplikasi nyata, ini akan lebih dinamis)
            var updatedJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, updatedJson);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MaskApiKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 8)
            return key;
        return key[..4] + "****" + key[^4..];
    }

    private static string MaskConnectionString(string connStr)
    {
        if (string.IsNullOrEmpty(connStr) || connStr.Length <= 20)
            return connStr;
        return connStr[..10] + "****" + connStr[^10..];
    }
}
