using System.Data.Common;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using AppBender.Core.AI;
using AppBender.Core.Common;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Azure.Storage.Blobs;

namespace AppBender.Core.Connectors;

internal static class ConnectorInput
{
    public static string Text(this Dictionary<string, object?> input, string key, string fallback = "")
        => input.TryGetValue(key, out var v) && v is not null ? TemplateEngine.ToText(v) : fallback;
}

// ------------------------------------------------------------------ HTTP family

public class RestApiConnector(IHttpClientFactory httpFactory) : IConnector
{
    public string Provider => "rest";
    public string DisplayName => "REST API";
    public string Category => "API";
    public string Icon => "🌐";
    public IReadOnlyList<string> ConfigKeys => ["baseUrl", "headers", "bearerToken"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
    [
        new() { Key = "get", Name = "GET", Inputs = ["path", "query"] },
        new() { Key = "post", Name = "POST", Inputs = ["path", "body"] },
        new() { Key = "put", Name = "PUT", Inputs = ["path", "body"] },
        new() { Key = "patch", Name = "PATCH", Inputs = ["path", "body"] },
        new() { Key = "delete", Name = "DELETE", Inputs = ["path"] },
    ];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var config = def.Config;
        var client = httpFactory.CreateClient("connector");
        var baseUrl = config.GetValueOrDefault("baseUrl", "").TrimEnd('/');
        var path = input.Text("path");
        if (!path.StartsWith('/') && path.Length > 0) path = "/" + path;
        var url = baseUrl + path + (input.TryGetValue("query", out var q) && q is not null ? "?" + q : "");

        using var request = new HttpRequestMessage(new HttpMethod(action.ToUpperInvariant()), url);
        foreach (var (k, v) in JsonUtil.DeserializeOrNew<Dictionary<string, string>>(config.GetValueOrDefault("headers")))
            request.Headers.TryAddWithoutValidation(k, v);
        var bearer = config.GetValueOrDefault("bearerToken");
        if (!string.IsNullOrEmpty(bearer))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearer}");
        if (input.TryGetValue("body", out var body) && body is not null && action is "post" or "put" or "patch")
            request.Content = new StringContent(
                body as string ?? JsonUtil.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        object? parsed = text;
        try { parsed = JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(text).RootElement); } catch { }
        return new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["isSuccess"] = response.IsSuccessStatusCode,
            ["body"] = parsed
        };
    }
}

public class GraphQlConnector(IHttpClientFactory httpFactory) : IConnector
{
    public string Provider => "graphql";
    public string DisplayName => "GraphQL API";
    public string Category => "API";
    public string Icon => "◈";
    public IReadOnlyList<string> ConfigKeys => ["endpoint", "headers", "bearerToken"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
        [new() { Key = "query", Name = "Execute query/mutation", Inputs = ["query", "variables"] }];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("connector");
        using var request = new HttpRequestMessage(HttpMethod.Post, def.Config.GetValueOrDefault("endpoint"));
        foreach (var (k, v) in JsonUtil.DeserializeOrNew<Dictionary<string, string>>(def.Config.GetValueOrDefault("headers")))
            request.Headers.TryAddWithoutValidation(k, v);
        var bearer = def.Config.GetValueOrDefault("bearerToken");
        if (!string.IsNullOrEmpty(bearer))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearer}");
        var payload = new Dictionary<string, object?>
        {
            ["query"] = input.Text("query"),
            ["variables"] = input.GetValueOrDefault("variables")
        };
        request.Content = new StringContent(JsonUtil.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        try { return JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(text).RootElement); }
        catch { return text; }
    }
}

/// <summary>Posts messages to incoming-webhook URLs (Slack, Teams, Discord, generic).</summary>
public class WebhookConnector(IHttpClientFactory httpFactory) : IConnector
{
    public string Provider => "webhook";
    public string DisplayName => "Chat Webhook (Slack/Teams/Discord)";
    public string Category => "Messaging";
    public string Icon => "💬";
    public IReadOnlyList<string> ConfigKeys => ["url", "format"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
        [new() { Key = "post_message", Name = "Post message", Inputs = ["text"] },
         new() { Key = "post_json", Name = "Post raw JSON", Inputs = ["json"] }];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("connector");
        string payload;
        if (action == "post_json")
        {
            var raw = input.GetValueOrDefault("json");
            payload = raw as string ?? JsonUtil.Serialize(raw);
        }
        else
        {
            var text = input.Text("text");
            payload = (def.Config.GetValueOrDefault("format", "slack")) switch
            {
                "discord" => JsonUtil.Serialize(new Dictionary<string, object?> { ["content"] = text }),
                _ => JsonUtil.Serialize(new Dictionary<string, object?> { ["text"] = text }) // slack & teams
            };
        }
        using var response = await client.PostAsync(def.Config.GetValueOrDefault("url"),
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        return new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["isSuccess"] = response.IsSuccessStatusCode
        };
    }
}

