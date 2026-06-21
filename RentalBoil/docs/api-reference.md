# 📡 API Reference

## Authentication

Semua endpoint `/api/*` memerlukan API Key via header atau query string:

```
Header: X-Api-Key: rntl-2025-secure-api-key-change-in-production
Query:  ?api_key=rntl-2025-secure-api-key-change-in-production
```

**Swagger UI**: `https://localhost:5001/swagger` (tidak perlu auth)

---

## 🚗 Vehicles

### Get All Vehicles

```http
GET /api/vehicles?search=avanza&type=Car&brand=Toyota&minPrice=100000&maxPrice=500000&sortBy=price&sortDesc=true&page=1&pageSize=20
```

**Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `search` | string | Search by name, brand, model |
| `type` | enum | `Car` or `Motorcycle` |
| `brand` | string | Brand filter (case-insensitive) |
| `minCapacity` | int | Minimum passenger capacity |
| `transmission` | enum | `Automatic` or `Manual` |
| `minPrice` | decimal | Minimum price per day |
| `maxPrice` | decimal | Maximum price per day |
| `sortBy` | string | `popular`, `price`, `rating`, `name` |
| `sortDesc` | bool | Descending order |
| `page` | int | Page number (default 1) |
| `pageSize` | int | Items per page (default 20) |

**Response**:
```json
{
  "data": [
    {
      "id": 1,
      "name": "Toyota Avanza 2024",
      "plateNumber": "B 1234 AB",
      "type": "Car",
      "brand": "Toyota",
      "model": "Avanza",
      "year": 2024,
      "pricePerDay": 400000,
      "pricePerHour": 50000,
      "averageRating": 4.5,
      "location": "Jl. Sudirman No. 123"
    }
  ],
  "total": 8,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Get Vehicle by ID

```http
GET /api/vehicles/1
```

### Create Vehicle

```http
POST /api/vehicles
Content-Type: application/json

{
  "name": "Toyota Avanza 2024",
  "plateNumber": "B 9999 ZZ",
  "type": "Car",
  "brand": "Toyota",
  "model": "Avanza",
  "year": 2024,
  "pricePerDay": 400000,
  "pricePerHour": 50000,
  "ownerId": "user-guid-here"
}
```

### Update Vehicle

```http
PUT /api/vehicles/1
Content-Type: application/json

{ ... full vehicle object ... }
```

### Delete Vehicle

```http
DELETE /api/vehicles/1
```

### Update Vehicle Location (GPS)

```http
PATCH /api/vehicles/1/location
Content-Type: application/json

{
  "latitude": -6.2090,
  "longitude": 106.8460,
  "speed": 45,
  "heading": 90,
  "address": "Jl. Sudirman, Jakarta"
}
```

### Update Vehicle IoT Status

```http
PATCH /api/vehicles/1/iot
Content-Type: application/json

{
  "lockStatus": "Unlocked",
  "engineStatus": "On",
  "motionStatus": "Moving"
}
```

### Simulator Update (Full)

```http
POST /api/vehicles/1/simulator-update
Content-Type: application/json

{
  "latitude": -6.2090,
  "longitude": 106.8460,
  "speed": 45,
  "heading": 90,
  "lockStatus": "Unlocked",
  "engineStatus": "On",
  "motionStatus": "Moving",
  "address": "Jl. Sudirman"
}
```

### Batch Simulator Update

```http
POST /api/vehicles/batch/simulator-update
Content-Type: application/json

[
  {
    "vehicleId": 1,
    "latitude": -6.2095,
    "longitude": 106.8465,
    "speed": 60,
    "heading": 180
  },
  {
    "vehicleId": 2,
    "latitude": -6.1830,
    "longitude": 106.8235,
    "speed": 30,
    "heading": 270
  }
]
```

### Get Active Vehicles (for Simulator)

```http
GET /api/vehicles/active-for-simulator
```

### Get Vehicle Reviews

```http
GET /api/vehicles/1/reviews
```

---

## 📋 Bookings

### Get All Bookings

```http
GET /api/bookings?status=Active&page=1
```

### Get Booking by ID

```http
GET /api/bookings/1
```

### Create Booking

```http
POST /api/bookings
Content-Type: application/json

{
  "vehicleId": 1,
  "customerId": "user-guid",
  "startDate": "2025-04-01T00:00:00",
  "endDate": "2025-04-03T00:00:00",
  "durationDays": 2,
  "durationHours": 48,
  "couponCode": "WELCOME50"
}
```

### Booking Actions

```http
PUT /api/bookings/1/accept          # Partner accepts booking
PUT /api/bookings/1/reject          # Partner rejects booking
PUT /api/bookings/1/activate        # Start rental (pickup)
PUT /api/bookings/1/complete        # End rental (return)
PUT /api/bookings/1/cancel          # Cancel booking
```

### Get Bookings by User

```http
GET /api/bookings/customer/{customerId}
GET /api/bookings/partner/{partnerId}
```

### Get Statistics

```http
GET /api/bookings/stats/admin
GET /api/bookings/stats/partner/{partnerId}
```

---

## 👤 Users

```http
GET    /api/users                    # List users
GET    /api/users/{id}               # Get user
PUT    /api/users/{id}/suspend       # Toggle suspend
```

---

## ⭐ Reviews

```http
POST   /api/reviews                  # Create review
GET    /api/reviews/vehicle/{id}     # Get reviews by vehicle
```

---

## 💰 Payments

```http
POST   /api/payments                 # Create payment
POST   /api/payments/{bookingId}/confirm   # Confirm payment
```

---

## 🎁 Promotions

```http
GET    /api/promotions               # List active promos
GET    /api/promotions/validate/{code}?tier=Gold   # Validate coupon
```

---

## 🔔 Notifications

```http
GET    /api/notifications/{userId}?unreadOnly=true   # List notifications
GET    /api/notifications/{userId}/count              # Unread count
PUT    /api/notifications/{id}/read                   # Mark as read
```

---

## 🛰️ GPS / IoT

```http
GET    /api/gps/vehicle/{id}                          # Get GPS status
POST   /api/gps/vehicle/{id}/lock/toggle              # Toggle lock
POST   /api/gps/vehicle/{id}/engine/toggle            # Toggle engine
POST   /api/gps/vehicle/{id}/track/start              # Start tracking
POST   /api/gps/vehicle/{id}/track/stop               # Stop tracking
```

---

## 🤖 Chat Bot

```http
POST /api/chat/send
Content-Type: application/json

{
  "message": "Cari mobil Avanza",
  "sessionId": 0,
  "imageUrl": null,
  "documentUrl": null
}
```

---

## 📊 Dashboard

```http
GET /api/dashboard/admin
```

---

## ⚠️ Error Responses

### 401 Unauthorized
```json
{
  "error": "Unauthorized",
  "message": "Invalid or missing API Key"
}
```

### 404 Not Found
```json
{
  "error": "Vehicle not found"
}
```

### 400 Bad Request
```json
{
  "error": "Vehicle not available"
}
```
