namespace VibeWallet.Models;

/// <summary>
/// Strongly-typed configuration classes for appsettings.json
/// </summary>

public class VibeWalletConfig
{
    public string AppName { get; set; } = "VibeWallet";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "Digital Wallet Application";
    public string CompanyName { get; set; } = "VibeWallet Indonesia";
    public string SupportEmail { get; set; } = "support@vibewallet.id";
    public string SupportPhone { get; set; } = "+62812-3456-7890";
    public int ItemsPerPage { get; set; } = 20;
    public int MaxLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int SessionTimeoutMinutes { get; set; } = 30;
}

public class StorageConfig
{
    public string Provider { get; set; } = "FileSystem";
    public FileSystemStorageConfig? FileSystem { get; set; }
    public AzureBlobStorageConfig? AzureBlob { get; set; }
    public S3StorageConfig? S3 { get; set; }
    public MinIOStorageConfig? MinIO { get; set; }
}

public class FileSystemStorageConfig
{
    public string BasePath { get; set; } = "wwwroot/uploads";
}

public class AzureBlobStorageConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "vibewallet";
}

public class S3StorageConfig
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "vibewallet";
    public string Region { get; set; } = "ap-southeast-1";
    public string ServiceUrl { get; set; } = string.Empty;
}

public class MinIOStorageConfig
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string BucketName { get; set; } = "vibewallet";
    public bool UseSSL { get; set; } = false;
}

public class ChatBotConfig
{
    public string Name { get; set; } = "Mbak Selvi";
    public string Provider { get; set; } = "OpenAI";
    public string SystemPrompt { get; set; } = string.Empty;
    public decimal Temperature { get; set; } = 0.7m;
    public int MaxTokens { get; set; } = 2000;
    public decimal TopP { get; set; } = 0.9m;
    public ChatBotModelsConfig? Models { get; set; }
    public TavilyConfig? Tavily { get; set; }
}

public class ChatBotModelsConfig
{
    public OpenAIConfig? OpenAI { get; set; }
    public AnthropicConfig? Anthropic { get; set; }
    public GeminiConfig? Gemini { get; set; }
    public OllamaConfig? Ollama { get; set; }
}

public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gpt-4o";
    public string Endpoint { get; set; } = string.Empty;
}

public class AnthropicConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "claude-3-5-sonnet-20241022";
    public string Endpoint { get; set; } = "https://api.anthropic.com/v1/messages";
}

public class GeminiConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gemini-1.5-pro";
    public string Endpoint { get; set; } = string.Empty;
}

public class OllamaConfig
{
    public string ModelId { get; set; } = "llama3.2";
    public string Endpoint { get; set; } = "http://localhost:11434";
}

public class TavilyConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.tavily.com/search";
}

public class TransactionLimitsConfig
{
    public decimal DailyTransferLimit { get; set; } = 25_000_000;
    public decimal DailyTopUpLimit { get; set; } = 10_000_000;
    public decimal DailyPaymentLimit { get; set; } = 50_000_000;
    public decimal MonthlyTransferLimit { get; set; } = 100_000_000;
    public decimal MaxWalletBalance { get; set; } = 50_000_000;
    public decimal MinTransferAmount { get; set; } = 10_000;
    public decimal MinTopUpAmount { get; set; } = 5_000;
}

public class RewardsConfig
{
    public decimal CashbackPercentage { get; set; } = 0.5m;
    public int PointsPerTransaction { get; set; } = 1;
    public decimal MinimumTransactionForPoints { get; set; } = 10_000;
    public decimal PointsExchangeRate { get; set; } = 1_000;
}

public class KycConfig
{
    public bool RequiredForTransactions { get; set; } = true;
    public int MaxUploadSizeMB { get; set; } = 5;
    public List<string> AllowedFileTypes { get; set; } = new() { ".jpg", ".jpeg", ".png", ".pdf" };
    public bool AutoVerificationEnabled { get; set; } = false;
}
