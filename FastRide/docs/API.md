# 📘 API Reference — FastRide

> Complete REST API endpoint documentation for the FastRide ride-hailing platform.

---

## 🌐 Base URL

```
Development: https://localhost:5001
Production:  https://api.fastride.com
```

All endpoints are prefixed with `/api/`.

---

## 📋 Endpoint Index

| # | Group | Endpoints | Description |
|---|-------|-----------|-------------|
| 1 | [Health](#1-health) | `GET /api/health` | Service health check |
| 2 | [Auth](#2-auth) | `POST /api/auth/register`, `POST /api/auth/login` | User registration & login |
| 3 | [Riders](#3-riders) | `GET /api/riders` | List all riders |
| 4 | [Drivers](#4-drivers) | `GET /api/drivers` | List all drivers with profiles |
| 5 | [Orders](#5-orders) | `GET /api/orders`, `POST /api/orders` | Order CRUD operations |
| 6 | [Payments](#6-payments) | `POST /api/payments` | Payment processing |
| 7 | [Dashboard](#7-dashboard) | `GET /api/dashboard/stats` | Real-time analytics |

---

## 1. Health

### `GET /api/health`

Check if the API service is running and healthy.

**Response** `200 OK`
```json
{
  "status": "healthy",
  "timestamp": "2025-06-15T10:30:00Z"
}
```

---

## 2. Auth

### `POST /api/auth/register`

Register a new user (rider or driver).

**Request Body**
```json
{
  "fullName": "Budi Santoso",
  "email": "budi@email.com",
  "phoneNumber": "0812-3456-7890",
  "password": "SecurePass123!",
  "role": "Rider"
}
```

**Response** `200 OK`
```json
{
  "message": "Registration endpoint ready"
}
```

### `POST /api/auth/login`

Authenticate user and receive JWT token.

**Request Body**
```json
{
  "email": "budi@email.com",
  "password": "SecurePass123!"
}
```

**Response** `200 OK`
```json
{
  "message": "Login endpoint ready"
}
```

---

## 3. Riders

### `GET /api/riders`

List all registered riders.

**Response** `200 OK`
```json
[
  {
    "id": "a1b2c3d4-...",
    "fullName": "Budi Santoso",
    "email": "budi@email.com",
    "phoneNumber": "0812-3456-7890",
    "isVerified": true,
    "createdAt": "2025-01-15T08:00:00Z"
  }
]
```

**Query Parameters** (planned):
| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `limit` | int | 20 | Items per page |
| `search` | string | - | Search by name or email |
| `sortBy` | string | `createdAt` | Sort field |
| `sortOrder` | string | `desc` | Sort direction |

---

## 4. Drivers

### `GET /api/drivers`

List all registered drivers with their profiles.

**Response** `200 OK`
```json
[
  {
    "id": "b2c3d4e5-...",
    "fullName": "Andi Santoso",
    "email": "andi@drive.com",
    "status": "Online",
    "rating": 4.9,
    "totalTrips": 1234,
    "totalEarnings": 45200000,
    "vehicleType": "Toyota Avanza",
    "vehiclePlate": "B 1234 XYZ"
  }
]
```

---

## 5. Orders

### `GET /api/orders`

List the 50 most recent orders.

**Response** `200 OK`
```json
[
  {
    "id": "c3d4e5f6-...",
    "riderName": "Budi Santoso",
    "driverName": "Andi Santoso",
    "pickupAddress": "Jl. Sudirman No. 123",
    "dropoffAddress": "Jl. Thamrin No. 456",
    "distanceKm": 5.2,
    "estimatedFare": 25000,
    "finalFare": 25000,
    "vehicleCategory": "Economy",
    "status": "Completed",
    "createdAt": "2025-06-15T09:30:00Z"
  }
]
```

### `POST /api/orders`

Create a new ride order.

**Request Body**
```json
{
  "pickupLatitude": -6.2088,
  "pickupLongitude": 106.8456,
  "pickupAddress": "Mall Grand Indonesia",
  "dropoffLatitude": -6.1275,
  "dropoffLongitude": 106.6535,
  "dropoffAddress": "Bandara Soekarno-Hatta",
  "vehicleCategory": "Comfort",
  "paymentMethod": "EWallet",
  "promoCode": "WELCOME50"
}
```

**Response** `200 OK`
```json
{
  "message": "Create order endpoint ready"
}
```

---

## 6. Payments

### `POST /api/payments`

Process a payment for an order.

**Request Body**
```json
{
  "orderId": "c3d4e5f6-...",
  "method": "EWallet",
  "amount": 50000
}
```

**Response** `200 OK`
```json
{
  "message": "Payment endpoint ready"
}
```

---

## 7. Dashboard

### `GET /api/dashboard/stats`

Get real-time dashboard statistics.

**Response** `200 OK`
```json
{
  "totalOrdersToday": 156,
  "activeDrivers": 42,
  "totalRevenueToday": 4250000,
  "averageRating": 4.7,
  "timestamp": "2025-06-15T10:30:00Z"
}
```

---

## 🔒 Authentication

All endpoints (except `/api/health`) will require JWT Bearer authentication in production:

```
Authorization: Bearer <token>
```

---

## ⚠️ Error Responses

### Standard Error Format

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation error details",
  "instance": "/api/orders",
  "errors": {
    "pickupAddress": ["Pickup address is required"]
  }
}
```

### HTTP Status Codes

| Code | Meaning |
|------|---------|
| `200` | Success |
| `201` | Created |
| `400` | Bad Request — validation error |
| `401` | Unauthorized — missing/invalid token |
| `403` | Forbidden — insufficient permissions |
| `404` | Not Found — resource doesn't exist |
| `409` | Conflict — duplicate resource |
| `500` | Internal Server Error |

---

## 📈 Planned Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/orders/{id}` | Get order details |
| `PUT` | `/api/orders/{id}/status` | Update order status |
| `GET` | `/api/orders/{id}/tracking` | Real-time order tracking |
| `POST` | `/api/orders/{id}/cancel` | Cancel an order |
| `GET` | `/api/promos` | List active promos |
| `POST` | `/api/promos/validate` | Validate promo code |
| `GET` | `/api/users/{id}/profile` | Get user profile |
| `PUT` | `/api/users/{id}/profile` | Update user profile |
| `POST` | `/api/reviews` | Submit review |
| `GET` | `/api/reviews/{id}` | Get reviews for user |
| `GET` | `/api/dashboard/orders-by-hour` | Hourly order breakdown |
| `GET` | `/api/dashboard/orders-by-status` | Orders grouped by status |
| `GET` | `/api/dashboard/revenue-trend` | Revenue trend over time |
