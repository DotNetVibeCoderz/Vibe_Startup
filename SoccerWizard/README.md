# ⚽ SoccerWizard - Football Match Prediction Platform

> AI-Powered Football Match Prediction using ML.NET, Poisson Distribution & Large Language Models

[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![ML.NET](https://img.shields.io/badge/ML.NET-4.0-blue)](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet)
[![Blazor](https://img.shields.io/badge/Blazor-Server-purple)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## 📖 Table of Contents

- [Overview](#-overview)
- [Features](#-features)
- [Architecture](#-architecture)
- [Tech Stack](#-tech-stack)
- [Getting Started](#-getting-started)
- [Demo Accounts](#-demo-accounts)
- [Project Structure](#-project-structure)
- [ML Pipeline](#-ml-pipeline)
- [LLM Integration](#-llm-integration)
- [API Integration](#-api-integration)
- [Screenshots](#-screenshots)
- [Contributing](#-contributing)
- [License](#-license)

---

## 🎯 Overview

**SoccerWizard** is a comprehensive football match prediction web application built with .NET Blazor Server. It combines **ML.NET** machine learning models, **Poisson distribution** statistical analysis, and **Large Language Models (LLMs)** to provide accurate, data-driven match predictions.

The platform features real-time updates via **SignalR**, rich data visualizations, AI-powered chat assistant, and sentiment analysis of football news.

---

## ✨ Features

### 📊 Data & Statistics
- **Live Match Data**: Real-time scores, schedules, match results
- **Historical Statistics**: Head-to-head records, home/away performance, goals per match
- **Poisson Distribution**: Score distribution and win probability calculations
- **Dashboard Visuals**: Performance trend charts, player heatmaps, prediction charts
- **Team Analysis**: ELO ratings, attack/defense/midfield strength metrics

### 🤖 Machine Learning (ML.NET)
- **Binary Classification**: Win/Draw/Loss prediction using Fast Forest algorithm
- **Regression Model**: Score prediction based on multiple input features
- **Feature Engineering**: ELO ratings, team momentum, expected goals (xG)
- **Model Evaluation**: Accuracy, Precision, Recall, F1 Score, AUC-ROC metrics
- **19 Features**: ELO, attack/defense strength, momentum, H2H, xG, weather data

### 🧠 LLM Integration
- **Multi-Provider Support**: OpenAI, Gemini, Anthropic, Ollama
- **Sentiment Analysis**: AI-powered news sentiment extraction
- **Text Predictions**: LLM-generated match analysis
- **Interactive Chat**: Conversational AI for football questions
- **Semantic Kernel**: Ready for kernel functions and plugins

### 🛠️ .NET Technology
- **Blazor Server**: Interactive UI with real-time components
- **SignalR**: Live score and prediction updates
- **Entity Framework Core**: Database management with SQLite
- **ASP.NET Core Identity**: User authentication & authorization
- **Cross-Platform**: Windows, Linux, cloud (Azure/AWS)

### 🎨 UI/UX
- **Dark Theme**: Professional football-inspired design
- **Responsive Layout**: Works on desktop and mobile
- **Interactive Components**: Live indicators, stat bars, probability charts
- **Bootstrap Icons**: Clean iconography throughout

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER                          │
│  ┌───────────┐  ┌──────────┐  ┌──────────┐  ┌────────────────┐ │
│  │ Dashboard │  │ Matches  │  │Predictions│  │   AI Chat      │ │
│  └───────────┘  └──────────┘  └──────────┘  └────────────────┘ │
│                         Blazor Server + SignalR                  │
├─────────────────────────────────────────────────────────────────┤
│                       SERVICE LAYER                              │
│  ┌────────────────┐  ┌──────────────┐  ┌──────────────────────┐ │
│  │  MatchService  │  │ MLPrediction │  │     LLMService       │ │
│  │                │  │   Service    │  │ (OpenAI/Gemini/      │ │
│  │  - CRUD Match  │  │              │  │  Anthropic/Ollama)   │ │
│  │  - Team Stats  │  │ - Classifier │  │                      │ │
│  │  - News Data   │  │ - Regressor  │  │ - Sentiment Analysis │ │
│  │                │  │ - Poisson    │  │ - Chat AI            │ │
│  └────────────────┘  └──────────────┘  └──────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                        DATA LAYER                                │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │              AppDbContext (EF Core + Identity)               │ │
│  │  Teams | Players | Matches | Leagues | Predictions | News   │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                         SQLite Database                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔧 Tech Stack

| Technology | Purpose |
|-----------|---------|
| **.NET 10** | Application framework |
| **Blazor Server** | Interactive web UI |
| **ML.NET 4.0** | Machine learning pipeline |
| **Entity Framework Core** | ORM / Database |
| **ASP.NET Core Identity** | Authentication |
| **SignalR** | Real-time updates |
| **SQLite** | Database |
| **Bootstrap Icons** | UI icons |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Any modern browser

### Installation

```bash
# Clone repository
git clone https://github.com/your-org/SoccerWizard.git
cd SoccerWizard

# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run
```

The application will be available at `https://localhost:5001`.

### Configuration

Edit `appsettings.json` to configure:

```json
{
  "LLM": {
    "DefaultProvider": "Ollama",
    "OpenAI": { "ApiKey": "sk-..." },
    "Gemini": { "ApiKey": "..." },
    "Anthropic": { "ApiKey": "..." },
    "Ollama": { "Endpoint": "http://localhost:11434" }
  }
}
```

---

## 👤 Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| **Admin** | admin@soccerwizard.com | Admin123! |
| **User** | demo@soccerwizard.com | Demo123! |
| User | john.doe@soccerwizard.com | User123! |
| User | jane.smith@soccerwizard.com | User123! |

---

## 📁 Project Structure

```
SoccerWizard/
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor          # Main layout with sidebar
│   ├── Pages/
│   │   ├── Auth/
│   │   │   ├── Login.razor           # User login
│   │   │   ├── Register.razor        # User registration
│   │   │   └── Logout.razor          # Logout handler
│   │   ├── Admin/
│   │   │   └── AdminPanel.razor      # Admin dashboard
│   │   ├── Home.razor                # Main dashboard
│   │   ├── Matches.razor             # Match list & detail
│   │   ├── Predict.razor             # Prediction engine
│   │   ├── Teams.razor               # Team list & detail
│   │   ├── News.razor                # News & sentiment
│   │   ├── Chat.razor                # AI Chat assistant
│   │   ├── MLDashboard.razor         # ML model management
│   │   └── Profile.razor             # User profile
│   ├── App.razor                     # App root
│   ├── Routes.razor                  # Route configuration
│   └── _Imports.razor                # Global imports
├── Data/
│   ├── AppDbContext.cs               # EF Core context
│   └── DataSeeder.cs                 # Sample data seeder
├── Hubs/
│   └── MatchHub.cs                   # SignalR hub
├── Models/
│   ├── Team.cs                       # Team model
│   ├── Player.cs                     # Player model
│   ├── Match.cs                      # Match model
│   ├── League.cs                     # League model
│   ├── Prediction.cs                 # Prediction model
│   ├── NewsArticle.cs                # News article model
│   ├── HeadToHead.cs                 # H2H model
│   ├── UserProfile.cs                # User profile model
│   ├── MatchData.cs                  # ML training data
│   └── MatchPrediction.cs            # ML prediction result
├── Services/
│   ├── MatchService.cs               # Match data service
│   ├── MLPredictionService.cs        # ML pipeline service
│   └── LLMService.cs                 # LLM integration service
├── wwwroot/
│   ├── css/soccerwizard.css          # Main stylesheet
│   └── js/soccerwizard.js            # Client-side JS
├── docs/                             # Documentation
├── Program.cs                        # Application entry
├── appsettings.json                  # Configuration
├── SoccerWizard.csproj               # Project file
├── PLAN.md                           # Development plan
├── README.md                         # English README
└── README.id.md                      # Indonesian README
```

---

## 🧠 ML Pipeline

### Training Data Features (19 total)

| Feature | Description |
|---------|-------------|
| HomeElo / AwayElo | ELO rating (1500-1900) |
| HomeAttackStrength / AwayAttackStrength | Attack capability (0-3) |
| HomeDefenseStrength / AwayDefenseStrength | Defense capability (0-3) |
| HomeMomentum / AwayMomentum | Recent form (0-1) |
| HomeAvgGoals / AwayAvgGoals | Goals per match |
| HomeAvgConceded / AwayAvgConceded | Goals conceded per match |
| HomeWinRate / AwayWinRate | Win percentage |
| H2HHomeWins / H2HAwayWins / H2HDraws | Head-to-head record |
| HomeXG / AwayXG | Expected goals |
| Temperature / Humidity | Weather conditions |

### Classifier Model
- **Algorithm**: Fast Forest (Random Forest variant)
- **Type**: Binary Classification
- **Output**: Home Win probability, Score, Confidence

### Score Regressor
- **Algorithm**: Fast Forest Regression
- **Output**: Predicted home/away goals

### Poisson Distribution
- **Purpose**: Score distribution estimation
- **Input**: Expected goals (λ) from team strengths
- **Output**: Score probabilities, win/draw/lose distribution
- **Integration**: Hybrid with ML.NET (60/40 weight)

---

## 🤖 LLM Integration

SoccerWizard supports multiple LLM providers through a unified interface:

```csharp
// Sentiment Analysis
var (score, label, summary) = await llmService.AnalyzeSentimentAsync(newsText);

// Match Prediction
var prediction = await llmService.GenerateTextPredictionAsync(match, homeTeam, awayTeam);

// Interactive Chat
var response = await llmService.ChatAsync("Who will win the Premier League?");
```

### Supported Providers
- **OpenAI** (GPT-4o-mini)
- **Google Gemini** (Gemini 2.0 Flash)
- **Anthropic** (Claude 3.5 Sonnet)
- **Ollama** (Llama 3.2, local)

---

## 📄 License

MIT License - Feel free to use, modify, and distribute.

---

## 🙏 Acknowledgements

- [ML.NET](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet)
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
- [Bootstrap Icons](https://icons.getbootstrap.com/)
- GraviCode Studios

---

**Made with ❤️ by Jacky the Code Bender @ GraviCode Studios**

*Kalau suka project ini, traktir pulsa dong! Kirim ke https://studios.gravicode.com/products/budax* ☕
