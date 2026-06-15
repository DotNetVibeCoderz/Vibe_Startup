#nullable disable
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SoccerWizard.Services.VectorData;
using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;

namespace SoccerWizard.Services.LLM;

public class SemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly VectorDataService _vectorData;
    private readonly HttpClient _httpClient;
    private readonly string _defaultProvider;
    private readonly string _baseUrl;
    private readonly bool _kernelReady;

    private readonly string _openAiKey;
    private readonly string _geminiKey;
    private readonly string _anthropicKey;
    private readonly string _ollamaEndpoint;
    private readonly string _tavilyKey;

    public string ActiveProvider => _defaultProvider;
    public bool IsKernelReady => _kernelReady;

    public SemanticKernelService(IConfiguration config, VectorDataService vectorData, HttpClient httpClient)
    {
        _vectorData = vectorData; _httpClient = httpClient;
        _defaultProvider = config["LLM:DefaultProvider"] ?? "Ollama";
        _baseUrl = config["AppSettings:BaseUrl"] ?? "https://localhost:5001";
        _openAiKey = config["LLM:OpenAI:ApiKey"];
        _geminiKey = config["LLM:Gemini:ApiKey"];
        _anthropicKey = config["LLM:Anthropic:ApiKey"];
        _ollamaEndpoint = config["LLM:Ollama:Endpoint"] ?? "http://localhost:11434";
        _tavilyKey = config["Tavily:ApiKey"];

        try
        {
            var builder = Kernel.CreateBuilder();
            if (!string.IsNullOrEmpty(_openAiKey))
                builder.AddOpenAIChatCompletion(config["LLM:OpenAI:Model"] ?? "gpt-4o-mini", _openAiKey);
            _kernel = builder.Build();

            // Register common kernel functions
            _kernel.Plugins.AddFromObject(new KernelUtilityFunctions(_httpClient, _tavilyKey), "utils");

            _chatService = _kernel.GetRequiredService<IChatCompletionService>();
            _kernelReady = true;
        }
        catch { _kernelReady = false; }
    }

    // ==================== COMMON KERNEL FUNCTIONS ====================
    /// <summary>Ambil waktu UTC dan lokal saat ini.</summary>
    public static (DateTime Utc, DateTime Local) GetCurrentDateTime() => (DateTime.UtcNow, DateTime.Now);

    /// <summary>Kalkulasi matematika sederhana (contoh: "(10+2)*3/4").</summary>
    public static double CalculateMath(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0;
        var cleaned = Regex.Replace(formula, @"[^0-9+\-*/().,%\s]", "");
        var dt = new DataTable();
        var value = dt.Compute(cleaned, "");
        return Convert.ToDouble(value);
    }

    /// <summary>Ambil cuaca saat ini via Open-Meteo (gratis, tanpa API key).</summary>
    public async Task<string> GetWeatherAsync(double latitude, double longitude)
    {
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,wind_speed_10m,weather_code";
        var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return "Weather data unavailable";
        var json = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var current = json.RootElement.GetProperty("current");
        var temp = current.GetProperty("temperature_2m").GetDouble();
        var wind = current.GetProperty("wind_speed_10m").GetDouble();
        var code = current.GetProperty("weather_code").GetInt32();
        return $"Temperature {temp}°C, Wind {wind} km/h, Code {code}";
    }

    /// <summary>Search internet via Tavily.</summary>
    public async Task<string> SearchInternetTavilyAsync(string query, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(_tavilyKey)) return "Tavily API key not configured";
        var body = new { api_key = _tavilyKey, query, max_results = maxResults, include_answer = false };
        var res = await PostJsonRaw("https://api.tavily.com/search", body);
        return res;
    }

    /// <summary>Scrap web page (text-only, strip HTML tags).</summary>
    public async Task<string> ScrapeWebPageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "URL is required";
        var html = await _httpClient.GetStringAsync(url);
        var text = Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<.*?>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length > 4000 ? text[..4000] + "..." : text;
    }

    /// <summary>Download file bytes from URL.</summary>
    public async Task<byte[]> ReadFileFromUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Array.Empty<byte>();
        return await _httpClient.GetByteArrayAsync(url);
    }

    /// <summary>Simple HEAD check to see if URL is reachable.</summary>
    public async Task<bool> UrlExistsAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        using var req = new HttpRequestMessage(HttpMethod.Head, url);
        using var resp = await _httpClient.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>Ping a URL to get status code.</summary>
    public async Task<int> GetUrlStatusAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return 0;
        using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        return (int)resp.StatusCode;
    }

    // ==================== CHAT ====================
    public async Task<string> ChatAsync(string userMessage, string systemPrompt = "", List<(string Name, string Content)> kernelContext = null)
    {
        if (_defaultProvider == "OpenAI" && _kernelReady)
            try { return await ChatViaSKAsync(userMessage, systemPrompt, kernelContext); } catch { }
        try { return await CallLLMHttpAsync(BuildPrompt(userMessage, systemPrompt, kernelContext)); }
        catch { return Fallback(userMessage); }
    }

    public async Task<string> ChatWithRagAsync(string userMessage, string systemPrompt = "")
    {
        var ctx = new List<(string, string)>();
        if (_vectorData != null && !string.IsNullOrEmpty(_ollamaEndpoint))
        {
            try
            {
                var body = new { model = "nomic-embed-text", prompt = userMessage, stream = false };
                var resp = await _httpClient.PostAsync($"{_ollamaEndpoint}/api/embeddings",
                    new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    var j = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    var arr = j.RootElement.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray();
                    var results = await _vectorData.SearchAsync(new ReadOnlyMemory<float>(arr), topK: 3);
                    foreach (var r in results) ctx.Add((r.Metadata.GetValueOrDefault("source", "unknown"), r.Text));
                }
            }
            catch { }
        }
        return await ChatAsync(userMessage, systemPrompt, ctx);
    }

    public async Task<string> ChatWithImagesAsync(string userMessage, List<string> imageUrls, string systemPrompt = "")
    {
        var imgs = new List<(string mime, string b64)>();
        foreach (var url in imageUrls)
        {
            try
            {
                var fullUrl = url.StartsWith("http") ? url : $"{_baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
                var bytes = await _httpClient.GetByteArrayAsync(fullUrl);
                var b64 = Convert.ToBase64String(bytes);
                var ext = Path.GetExtension(url).ToLowerInvariant();
                var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", _ => "image/png" };
                imgs.Add((mime, b64));
            }
            catch { }
        }
        if (!imgs.Any())
        {
            var ul = string.Join("\n", imageUrls.Select(u => $"- {u}"));
            return await ChatAsync($"{userMessage}\n\n[Images: {ul}]", systemPrompt);
        }
        if (_defaultProvider == "OpenAI" && _kernelReady)
            try { return await OpenAIMultimodalSKAsync(userMessage, imgs, systemPrompt); } catch { }
        try { return await CallMultimodalHttpAsync(userMessage, imgs); }
        catch { return await ChatAsync(userMessage, systemPrompt); }
    }

    // ==================== SK Chat ====================
    private async Task<string> ChatViaSKAsync(string msg, string sys, List<(string Name, string Content)> ctx)
    {
        var h = new ChatHistory();
        h.AddSystemMessage(string.IsNullOrEmpty(sys)
            ? "You are SoccerWizard AI, football expert. You can call tools when needed. Be concise."
            : sys + "\nYou can call tools when needed.");

        if (ctx?.Any() == true)
            h.AddSystemMessage("Context:\n" + string.Join("\n", ctx.Select(k => $"[{k.Name}]: {k.Content}")));

        h.AddUserMessage(msg);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.7,
            MaxTokens = 700,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var r = await _chatService.GetChatMessageContentAsync(h, settings, _kernel);
        return r.Content ?? Fallback(msg);
    }

    private async Task<string> OpenAIMultimodalSKAsync(string msg, List<(string mime, string b64)> imgs, string systemPrompt)
    {
        var h = new ChatHistory();
        h.AddSystemMessage(string.IsNullOrWhiteSpace(systemPrompt)
            ? "You are SoccerWizard AI, a football expert that analyzes images."
            : systemPrompt);
        var items = new ChatMessageContentItemCollection();
        foreach (var (mime, b64) in imgs)
            items.Add(new ImageContent(new BinaryData(Convert.FromBase64String(b64)), mime));
        items.Add(new TextContent(string.IsNullOrWhiteSpace(msg) ? "Please analyze this image." : msg));
        h.AddUserMessage(items);
        var r = await _chatService.GetChatMessageContentAsync(h);
        return r.Content ?? "Image analyzed.";
    }

    // ==================== Custom HTTP ====================
    private async Task<string> CallLLMHttpAsync(string prompt) => _defaultProvider switch
    {
        "OpenAI" => await CallOpenAIAsync(prompt),
        "Gemini" => await CallGeminiAsync(prompt),
        "Anthropic" => await CallAnthropicAsync(prompt),
        "Ollama" => await CallOllamaAsync(prompt),
        _ => Fallback(prompt)
    };

    private async Task<string> CallMultimodalHttpAsync(string msg, List<(string mime, string b64)> imgs) => _defaultProvider switch
    {
        "OpenAI" => await OpenAIMultimodalHttpAsync(msg, imgs),
        "Gemini" => await GeminiMultimodalHttpAsync(msg, imgs),
        "Anthropic" => await AnthropicMultimodalHttpAsync(msg, imgs),
        "Ollama" => await OllamaMultimodalHttpAsync(msg, imgs),
        _ => await ChatAsync(msg)
    };

    // ---- OpenAI ----
    private async Task<string> CallOpenAIAsync(string prompt) =>
        await PostJson("https://api.openai.com/v1/chat/completions",
            new { model = "gpt-4o-mini", messages = new[] { new { role = "user", content = prompt } }, max_tokens = 500, temperature = 0.7 },
            ("Authorization", $"Bearer {_openAiKey}"),
            j => j.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "");

    private async Task<string> OpenAIMultimodalHttpAsync(string msg, List<(string mime, string b64)> imgs)
    {
        var cl = new List<object>();
        foreach (var (mime, b64) in imgs) cl.Add(new { type = "image_url", image_url = new { url = $"data:{mime};base64,{b64}" } });
        cl.Add(new { type = "text", text = string.IsNullOrWhiteSpace(msg) ? "Please analyze." : msg });
        return await PostJson("https://api.openai.com/v1/chat/completions",
            new { model = "gpt-4o-mini", messages = new[] { new { role = "user", content = (object)cl } }, max_tokens = 800, temperature = 0.7 },
            ("Authorization", $"Bearer {_openAiKey}"),
            j => j.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "");
    }

    // ---- Gemini ----
    private async Task<string> CallGeminiAsync(string prompt) =>
        await PostJson($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiKey}",
            new { contents = new[] { new { parts = new[] { new { text = prompt } } } } }, null,
            j => j.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "");

    private async Task<string> GeminiMultimodalHttpAsync(string msg, List<(string mime, string b64)> imgs)
    {
        var parts = new List<object>();
        foreach (var (mime, b64) in imgs) parts.Add(new { inline_data = new { mime_type = mime, data = b64 } });
        parts.Add(new { text = string.IsNullOrWhiteSpace(msg) ? "Please analyze." : msg });
        return await PostJson($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiKey}",
            new { contents = new[] { new { parts = parts.ToArray() } } }, null,
            j => j.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "");
    }

    // ---- Anthropic ----
    private async Task<string> CallAnthropicAsync(string prompt) =>
        await PostJson("https://api.anthropic.com/v1/messages",
            new { model = "claude-3-5-sonnet-20241022", max_tokens = 500, messages = new[] { new { role = "user", content = prompt } } },
            ("x-api-key", _anthropicKey),
            j => j.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "");

    private async Task<string> AnthropicMultimodalHttpAsync(string msg, List<(string mime, string b64)> imgs)
    {
        var bl = new List<object>();
        foreach (var (mime, b64) in imgs) bl.Add(new { type = "image", source = new { type = "base64", media_type = mime, data = b64 } });
        bl.Add(new { type = "text", text = string.IsNullOrWhiteSpace(msg) ? "Please analyze." : msg });
        return await PostJson("https://api.anthropic.com/v1/messages",
            new { model = "claude-3-5-sonnet-20241022", max_tokens = 800, messages = new[] { new { role = "user", content = bl.ToArray() } } },
            ("x-api-key", _anthropicKey),
            j => j.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "");
    }

    // ---- Ollama ----
    private async Task<string> CallOllamaAsync(string prompt) =>
        await PostJson($"{_ollamaEndpoint}/api/generate",
            new { model = "llama3.2", prompt, stream = false, options = new { temperature = 0.7, max_tokens = 500 } }, null,
            j => j.RootElement.GetProperty("response").GetString() ?? "", Fallback(prompt));

    private async Task<string> OllamaMultimodalHttpAsync(string msg, List<(string mime, string b64)> imgs) =>
        await PostJson($"{_ollamaEndpoint}/api/generate",
            new { model = "minicpm-v", prompt = msg, images = imgs.Select(i => i.b64).ToList(), stream = false }, null,
            j => j.RootElement.GetProperty("response").GetString() ?? "", await ChatAsync(msg));

    // ==================== HTTP Helper ====================
    private async Task<string> PostJson(string url, object body, (string key, string value)? auth,
        Func<System.Text.Json.JsonDocument, string> extract, string fb = "")
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            if (auth.HasValue) { req.Headers.Add(auth.Value.key, auth.Value.value); if (auth.Value.key == "x-api-key") req.Headers.Add("anthropic-version", "2023-06-01"); }
            req.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            var r = await _httpClient.SendAsync(req); r.EnsureSuccessStatusCode();
            return extract(System.Text.Json.JsonDocument.Parse(await r.Content.ReadAsStringAsync()));
        }
        catch { return fb; }
    }

    private async Task<string> PostJsonRaw(string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        var r = await _httpClient.SendAsync(req);
        return await r.Content.ReadAsStringAsync();
    }

    // ==================== Sentiment & Prediction ====================
    public async Task<(double score, string label, string summary)> AnalyzeSentimentAsync(string text)
    {
        try
        {
            var r = await ChatAsync($"Sentiment JSON only: {{\"score\":-1 to 1,\"label\":\"POSITIVE/NEGATIVE/NEUTRAL\",\"summary\":\"one-line\"}}\nText:{text[..Math.Min(text.Length,1000)]}");
            try { var j = System.Text.Json.JsonDocument.Parse(r); return (j.RootElement.GetProperty("score").GetDouble(), j.RootElement.GetProperty("label").GetString() ?? "NEUTRAL", j.RootElement.GetProperty("summary").GetString() ?? ""); } catch { }
        }
        catch { }
        return SimSentiment(text);
    }

    public async Task<string> GenerateTextPredictionAsync(Models.Match m, Models.Team h, Models.Team a)
    {
        try { return await ChatAsync($"Predict: {h.Name} vs {a.Name}. ELO {h.EloRating:F0}/{a.EloRating:F0}. Score, factors. 3 sentences."); }
        catch { return $"{h.Name} {(h.EloRating > a.EloRating ? 2 : 1)}-{(h.EloRating > a.EloRating ? 1 : 2)} {a.Name}."; }
    }

    // ==================== Fallback ====================
    private string BuildPrompt(string msg, string sys, List<(string, string)> ctx)
    {
        var p = new List<string> { string.IsNullOrEmpty(sys) ? "You are SoccerWizard AI, football expert." : sys };
        if (ctx?.Any() == true) p.Add("Context:\n" + string.Join("\n", ctx.Select(k => $"[{k.Item1}]: {k.Item2}")));
        p.Add($"User: {msg}\nAssistant:"); return string.Join("\n\n", p);
    }

    private string Fallback(string msg)
    {
        var l = msg.ToLower();
        if (l.Contains("prediction")) return "Use Predict Match page. ML.NET + Poisson!";
        if (l.Contains("h2h")) return "H2H records on match detail page.";
        return "I'm SoccerWizard AI! Ask me about football!";
    }

    private (double, string, string) SimSentiment(string t)
    {
        var pos = new[] { "win", "victory", "great", "excellent", "amazing" };
        var neg = new[] { "loss", "defeat", "injury", "crisis", "poor" };
        double s = 0; int c = 0; var l = t.ToLower();
        foreach (var w in pos) { if (l.Contains(w)) { s += 0.3; c++; } }
        foreach (var w in neg) { if (l.Contains(w)) { s -= 0.3; c++; } }
        s = c > 0 ? Math.Clamp(s / c, -1, 1) : 0;
        var lb = s > 0.2 ? "POSITIVE" : s < -0.2 ? "NEGATIVE" : "NEUTRAL";
        return (Math.Round(s, 2), lb, lb == "POSITIVE" ? "Positive" : lb == "NEGATIVE" ? "Negative" : "Neutral");
    }

    // ==================== Kernel Utility Plugin ====================
    private class KernelUtilityFunctions
    {
        private readonly HttpClient _http;
        private readonly string _tavilyKey;

        public KernelUtilityFunctions(HttpClient http, string tavilyKey)
        {
            _http = http;
            _tavilyKey = tavilyKey;
        }

        [KernelFunction, Description("Get current UTC and local time in ISO format")]
        public string GetCurrentDateTime()
        {
            var utc = DateTime.UtcNow.ToString("o");
            var local = DateTime.Now.ToString("o");
            return $"UTC: {utc}, Local: {local}";
        }

        [KernelFunction, Description("Calculate math formula (e.g. (10+2)*3/4)")]
        public double CalculateMath([Description("Math formula")] string formula)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0;
            var cleaned = Regex.Replace(formula, @"[^0-9+\-*/().,%\s]", "");
            var dt = new DataTable();
            var value = dt.Compute(cleaned, "");
            return Convert.ToDouble(value);
        }

        [KernelFunction, Description("Get current weather using latitude and longitude")]
        public async Task<string> GetWeatherAsync(double latitude, double longitude)
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,wind_speed_10m,weather_code";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return "Weather data unavailable";
            var json = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var current = json.RootElement.GetProperty("current");
            var temp = current.GetProperty("temperature_2m").GetDouble();
            var wind = current.GetProperty("wind_speed_10m").GetDouble();
            var code = current.GetProperty("weather_code").GetInt32();
            return $"Temperature {temp}°C, Wind {wind} km/h, Code {code}";
        }

        [KernelFunction, Description("Search internet using Tavily")]
        public async Task<string> SearchInternetTavilyAsync([Description("Search query")] string query, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(_tavilyKey)) return "Tavily API key not configured";
            var body = new { api_key = _tavilyKey, query, max_results = maxResults, include_answer = false };
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            var r = await _http.SendAsync(req);
            return await r.Content.ReadAsStringAsync();
        }

        [KernelFunction, Description("Scrape web page text (HTML stripped)")]
        public async Task<string> ScrapeWebPageAsync([Description("Page URL")] string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "URL is required";
            var html = await _http.GetStringAsync(url);
            var text = Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<.*?>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 4000 ? text[..4000] + "..." : text;
        }

        [KernelFunction, Description("Read file from URL and return Base64 string (max 5MB)")]
        public async Task<string> ReadFileFromUrlAsync([Description("File URL")] string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            var bytes = await _http.GetByteArrayAsync(url);
            if (bytes.Length > 5 * 1024 * 1024) return "File too large (max 5MB)";
            return Convert.ToBase64String(bytes);
        }

        [KernelFunction, Description("Check if URL exists")]
        public async Task<bool> UrlExistsAsync([Description("URL")] string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        [KernelFunction, Description("Get URL status code")]
        public async Task<int> GetUrlStatusAsync([Description("URL")] string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return 0;
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return (int)resp.StatusCode;
        }
    }
}
#pragma warning restore SKEXP0070
#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001
