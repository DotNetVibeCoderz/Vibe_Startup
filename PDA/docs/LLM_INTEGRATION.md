# 🤖 LLM Integration Guide

Panduan integrasi berbagai LLM provider di PDA.

---

## Supported Providers

| Provider | API | Auth | Models |
|----------|-----|------|--------|
| **OpenAI** | `https://api.openai.com/v1` | API Key (Bearer) | GPT-4o, GPT-4o-mini, GPT-4-turbo, GPT-3.5-turbo |
| **Anthropic** | `https://api.anthropic.com/v1` | API Key (x-api-key) | Claude 3.5 Sonnet, Claude 3 Opus, Claude 3 Haiku |
| **Gemini** | `https://generativelanguage.googleapis.com/v1beta` | API Key (query) | Gemini 2.0 Flash, Gemini 1.5 Pro, Gemini 1.5 Flash |
| **Ollama** | `http://localhost:11434` | None | llama3.1, mistral, codellama, phi3, dll |
| **OpenAI Compatible** | Custom | Custom | Custom |

---

## Provider Architecture

```
┌──────────────────────────────────────┐
│          LlmProviderFactory          │
│                                      │
│  CreateProvider(config) → ILlmProvider│
└──────────────┬───────────────────────┘
               │
       ┌───────┼───────────┬──────────────┐
       ▼       ▼           ▼              ▼
┌─────────┐ ┌─────────┐ ┌────────┐ ┌──────────┐
│OpenAI   │ │Anthropic│ │Gemini  │ │Ollama    │
│Provider │ │Provider │ │Provider│ │Provider  │
└─────────┘ └─────────┘ └────────┘ └──────────┘
       │       │           │              │
       └───────┴───────────┴──────────────┘
                      │
              Semua implementasi:
              - ChatAsync(messages, tools)
              - StreamChatAsync(messages, tools)
              - CountTokensAsync(text)
```

---

## OpenAI Setup

