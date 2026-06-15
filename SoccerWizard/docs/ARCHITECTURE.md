# SoccerWizard Documentation

## 📚 Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Setup Guide](#setup-guide)
3. [Database Schema](#database-schema)
4. [ML Pipeline](#ml-pipeline)
5. [LLM Integration](#llm-integration)
6. [Authentication](#authentication)
7. [SignalR Real-Time](#signalr-real-time)
8. [Deployment](#deployment)

---

## Architecture Overview

SoccerWizard follows a layered architecture:

- **Presentation Layer**: Blazor Server components with SignalR
- **Service Layer**: MatchService, MLPredictionService, LLMService
- **Data Layer**: EF Core with SQLite (configurable to SQL Server, MySQL, PostgreSQL)

### Design Patterns
- Repository pattern via EF Core DbContext
- Factory pattern with IDbContextFactory for thread safety
- Strategy pattern for LLM provider selection
- Singleton/Scoped service lifetimes

---

## Setup Guide

### Prerequisites
- .NET 10 SDK
- Optional: Ollama (for local LLM), API keys for cloud LLMs

### Step 1: Clone & Restore
```bash
git clone <repo-url>
cd SoccerWizard
dotnet restore
```

### Step 2: Configure
Edit `appsettings.json` for database and LLM settings.

### Step 3: Run
```bash
dotnet run
```

### Step 4: Seed Data
Database is automatically seeded on first run with:
- 7 user accounts (1 admin, 6 users)
- 4 leagues (Premier League, La Liga, Serie A, Bundesliga)
- 20 teams with ELO ratings and strength metrics
- 21+ players with stats
- 50+ matches (finished, live, scheduled)
- Head-to-head records
- News articles with sentiment analysis
- Prediction results with accuracy tracking

---

## Database Schema

### Tables
- **Leagues**: Competition/league data
- **Teams**: Team info, ratings, performance stats
- **Players**: Player profiles, stats, injury status
- **Matches**: Fixtures with scores and stats
- **MatchStatistics**: Detailed per-match stats
- **Predictions**: ML/LLM prediction results
- **HeadToHeads**: Historical H2H records
- **NewsArticles**: News with sentiment scores
- **UserProfiles**: Extended user data

### Identity Tables
- AspNetUsers, AspNetRoles, AspNetUserRoles, etc.

---

## ML Pipeline

### Data Flow
1. **Data Collection**: Historical matches from database
2. **Feature Engineering**: 19 features computed from raw data
3. **Training**: Fast Forest algorithm with 80/20 split
4. **Evaluation**: Accuracy, Precision, Recall, F1, AUC metrics
5. **Prediction**: Combined ML.NET + Poisson hybrid approach

### Training Code
```csharp
var pipeline = mlContext.Transforms.Concatenate("Features", ...)
    .Append(mlContext.Transforms.NormalizeMinMax("Features"))
    .Append(mlContext.BinaryClassification.Trainers.FastForest());

var model = pipeline.Fit(trainingData);
```

### Prediction
- ML.NET model provides base probabilities (60% weight)
- Poisson distribution provides statistical probabilities (40% weight)
- Final probabilities are normalized and weighted

---

## LLM Integration

### Provider Configuration
Set `LLM:DefaultProvider` to one of: OpenAI, Gemini, Anthropic, Ollama

### API Endpoints Used
- OpenAI: `https://api.openai.com/v1/chat/completions`
- Gemini: `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent`
- Anthropic: `https://api.anthropic.com/v1/messages`
- Ollama: `http://localhost:11434/api/generate`

### Fallback Mechanism
If no LLM API key is configured, skilled fallback responses are generated based on statistical data.

---

## Authentication

### Identity Configuration
- ASP.NET Core Identity with EF Core stores
- Password requirements: min 6 chars, digit, lowercase, uppercase, non-alphanumeric
- Cookie authentication with 7-day sliding expiration
- Roles: Admin, User

### Protected Routes
- `/admin` - Admin role required
- `/profile` - Authenticated users only
- `/auth/login`, `/auth/register` - Public

---

## SignalR Real-Time

### Hub: MatchHub (`/matchhub`)
- **JoinMatchGroup**: Subscribe to match-specific updates
- **UpdateScore**: Broadcast score changes to match group
- **NewPrediction**: Broadcast new predictions
- **LiveMatchNotification**: Global notifications

### Client Integration
```javascript
connection.on("ScoreUpdated", (data) => { /* update UI */ });
connection.on("LiveNotification", (data) => { /* show toast */ });
```

---

## Deployment

### Windows
```bash
dotnet publish -c Release -o ./publish
```

### Linux
```bash
dotnet publish -c Release -r linux-x64 -o ./publish
```

### Docker
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "SoccerWizard.dll"]
```

### Azure
```bash
az webapp up --name soccerwizard --runtime "DOTNET:10.0"
```
