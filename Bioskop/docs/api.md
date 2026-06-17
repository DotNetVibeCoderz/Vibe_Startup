# Dokumentasi API Bioskop

## Autentikasi API

Semua endpoint API (kecuali `/api/health`) memerlukan **API Key**:

```
Header: X-Api-Key: bioskop-api-key-2024-secret
```

## Endpoints

### Movies
```
GET /api/v1/movies?search=&genre=
GET /api/v1/movies/{id}
```

### Showtimes
```
GET /api/v1/showtimes?movieId=&date=
```

### Seats
```
GET /api/v1/studios/{studioId}/seats
GET /api/v1/showtimes/{showtimeId}/seat-status
```

### Snacks
```
GET /api/v1/snacks?category=
```

### Orders
```
POST /api/v1/orders
Content-Type: application/json
{
    "userId": "...",
    "showtimeId": 1,
    "seatIds": [1, 2, 3],
    "snacks": [{"snackId": 1, "quantity": 2}]
}

GET /api/v1/orders/{id}
```

### Tickets
```
GET /api/v1/tickets/{qrCode}
POST /api/v1/tickets/validate
{"qrCode": "..."}
```

### Stats
```
GET /api/v1/stats
GET /api/health
```