### 1. Dapatkan API Key
1. Buka [platform.openai.com/api-keys](https://platform.openai.com/api-keys)
2. Klik "Create new secret key"
3. Copy API key

### 2. Konfigurasi

```json
"LLM": {
  "DefaultProvider": "OpenAI",
  "DefaultModel": "gpt-4o",
  "Providers": {
    "OpenAI": {
      "ApiKey": "sk-proj-...",
      "Endpoint": "https://api.openai.com/v1",
      "Models": ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo"]
    }
  }
}
```

### 3. Model Recommendations

| Model | Use Case | Cost |
|-------|----------|------|
| **gpt-4o** | Analisis kompleks, dashboard | $$$ |
| **gpt-4o-mini** | Query SQL, analisis sederhana | $ |
| **gpt-4-turbo** | Analisis data besar | $$$ |
| **gpt-3.5-turbo** | Prototyping, testing | $ |

### 4. Pricing (per 1M tokens)
- GPT-4o: ~$2.50 input / $10 output
- GPT-4o-mini: ~$0.15 input / $0.60 output

---

## Anthropic Setup

### 1. Dapatkan API Key
1. Buka [console.anthropic.com](https://console.anthropic.com/)
2. Generate API key

### 2. Konfigurasi

```json
"Anthropic": {
  "ApiKey": "sk-ant-api03-...",
  "Endpoint": "https://api.anthropic.com/v1",
  "Models": ["claude-3-5-sonnet-20241022", "claude-3-opus-20240229"]
}
```

### 3. Fitur Khusus
- System prompt dipisah (tidak seperti OpenAI)
- Tools menggunakan `input_schema` format
- API version header: `anthropic-version: 2023-06-01`

---

## Gemini Setup

### 1. Dapatkan API Key
1. Buka [aistudio.google.com](https://aistudio.google.com/)
2. Get API key (gratis untuk development!)

### 2. Konfigurasi

```json
"Gemini": {
  "ApiKey": "AIza...",
  "Endpoint": "https://generativelanguage.googleapis.com/v1beta",
  "Models": ["gemini-2.0-flash", "gemini-1.5-pro"]
}
```

### 3. Fitur Khusus
- API key via query parameter (`?key=...`)
- Role mapping: `assistant` → `model`
- Gratis tier untuk development
- Gemini 2.0 Flash sangat cepat dan murah

---

## Ollama Setup (Local - Free)

Ollama adalah opsi **GRATIS** untuk development!

### 1. Install Ollama
```bash
# Linux/Mac
curl -fsSL https://ollama.com/install.sh | sh

# Windows
# Download dari https://ollama.com
```

### 2. Pull Model
```bash
ollama pull llama3.1     # ~4.7 GB
ollama pull mistral       # ~4.1 GB
ollama pull codellama     # ~3.8 GB
ollama pull phi3          # ~2.3 GB (ringan)
```

### 3. Konfigurasi

```json
"LLM": {
  "DefaultProvider": "Ollama",
  "DefaultModel": "llama3.1",
  "Providers": {
    "Ollama": {
      "ApiKey": "",
      "Endpoint": "http://localhost:11434",
      "Models": ["llama3.1", "mistral", "codellama", "phi3"]
    }
  }
}
```

### 4. Kelebihan & Kekurangan

| ✅ Kelebihan | ❌ Kekurangan |
|-------------|--------------|
| Gratis 100% | Butuh RAM/GPU |
| Data lokal (privacy) | Lebih lambat dari cloud |
| No rate limit | Model lebih kecil |
| No internet needed | Fitur tools terbatas |

### 5. Hardware Requirements

| Model | RAM Minimum | RAM Recommended |
|-------|------------|-----------------|
| phi3 | 4 GB | 8 GB |
| llama3.1 (8B) | 8 GB | 16 GB |
| mistral (7B) | 8 GB | 16 GB |
| codellama (7B) | 8 GB | 16 GB |

---

## Tool Calling Compatibility

| Feature | OpenAI | Anthropic | Gemini | Ollama |
|---------|--------|-----------|--------|--------|
| Function Calling | ✅ Native | ✅ Native | ⚠️ Manual | ⚠️ Manual |
| System Prompt | ✅ | ✅ | ✅ | ✅ |
| Streaming | ✅ | ✅ | ✅ | ✅ |
| JSON Mode | ✅ | ✅ | ✅ | ⚠️ Limited |

---

## Best Practices

### 1. Temperature Settings

```
Data Query:    0.0 - 0.2  (presisi)
Data Analysis: 0.3 - 0.5  (seimbang)
Creative:      0.7 - 1.0  (kreatif)
```

### 2. Cost Optimization
- Gunakan **Ollama** untuk development
- Gunakan **gpt-4o-mini** untuk query sederhana
- Gunakan **gpt-4o** hanya untuk analisis kompleks
- Cache schema context (tidak perlu re-extract tiap request)

### 3. Error Handling
```csharp
// Sudah di-handle di OpenAiProvider.ChatAsync()
catch (Exception ex)
{
    return new LlmResponse 
    { 
        IsSuccess = false, 
        ErrorMessage = ex.Message 
    };
}
```

### 4. Token Management
- System prompt + schema context: ~500-2000 tokens
- History: last 20 messages
- Response: max 4096 tokens
- Total context window: ~8K-128K (model dependent)

---

## Adding New Provider

Untuk menambahkan provider baru:

1. Buat class implementing `ILlmProvider`
2. Daftarkan di `LlmProviderFactory.CreateProvider()`
3. Tambahkan konfigurasi di `appsettings.json`

```csharp
public class CustomProvider : ILlmProvider
{
    public async Task<LlmResponse> ChatAsync(List<LlmMessage> messages, List<LlmTool>? tools = null)
    {
        // Implementasi custom
    }
    
    public async IAsyncEnumerable<string> StreamChatAsync(...)
    {
        // Implementasi streaming
    }
    
    public Task<int> CountTokensAsync(string text)
    {
        // Token counting
    }
}
```