// ------------------------------------------------------------------ databases

public abstract class SqlConnectorBase : IConnector
{
    public abstract string Provider { get; }
    public abstract string DisplayName { get; }
    public string Category => "Database";
    public virtual string Icon => "🛢️";
    public IReadOnlyList<string> ConfigKeys => ["connectionString"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
    [
        new() { Key = "query", Name = "Query (rows)", Inputs = ["sql"] },
        new() { Key = "execute", Name = "Execute (non-query)", Inputs = ["sql"] },
        new() { Key = "scalar", Name = "Scalar", Inputs = ["sql"] },
    ];

    protected abstract DbConnection CreateConnection(string connectionString);

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var sql = input.Text("sql");
        if (string.IsNullOrWhiteSpace(sql)) throw new InvalidOperationException("'sql' input is required.");
        await using var connection = CreateConnection(def.Config.GetValueOrDefault("connectionString", ""));
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        // named parameters: input keys other than "sql" become @name parameters
        foreach (var (k, v) in input.Where(kv => kv.Key != "sql"))
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + k;
            parameter.Value = v ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        switch (action)
        {
            case "execute":
                return await command.ExecuteNonQueryAsync(ct);
            case "scalar":
                return await command.ExecuteScalarAsync(ct);
            default:
                var rows = new List<Dictionary<string, object?>>();
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var row = new Dictionary<string, object?>();
                        for (var i = 0; i < reader.FieldCount; i++)
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                    }
                }
                return rows;
        }
    }
}

public class SqlServerConnector : SqlConnectorBase
{
    public override string Provider => "sqlserver";
    public override string DisplayName => "SQL Server";
    protected override DbConnection CreateConnection(string cs) => new Microsoft.Data.SqlClient.SqlConnection(cs);
}

public class PostgresConnector : SqlConnectorBase
{
    public override string Provider => "postgres";
    public override string DisplayName => "PostgreSQL";
    public override string Icon => "🐘";
    protected override DbConnection CreateConnection(string cs) => new Npgsql.NpgsqlConnection(cs);
}

public class MySqlConnectorProvider : SqlConnectorBase
{
    public override string Provider => "mysql";
    public override string DisplayName => "MySQL / MariaDB";
    public override string Icon => "🐬";
    protected override DbConnection CreateConnection(string cs) => new MySqlConnector.MySqlConnection(cs);
}

public class SqliteConnector : SqlConnectorBase
{
    public override string Provider => "sqlite";
    public override string DisplayName => "SQLite";
    protected override DbConnection CreateConnection(string cs) => new Microsoft.Data.Sqlite.SqliteConnection(cs);
}

// ------------------------------------------------------------------ storage

public class S3Connector : IConnector
{
    public string Provider => "s3";
    public string DisplayName => "S3 / MinIO";
    public string Category => "Storage";
    public string Icon => "🪣";
    public IReadOnlyList<string> ConfigKeys => ["serviceUrl", "region", "accessKey", "secretKey", "bucket"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
    [
        new() { Key = "list", Name = "List objects", Inputs = ["prefix"] },
        new() { Key = "read_text", Name = "Read text object", Inputs = ["key"] },
        new() { Key = "write_text", Name = "Write text object", Inputs = ["key", "content"] },
        new() { Key = "delete", Name = "Delete object", Inputs = ["key"] },
    ];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var config = def.Config;
        var s3Config = new AmazonS3Config { ForcePathStyle = true };
        var serviceUrl = config.GetValueOrDefault("serviceUrl");
        if (!string.IsNullOrEmpty(serviceUrl)) s3Config.ServiceURL = serviceUrl;
        else s3Config.AuthenticationRegion = config.GetValueOrDefault("region", "us-east-1");
        using var client = new AmazonS3Client(config.GetValueOrDefault("accessKey"), config.GetValueOrDefault("secretKey"), s3Config);
        var bucket = config.GetValueOrDefault("bucket", "appbender");

        switch (action)
        {
            case "list":
                var listed = await client.ListObjectsV2Async(new ListObjectsV2Request
                { BucketName = bucket, Prefix = input.Text("prefix") }, ct);
                return listed.S3Objects?.Select(o => (object?)o.Key).ToList() ?? [];
            case "read_text":
                using (var obj = await client.GetObjectAsync(bucket, input.Text("key"), ct))
                using (var reader = new StreamReader(obj.ResponseStream))
                    return await reader.ReadToEndAsync(ct);
            case "write_text":
                await client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucket, Key = input.Text("key"),
                    ContentBody = input.Text("content"), ContentType = "text/plain"
                }, ct);
                return input.Text("key");
            case "delete":
                await client.DeleteObjectAsync(bucket, input.Text("key"), ct);
                return true;
            default:
                throw new InvalidOperationException($"Unknown S3 action '{action}'.");
        }
    }
}

