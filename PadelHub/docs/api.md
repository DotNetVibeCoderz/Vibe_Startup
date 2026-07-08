# PadelHub API Reference

## REST API Endpoints

Base URL: `https://localhost:5001/api`

### Health Check
```
GET /api/health
Response: { "Status": "Healthy", "Timestamp": "2024-...", "AppName": "PadelHub API" }
```

### Players
```
GET /api/players          - List all players (max 100)
GET /api/players/{id}     - Get player by ID
```

### Clubs
```
GET /api/clubs            - List all clubs (max 50)
GET /api/clubs/{id}       - Get club with courts
```

### Courts
```
GET /api/courts           - List all courts with club info
```

### Tournaments
```
GET /api/tournaments      - List tournaments (max 50)
```

### IoT Sensors
```
POST /api/sensors/push    - Push sensor data
Body: { "CourtId": 1, "SensorType": "Temperature", "Value": "{json}" }

GET /api/sensors/{courtId} - Get sensor data for court
```

### Rankings
```
GET /api/rankings         - Get player rankings (top 50)
```

## Swagger UI

Available at: `https://localhost:5001/swagger` (Development mode)

## Authentication

Future versions will include JWT authentication for API access.
Currently, the API is open for development purposes.
