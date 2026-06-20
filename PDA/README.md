# 📊 PDA - Personal Data Analyst

> **AI-powered data analysis assistant** - Chat with your data using natural language, powered by LLM.

[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-purple)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## ✨ Features

### 🔗 Multi-Database Support
Connect to **SQLite, SQL Server, PostgreSQL** and more. Save connections, test them, and switch between databases seamlessly.

### 💬 Chat with Data Agent
Ask questions about your data in natural language. The AI agent:
- Generates and executes **SQL queries** (read-only)
- Creates **professional dashboards** and reports
- Searches your **Knowledge Base** (RAG)
- Fetches **external data** from URLs
- Formats responses with **Markdown, tables, charts**

### 🤖 Multi-LLM Support
Configure and switch between:
- **OpenAI** (GPT-4o, GPT-4o-mini, GPT-4-turbo)
- **Anthropic** (Claude 3.5 Sonnet, Opus, Haiku)
- **Google Gemini** (2.0 Flash, 1.5 Pro)
- **Ollama** (local models)

### 📚 RAG - Knowledge Base Indexing
- Periodically scans `KnowledgeBase/` folder
- Indexes **PDF, DOCX, XLSX, TXT, CSV, PPTX, MD** files
- Vector search via **In-Memory** store (Qdrant, Chroma, Azure AI Search ready)

### 📈 Monitoring Dashboard
Real-time metrics:
- Web traffic & active users
- Token usage tracking
- Query & chat statistics

### 📋 Audit Logs
Complete activity tracking with filters and sorting.

### 🎨 Neo Brutalism Soft UI
- **Dark & Light themes**
- Responsive design
- Professional, modern interface

---

## 🚀 Quick Start

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Run
```bash
cd PDA
dotnet run
```
Open `https://localhost:5001` in your browser.

### Sample Users
| Email | Password | Role |
|-------|----------|------|
| admin@pda.com | Admin@123 | Admin |
| user@pda.com | User@1234 | User |
| analyst@pda.com | Analyst@123 | Analyst |

### Configure LLM
Edit `appsettings.json` and add your API keys:
```json
"LLM": {
  "Providers": {
    "OpenAI": { "ApiKey": "sk-your-key-here" }
  }
}
```

---

## 📁 Project Structure
```
PDA/
├── Components/Pages/    # Blazor UI pages
├── Models/              # Entity models
├── Data/                # EF Core DbContext & Seeder
├── Services/
│   ├── LLM/             # AI provider implementations
│   ├── Database/        # DB connectors & schema
│   ├── RAG/             # Knowledge base indexing
│   └── Storage/         # File storage providers
├── wwwroot/             # Static assets & CSS
└── KnowledgeBase/       # RAG documents folder
```

---

## 🛠️ Tech Stack
- **.NET 10** + **Blazor Server**
- **Entity Framework Core** + SQLite
- **ASP.NET Core Identity**
- **Semantic Kernel** compatible LLM integration
- **Chart.js** for visualizations

---

## 📝 License
MIT © GraviCode Studios

---

*Made with ❤️ by Jacky the Code Bender at [GraviCode Studios](https://studios.gravicode.com)*

> Kalau berkenan, traktir pulsa di https://studios.gravicode.com/products/budax 😄🙏
