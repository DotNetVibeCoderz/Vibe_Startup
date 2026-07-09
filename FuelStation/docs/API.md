# FuelStation REST API Documentation

## Base URL
```
https://localhost:5001/api
```

## Authentication
Currently using cookie-based authentication. API key authentication coming soon.

---

## Endpoints

### 1. Stations

#### GET /api/fuelapi/stations
Get all active fuel stations.

**Response:**
```json
[
  {
    "id": "guid",
    "name": "SPBU Mini - Cabang Utama",
    "code": "SPBU-001",
    "address": "Jl. Raya Utama No. 123",
    "phone": "021-5550101"
  }
]
```

### 2. Products

#### GET /api/fuelapi/products
Get all active fuel products with prices.

**Response:**
```json
[
  {
    "id": "guid",
    "name": "Pertalite",
    "code": "PLT",
    "pricePerLiter": 10000.00,
    "fuelType": "Gasoline"
  }
]
```

### 3. Tank Readings

#### GET /api/fuelapi/tanks/{stationId}
Get real-time tank readings for a station.

**Response:**
```json
[
  {
    "id": "guid",
    "name": "Tank PLT - SPBU-001",
    "tankNumber": "TNK-SPBU-001-PLT",
    "capacityLiters": 20000.00,
    "currentVolumeLiters": 15000.00,
    "minThresholdLiters": 2000.00,
    "fillPercentage": 75.0,
    "temperatureCelsius": 28.5,
    "pressureBar": 1.01,
    "isLeakDetected": false,
    "lastSensorReading": "2024-01-15T10:30:00Z",
    "productName": "Pertalite"
  }
]
```

#### POST /api/fuelapi/tanks/{tankId}/reading
Update tank sensor reading (for IoT integration).

**Request Body:**
```json
{
  "volumeLiters": 15000.00,
  "temperatureCelsius": 28.5,
  "pressureBar": 1.01,
  "isLeakDetected": false
}
```

### 4. Transactions

#### POST /api/fuelapi/transactions
Create a new transaction.

**Request Body:**
```json
{
  "fuelStationId": "guid",
  "fuelProductId": "guid",
  "liters": 20.00,
  "customerId": null,
  "paymentMethod": "Cash",
  "paymentReference": null
}
```

### 5. Summary

#### GET /api/fuelapi/summary/daily?date=2024-01-15
Get daily transaction summary.

### 6. Alerts

#### GET /api/fuelapi/alerts?unresolvedOnly=true
Get emergency alerts.

---

## Error Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request |
| 404 | Not Found |
| 500 | Internal Server Error |
