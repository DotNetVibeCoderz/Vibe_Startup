# 📡 SoccerWizard REST API Documentation

## Authentication

Semua endpoint `/api/*` memerlukan **ApiKey**. Kirim via:

```bash
# Header
curl -H "X-Api-Key: sw-dev-key-2024" https://localhost:5001/api/matches

# Query Parameter
curl "https://localhost:5001/api/matches?api_key=sw-dev-key-2024"
```

**Default Development API Key:** `sw-dev-key-2024`

Keys dikonfigurasi di `appsettings.json` → `ApiKeys[]`.

---

## Swagger UI

Buka browser: **https://localhost:5001/swagger**

---

## Endpoints

### Matches

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/matches` | Semua pertandingan (pagination + filter) |
| `GET` | `/api/matches?status=LIVE` | Filter by status |
| `GET` | `/api/matches?leagueId=1` | Filter by league |
| `GET` | `/api/matches?teamId=5` | Filter by team |
| `GET` | `/api/matches/live` | Live matches only |
| `GET` | `/api/matches/upcoming` | Upcoming matches |
| `GET` | `/api/matches/{id}` | Detail match + predictions |
| `GET` | `/api/matches/export/csv` | Export finished matches as CSV |

```bash
curl -H "X-Api-Key: sw-dev-key-2024" \
  "http://localhost:5024/api/matches?status=LIVE&page=1&pageSize=10"
```

### Teams

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/teams` | All teams |
| `GET` | `/api/teams?leagueId=1` | Filter by league |
| `GET` | `/api/teams/{id}` | Team detail + players |
| `GET` | `/api/teams/top?count=10` | Top teams by ELO |

### Players

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/players` | All players (pagination) |
| `GET` | `/api/players?teamId=1` | Players by team |
| `GET` | `/api/players?position=FWD` | Filter by position |
| `GET` | `/api/players/top-scorers` | Top 10 goal scorers |

### Predictions

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/predictions` | All predictions (pagination) |
| `GET` | `/api/predictions/{id}` | Prediction detail |
| `GET` | `/api/predictions/accuracy` | Accuracy metrics |

### Head-to-Head

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/headtohead` | All H2H records |
| `GET` | `/api/headtohead?team1Id=10&team2Id=11` | H2H between 2 teams |

### News

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/news` | All news (pagination) |
| `GET` | `/api/news?sentiment=POSITIVE` | Filter by sentiment |

### Stats

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/stats/dashboard` | Full dashboard stats |
| `GET` | `/api/stats/elo-ranking` | ELO ranking all teams |

### Search

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/search?q=Argentina` | Search teams, players, matches |

---

## Response Format

```json
{
  "success": true,
  "data": {
    "items": [...],
    "page": 1,
    "pageSize": 20,
    "totalCount": 45,
    "totalPages": 3,
    "hasNextPage": true,
    "hasPrevPage": false
  },
  "error": null,
  "totalCount": 45
}
```

## Error Response

```json
{
  "success": false,
  "data": null,
  "error": "Match not found",
  "totalCount": 0
}
```
