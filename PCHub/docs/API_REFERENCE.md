# PCHub - API Documentation

Base URL: `https://localhost:5001/api`  
Swagger UI: `https://localhost:5001/swagger`

---

## Authentication

### POST /api/auth/login
Login user dan dapatkan token.

**Request:**
```json
{
  "username": "admin",
  "password": "Admin123!"
}
```

**Response (200):**
```json
{
  "userId": "guid",
  "username": "admin",
  "email": "admin@pchub.com",
  "fullName": "Administrator",
  "role": "Admin",
  "token": "base64token..."
}
```

### POST /api/auth/register
Registrasi user baru.

**Request:**
```json
{
  "username": "member1",
  "email": "member1@email.com",
  "password": "Password123!",
  "fullName": "Member Satu",
  "phoneNumber": "081234567890"
}
```

---

## Dashboard

### GET /api/dashboard/stats
Mendapatkan statistik dashboard.

**Response:**
```json
{
  "totalUsers": 17,
  "activeUsers": 5,
  "totalPcs": 15,
  "availablePcs": 8,
  "todayRevenue": 450000,
  "monthRevenue": 12500000,
  "activeSessions": 3,
  "pendingReservations": 2,
  "popularGames": [ {"gameName": "Valorant", "playCount": 45, "totalMinutes": 5400} ],
  "revenueChart": [ {"date": "2025-01-01", "amount": 150000} ]
}
```

---

## PCs

### GET /api/pcs
List PC dengan paging, filter, sort.

**Query Params:** `page, pageSize, search, sortBy, sortDesc`

### POST /api/pcs
Tambah PC baru.

**Request:**
```json
{
  "name": "PC Gaming 16",
  "pcNumber": "PC-016",
  "specifications": "Ryzen 7 7800X3D, RTX 4070, 32GB DDR5",
  "hourlyRate": 12000
}
```

### PUT /api/pcs
Update PC.

**Request:**
```json
{
  "id": "guid",
  "name": "Updated Name",
  "pcNumber": "PC-016",
  "status": "Available",
  "specifications": "Updated specs",
  "hourlyRate": 15000,
  "isActive": true
}
```

---

## Games

### GET /api/games
List games dengan paging.

### POST /api/games
```json
{
  "name": "New Game",
  "genre": "FPS",
  "description": "Game description",
  "executablePath": "C:\\Games\\game.exe",
  "iconUrl": "https://example.com/icon.png",
  "version": "1.0.0"
}
```

---

## Billing

### POST /api/billing/start
Mulai sesi billing.

**Request:**
```json
{
  "userId": "guid",
  "pcId": "guid",
  "paymentMethod": "Cash"
}
```

### POST /api/billing/stop/{billingId}
Akhiri sesi billing.

### GET /api/billing/active/{userId}
Cek sesi aktif user.

**Response (200 / 204 No Content):**
```json
{
  "id": "guid",
  "userId": "guid",
  "username": "member1",
  "pcId": "guid",
  "pcName": "PC Gaming 1",
  "startTime": "2025-01-01T10:00:00Z",
  "endTime": null,
  "hourlyRate": 8000,
  "totalCost": 0,
  "status": "Active",
  "paymentMethod": "Cash",
  "paymentStatus": "Pending"
}
```

---

## Reservations

### GET /api/reservations
List semua reservasi.

### POST /api/reservations
Buat reservasi baru.

**Query:** `userId=guid`  
**Request:**
```json
{
  "pcId": "guid or null",
  "reservationDate": "2025-01-05T14:00:00Z",
  "durationMinutes": 120,
  "gameRequested": "Valorant",
  "notes": "Request headset tambahan"
}
```

---

## ChatBot

### POST /api/chat/send
Kirim pesan ke Koh Dedi.

**Request:**
```json
{
  "sessionId": "guid",
  "message": "Berapa harga sewa PC?",
  "imageUrl": null,
  "documentUrl": null
}
```

**Response:**
```json
{
  "sessionId": "guid",
  "role": "assistant",
  "content": "💰 Tarif PC Gaming mulai...",
  "timestamp": "2025-01-01T10:05:00Z"
}
```

---

## Memberships

### GET /api/memberships
List semua paket membership.

### POST /api/memberships/subscribe
Subscribe user ke membership.

**Query:** `userId=guid`  
**Request:**
```json
{
  "membershipId": "guid",
  "durationMonths": 3
}
```

---

## Promos

### GET /api/promos
List semua promo (paged).

### POST /api/promos/validate
Validasi kode promo.

**Query:** `code=DISKON50`

---

## Tournaments

### GET /api/tournaments
List turnamen.

### POST /api/tournaments/join
Join turnamen.

**Query:** `userId=guid`  
**Request:**
```json
{
  "tournamentId": "guid"
}
```

---

## Notifications

### GET /api/notifications/{userId}
List notifikasi user.

### PUT /api/notifications/{id}/read
Tandai notifikasi sudah dibaca.
