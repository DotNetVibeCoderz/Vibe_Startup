# 🤖 AI ChatBot "Tante Sherly" — Technical Guide

## Overview

"Tante Sherly" adalah AI assistant yang dibangun dengan **Microsoft Semantic Kernel**, mendukung **4 provider AI** dengan mekanisme **AutoInvokeKernelFunctions** untuk tool calling otomatis.

---

## Architecture

```
User Input → ChatBot.razor → StorageService (upload) → AiChatService.ChatAsync()
    → Build ChatHistory (Text + ImageContent)
    → Semantic Kernel → AI Provider (OpenAI/Anthropic/Gemini/Ollama)
    → AutoInvoke Kernel Functions (25 functions)
    → Response → Save to DB → Render Markdown
```

---

## Supported Models

| Provider | Model | Setup |
|----------|-------|-------|
| **OpenAI** | gpt-4o, gpt-4-turbo | Set `ApiKey` in appsettings |
| **Anthropic** | claude-3-5-sonnet | Set `ApiKey` in appsettings |
| **Gemini** | gemini-2.0-flash | Set `ApiKey` in appsettings |
| **Ollama** | llama3, mistral, etc | Local Ollama server |

---

## AutoInvokeKernelFunctions

```csharp
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()  // 🔑 Auto-invoke
};
```

Ini memungkinkan AI **otomatis** memanggil kernel functions saat dibutuhkan, tanpa user perlu eksplisit menyebut nama function.

Contoh:
- User: "Ada event apa aja sih?" → AI auto-panggil `query_events`
- User: "Budget wedding 200 tamu berapa?" → AI auto-panggil `calculate_budget_estimate`
- User: "Deadline catering 25 Des masih aman?" → AI auto-panggil `check_deadline_status`

---

## ImageContent Support

Saat user upload gambar, image dikirim sebagai `ImageContent` ke AI:

```csharp
if (!string.IsNullOrEmpty(m.ImageUrl))
{
    chatHistory.AddUserMessage([
        new TextContent(m.Content ?? ""),
        new ImageContent(new Uri(m.ImageUrl))  // ← AI Vision
    ]);
}
```

Supported by: OpenAI (GPT-4o), Anthropic (Claude 3.5), Gemini (2.0 Flash)

---

## Kernel Functions Reference

### 🕐 DateTime Functions

| Function | Parameters | Description |
|----------|-----------|-------------|
| `get_current_datetime` | timezone | Current date & time (WIB/WITA/WIT/UTC) |
| `calculate_date_difference` | date1, date2 | Days between dates |
| `get_day_of_week` | date | Day name for a date |
| `check_deadline_status` | deadline, label? | Overdue/today/soon/safe |
| `add_days_to_date` | days, startDate | Add/subtract days |

### 🧮 Math Functions

| Function | Parameters | Description |
|----------|-----------|-------------|
| `calculate_math` | expression | Evaluate math expression |
| `calculate_percentage` | value, total, label? | Percentage calculation |
| `calculate_discount` | price, discountPercent | Discount calculator |
| `calculate_average` | numbers (comma-separated) | Average of numbers |
| `calculate_ratio` | a, b, labelA?, labelB? | Ratio A:B |

### 📏 Conversion Functions

| Function | Parameters | Description |
|----------|-----------|-------------|
| `convert_currency` | amount (IDR), targetCurrency | Currency conversion |
| `convert_units` | value, fromUnit, toUnit | Unit conversion (km, kg, liter, inch) |

### 📝 Text Functions

| Function | Parameters | Description |
|----------|-----------|-------------|
| `format_number` | number, currency? | Format large numbers |
| `summarize_text` | text, maxPoints | Summarize to bullet points |

### 🎲 Tips Functions

| Function | Parameters | Description |
|----------|-----------|-------------|
| `get_random_tip` | category? | Random event planning tip |
| `get_event_checklist` | eventType | Planning checklist |

### 🌐 Internet Functions

| Function | Parameters | Description |
|----------|-----------|-------------|
| `search_internet` | query | Tavily search API |
| `scrape_webpage` | url | Extract web page content |

### 🗄️ Database Functions

| Function | Parameters | Description |
|----------|-----------|-------------|
| `query_events` | keyword | Search events (case-insensitive) |
| `query_vendors` | keyword | Search vendors (name + category) |
| `query_budget` | eventName | Event budget details |
| `query_tasks` | eventName | Event task/checklist |
| `query_seating` | eventName | Seating arrangement |
| `calculate_budget_estimate` | guests, type | Budget calculator |
| `get_dashboard_summary` | - | System overview stats |

---

## System Prompt Configuration

Di `appsettings.json`:
```json
{
  "AI": {
    "ChatBot": {
      "Name": "Tante Sherly",
      "SystemPrompt": "Kamu adalah Tante Sherly, asisten virtual...",
      "Temperature": 0.7,
      "MaxTokens": 2000,
      "TopP": 0.9
    }
  }
}
```

---

## Adding New Kernel Functions

1. Tambahkan method di class `AiKernelFunctions`
2. Beri attribute `[KernelFunction("function_name")]` dan `[Description("...")]`
3. Pastikan method return `string` atau `Task<string>`
4. AI akan otomatis bisa memanggilnya via `FunctionChoiceBehavior.Auto()`

```csharp
[KernelFunction("my_new_function")]
[Description("Deskripsi function untuk AI")]
public async Task<string> MyNewFunction(
    [Description("Parameter description")] string param)
{
    // Implementation
    return "result";
}
```