public class AzureBlobConnector : IConnector
{
    public string Provider => "azureblob";
    public string DisplayName => "Azure Blob Storage";
    public string Category => "Storage";
    public string Icon => "☁️";
    public IReadOnlyList<string> ConfigKeys => ["connectionString", "container"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
    [
        new() { Key = "list", Name = "List blobs", Inputs = ["prefix"] },
        new() { Key = "read_text", Name = "Read text blob", Inputs = ["key"] },
        new() { Key = "write_text", Name = "Write text blob", Inputs = ["key", "content"] },
        new() { Key = "delete", Name = "Delete blob", Inputs = ["key"] },
    ];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var container = new BlobContainerClient(
            def.Config.GetValueOrDefault("connectionString"),
            def.Config.GetValueOrDefault("container", "appbender"));
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        switch (action)
        {
            case "list":
                var names = new List<object?>();
                await foreach (var blob in container.GetBlobsAsync(
                    Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None,
                    input.Text("prefix"), ct))
                    names.Add(blob.Name);
                return names;
            case "read_text":
                var download = await container.GetBlobClient(input.Text("key")).DownloadContentAsync(ct);
                return download.Value.Content.ToString();
            case "write_text":
                await container.GetBlobClient(input.Text("key"))
                    .UploadAsync(BinaryData.FromString(input.Text("content")), overwrite: true, ct);
                return input.Text("key");
            case "delete":
                await container.DeleteBlobIfExistsAsync(input.Text("key"), cancellationToken: ct);
                return true;
            default:
                throw new InvalidOperationException($"Unknown Azure Blob action '{action}'.");
        }
    }
}

