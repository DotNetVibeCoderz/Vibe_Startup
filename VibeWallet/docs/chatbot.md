# Chat Bot "Mbak Selvi" - Setup & Configuration

## Overview

Mbak Selvi is VibeWallet's AI-powered virtual assistant built with **Microsoft Semantic Kernel**. She can help users with various tasks from checking balance to finding promos.

## Architecture

```
┌─────────────────────────────────────────┐
│           ChatPage.razor                 │
│  (Blazor Server UI Component)           │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│         ChatService.cs                   │
│  (Business Logic & SK Integration)      │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│      Semantic Kernel + AI Models         │
│  OpenAI | Anthropic | Gemini | Ollama   │
└─────────────────────────────────────────┘
```

## Configuration

All chat bot settings are in `appsettings.json` under the `ChatBot` section:

```json
{
  "ChatBot": {
    "Name": "Mbak Selvi",
    "Provider": "OpenAI",
    "SystemPrompt": "...",
    "Temperature": 0.7,
    "MaxTokens": 2000,
    "TopP": 0.9,
    "Models": {
      "OpenAI": {
        "ApiKey": "sk-your-key-here",
        "ModelId": "gpt-4o",
        "Endpoint": ""
      },
      "Anthropic": {
        "ApiKey": "sk-ant-your-key-here",
        "ModelId": "claude-3-5-sonnet-20241022",
        "Endpoint": "https://api.anthropic.com/v1/messages"
      },
      "Gemini": {
        "ApiKey": "your-gemini-key",
        "ModelId": "gemini-1.5-pro",
        "Endpoint": ""
      },
      "Ollama": {
        "ModelId": "llama3.2",
        "Endpoint": "http://localhost:11434"
      }
    },
    "Tavily": {
      "ApiKey": "tvly-your-key",
      "Endpoint": "https://api.tavily.com/search"
    }
  }
}
```

## Model Support

### OpenAI
- `gpt-4o` (recommended)
- `gpt-4-turbo`
- `gpt-3.5-turbo`

Set `ApiKey` in configuration.

### Anthropic (Claude)
- `claude-3-5-sonnet-20241022` (recommended)
- `claude-3-opus-20240229`

Set `ApiKey` and `Endpoint`.

### Google Gemini
- `gemini-1.5-pro` (recommended)
- `gemini-1.5-flash`

Set `ApiKey`.

### Ollama (Local)
- `llama3.2` (recommended)
- `mistral`
- Any model pulled in Ollama

Set `Endpoint` to your Ollama server. No API key needed.

## System Prompt

The system prompt defines Mbak Selvi's personality:

```
Kamu adalah Mbak Selvi, asisten virtual yang ramah dan helpful 
dari VibeWallet, aplikasi dompet digital terkemuka di Indonesia.

Kamu membantu pengguna dengan pertanyaan seputar fitur VibeWallet, 
transaksi, saldo, pembayaran, dan layanan keuangan lainnya.

Gunakan bahasa Indonesia yang santai dan ramah, sesekali gunakan 
kata 'kak', 'sahabat VibeWallet', atau 'bestie'.
```

## Kernel Functions (Tools)

Mbak Selvi has access to these built-in functions:

### 🌐 `SearchInternet(string query)`
Search the internet using Tavily API.

### 📄 `ScrapWebPage(string url)`
Scrape and extract text from a web page.

### 📖 `ReadFileFromUrl(string url)`
Read file content from a URL.

### 🕐 `GetCurrentDateTime(string timezone)`
Get current date and time for a timezone.

### 🧮 `CalculateMath(string expression)`
Evaluate mathematical expressions.

### 🗄️ `QueryDatabase(string queryContext)`
Query VibeWallet database for user info, transactions, promos, etc.

### 💰 Built-in Context
The chat automatically receives user context:
- User name
- Wallet balance
- Loyalty points
- KYC status
- Wallet number

## Markdown Rendering

Mbak Selvi's responses support full Markdown rendering:
- **Bold**, *italic*, ~~strikethrough~~
- Headers (H1-H6)
- Tables with alignment
- Code blocks with syntax highlighting
- Images, video, audio embeds
- Task lists
- Emoji 😊
- Auto-links

## Fallback Mode

If no AI API key is configured, Mbak Selvi works in "fallback mode" with pre-programmed responses for common queries:
- Balance checking
- Promo inquiries
- Transfer information
- Top-up guidance

## UI Features

- **Multi-session**: Create, delete, and manage multiple chat sessions
- **Session reset**: Clear conversation history
- **File attachments**: Upload images and documents
- **Model switching**: Change AI model mid-conversation
- **Dark/Light theme**: Full theme support
