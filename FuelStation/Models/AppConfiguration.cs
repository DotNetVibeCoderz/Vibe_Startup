namespace FuelStation.Models;

/// <summary>
/// App configuration model for settings that can be changed via UI
/// </summary>
public class AppConfiguration
{
    public string DatabaseProvider { get; set; } = "SQLite";
    public string StorageProvider { get; set; } = "FileSystem";
    public string Theme { get; set; } = "Light";
    public string StationName { get; set; } = "FuelStation Mini";
    public int LowStockThresholdPercent { get; set; } = 20;
    public bool EnableNotifications { get; set; } = true;
    public bool EnableSimulator { get; set; } = false;
    public int SimulatorIntervalMs { get; set; } = 5000;
    public int LoyaltyPointsPerLiter { get; set; } = 1;
    public decimal DefaultTaxRate { get; set; } = 0.11m;
    public string PrinterType { get; set; } = "ESC/POS";
    public string PrinterPort { get; set; } = "COM1";

    // Chat Bot Configuration
    public string ChatProvider { get; set; } = "OpenAI"; // OpenAI, Anthropic, Gemini, Ollama
    public string ChatModel { get; set; } = "gpt-4o";
    public string ChatApiKey { get; set; } = "";
    public string ChatEndpoint { get; set; } = "";
    public double ChatTemperature { get; set; } = 0.7;
    public string ChatSystemPrompt { get; set; } = "Kamu adalah Bang Jenggo, asisten virtual SPBU Mini yang ramah dan informatif.";
}

/// <summary>
/// Storage provider configuration
/// </summary>
public class StorageConfig
{
    public string Provider { get; set; } = "FileSystem";
    public string BasePath { get; set; } = "uploads";
    public string? ConnectionString { get; set; }
    public string? BucketName { get; set; }
    public string? Endpoint { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}
