using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ngibrid.Services;

/// <summary>
/// Reads and writes appsettings.json at runtime so every setting is editable from the Settings page.
///
/// Writes go to the physical file and then trigger a configuration reload, so IConfiguration consumers
/// (and anything resolving config per-request, like ChatBotService) pick the change up without a restart.
/// A single writer lock plus write-to-temp-then-replace keeps the file from being torn by concurrent saves.
/// </summary>
public class AppConfigService
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AppConfigService> _logger;

    public AppConfigService(IConfiguration config, IWebHostEnvironment env, ILogger<AppConfigService> logger)
    {
        _config = config;
        _env = env;
        _logger = logger;
    }

    public string SettingsFilePath => Path.Combine(_env.ContentRootPath, "appsettings.json");

    public string? Get(string key) => _config[key];

    public T GetValue<T>(string key, T defaultValue) => _config.GetValue(key, defaultValue)!;

    /// <summary>
    /// Apply a batch of "Section:Sub:Key" → value updates in one write.
    /// Null values remove the key. Returns the number of keys actually changed.
    /// </summary>
    public async Task<int> UpdateAsync(IDictionary<string, string?> updates)
    {
        if (updates.Count == 0) return 0;

        await WriteLock.WaitAsync();
        try
        {
            var path = SettingsFilePath;
            var root = await LoadRootAsync(path);
            var changed = 0;

            foreach (var (key, value) in updates)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (SetValue(root, key, value)) changed++;
            }

            if (changed == 0) return 0;

            await SaveRootAsync(path, root);
            Reload();
            _logger.LogInformation("Updated {Count} setting(s) in appsettings.json", changed);
            return changed;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public Task<int> UpdateAsync(string key, string? value) =>
        UpdateAsync(new Dictionary<string, string?> { [key] = value });

    /// <summary>Force IConfiguration to re-read its providers.</summary>
    public void Reload()
    {
        if (_config is IConfigurationRoot root) root.Reload();
    }

    private static async Task<JsonObject> LoadRootAsync(string path)
    {
        if (!File.Exists(path)) return new JsonObject();

        await using var stream = File.OpenRead(path);
        var node = await JsonNode.ParseAsync(stream,
            nodeOptions: new JsonNodeOptions { PropertyNameCaseInsensitive = true },
            documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        return node as JsonObject ?? new JsonObject();
    }

    private static async Task SaveRootAsync(string path, JsonObject root)
    {
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        // Write to a sibling temp file first so a crash mid-write can't leave appsettings.json truncated.
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, json, System.Text.Encoding.UTF8);

        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }

    /// <summary>
    /// Walk/created the nested objects for a colon-delimited key and set the leaf.
    /// Values are typed from the existing node so numbers and booleans don't degrade into strings
    /// (GetValue&lt;bool&gt; would silently return the default if "true" became "\"true\"").
    /// </summary>
    private static bool SetValue(JsonObject root, string key, string? value)
    {
        var parts = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current[parts[i]] is JsonObject child)
            {
                current = child;
            }
            else
            {
                var created = new JsonObject();
                current[parts[i]] = created;
                current = created;
            }
        }

        var leaf = parts[^1];
        var existing = current[leaf];

        if (value is null)
        {
            if (existing is null) return false;
            current.Remove(leaf);
            return true;
        }

        var newNode = CoerceValue(existing, value);
        if (existing is not null && JsonNode.DeepEquals(existing, newNode)) return false;

        current[leaf] = newNode;
        return true;
    }

    private static JsonNode CoerceValue(JsonNode? existing, string value)
    {
        var existingKind = existing is JsonValue v ? v.GetValue<JsonElement>().ValueKind : JsonValueKind.Undefined;

        switch (existingKind)
        {
            case JsonValueKind.Number when double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var num):
                return num == Math.Floor(num) && Math.Abs(num) < long.MaxValue
                    ? JsonValue.Create((long)num)
                    : JsonValue.Create(num);

            case JsonValueKind.True or JsonValueKind.False when bool.TryParse(value, out var flag):
                return JsonValue.Create(flag);

            default:
                // New keys: infer bool/number so freshly added settings behave like hand-written ones.
                if (existing is null)
                {
                    if (bool.TryParse(value, out var newFlag)) return JsonValue.Create(newFlag);
                    if (long.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var newInt))
                        return JsonValue.Create(newInt);
                }
                return JsonValue.Create(value);
        }
    }
}