public class FileSystemConnector : IConnector
{
    public string Provider => "filesystem";
    public string DisplayName => "File System";
    public string Category => "Storage";
    public string Icon => "📁";
    public IReadOnlyList<string> ConfigKeys => ["basePath"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
    [
        new() { Key = "list", Name = "List files", Inputs = ["prefix"] },
        new() { Key = "read_text", Name = "Read text file", Inputs = ["key"] },
        new() { Key = "write_text", Name = "Write text file", Inputs = ["key", "content"] },
        new() { Key = "delete", Name = "Delete file", Inputs = ["key"] },
    ];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var basePath = def.Config.GetValueOrDefault("basePath", Path.Combine(AppContext.BaseDirectory, "storage", "connector"));
        Directory.CreateDirectory(basePath);
        string Resolve(string key)
        {
            var full = Path.GetFullPath(Path.Combine(basePath, key.Replace('\\', '/').TrimStart('/')));
            if (!full.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escapes the connector base path.");
            return full;
        }

        switch (action)
        {
            case "list":
                var dir = Resolve(input.Text("prefix", "."));
                if (!Directory.Exists(dir)) return new List<object?>();
                return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Select(f => (object?)Path.GetRelativePath(basePath, f).Replace('\\', '/')).ToList();
            case "read_text":
                var readPath = Resolve(input.Text("key"));
                return File.Exists(readPath) ? await File.ReadAllTextAsync(readPath, ct) : null;
            case "write_text":
                var writePath = Resolve(input.Text("key"));
                Directory.CreateDirectory(Path.GetDirectoryName(writePath)!);
                await File.WriteAllTextAsync(writePath, input.Text("content"), ct);
                return input.Text("key");
            case "delete":
                var deletePath = Resolve(input.Text("key"));
                if (File.Exists(deletePath)) File.Delete(deletePath);
                return true;
            default:
                throw new InvalidOperationException($"Unknown file system action '{action}'.");
        }
    }
}

// ------------------------------------------------------------------ messaging & search

public class SmtpConnector(IEmailService email) : IConnector
{
    public string Provider => "smtp";
    public string DisplayName => "Email (SMTP)";
    public string Category => "Messaging";
    public string Icon => "📧";
    public IReadOnlyList<string> ConfigKeys => ["Host", "Port", "Username", "Password", "From", "UseSsl"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
        [new() { Key = "send", Name = "Send email", Inputs = ["to", "subject", "body", "cc"] }];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        await email.SendAsync(input.Text("to"), input.Text("subject"), input.Text("body"),
            input.Text("cc") is { Length: > 0 } cc ? cc : null, def.Config);
        return "sent";
    }
}

public class TavilyConnector(IWebSearchClient search) : IConnector
{
    public string Provider => "tavily";
    public string DisplayName => "Tavily Web Search";
    public string Category => "AI";
    public string Icon => "🔍";
    public IReadOnlyList<string> ConfigKeys => ["apiKey"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions =>
        [new() { Key = "search", Name = "Search", Inputs = ["query", "maxResults"] }];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var max = int.TryParse(input.Text("maxResults", "5"), out var m) ? m : 5;
        var results = await search.SearchAsync(input.Text("query"), max, ct);
        return results.Select(r => new Dictionary<string, object?>
        { ["title"] = r.Title, ["url"] = r.Url, ["snippet"] = r.Snippet }).ToList();
    }
}

// ------------------------------------------------------------------ custom (JSON-defined)

/// <summary>Runs actions defined declaratively in a CustomConnectorSpec (Custom Connector Builder).</summary>
public class CustomConnector(IHttpClientFactory httpFactory) : IConnector
{
    public string Provider => "custom";
    public string DisplayName => "Custom Connector";
    public string Category => "Custom";
    public string Icon => "🧩";
    public IReadOnlyList<string> ConfigKeys => ["apiKey", "username", "password"];
    public IReadOnlyList<ConnectorActionDescriptor> Actions => [];

    public async Task<object?> ExecuteAsync(ConnectorDefinition def, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var spec = def.Spec;
        var op = spec.Actions.FirstOrDefault(a => a.Key == action)
            ?? throw new InvalidOperationException($"Custom connector '{def.Name}' has no action '{action}'.");

        var templateContext = input.ToDictionary(kv => kv.Key, kv => kv.Value);
        var path = TemplateEngine.Render(op.PathTemplate, templateContext);
        var url = spec.BaseUrl.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);

        var config = def.Config;
        if (spec.AuthType == "apikey_query" && !string.IsNullOrEmpty(spec.AuthParamName))
            url += (url.Contains('?') ? "&" : "?") + $"{spec.AuthParamName}={Uri.EscapeDataString(config.GetValueOrDefault("apiKey", ""))}";

        var client = httpFactory.CreateClient("connector");
        using var request = new HttpRequestMessage(new HttpMethod(op.Method.ToUpperInvariant()), url);

        foreach (var (k, v) in spec.DefaultHeaders)
            request.Headers.TryAddWithoutValidation(k, TemplateEngine.Render(v, templateContext));
        foreach (var (k, v) in op.Headers)
            request.Headers.TryAddWithoutValidation(k, TemplateEngine.Render(v, templateContext));

        switch (spec.AuthType)
        {
            case "apikey_header" when !string.IsNullOrEmpty(spec.AuthParamName):
                request.Headers.TryAddWithoutValidation(spec.AuthParamName, config.GetValueOrDefault("apiKey", ""));
                break;
            case "bearer":
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {config.GetValueOrDefault("apiKey", "")}");
                break;
            case "basic":
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{config.GetValueOrDefault("username", "")}:{config.GetValueOrDefault("password", "")}"));
                request.Headers.TryAddWithoutValidation("Authorization", $"Basic {credentials}");
                break;
        }

        if (!string.IsNullOrEmpty(op.BodyTemplate) && op.Method.ToUpperInvariant() is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent(
                TemplateEngine.Render(op.BodyTemplate, templateContext), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        object? parsed = text;
        try { parsed = JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(text).RootElement); } catch { }
        return new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["isSuccess"] = response.IsSuccessStatusCode,
            ["body"] = parsed
        };
    }
}
