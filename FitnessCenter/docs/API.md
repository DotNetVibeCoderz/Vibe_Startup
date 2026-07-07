# API Documentation

## Base URL
```
https://localhost:5001/api/v1
```

## Authentication
API menggunakan cookie-based authentication (Identity). Untuk testing, login terlebih dahulu melalui browser di `/login`.

## Endpoints

### Members
```
GET /members — List semua member
GET /members/{id} — Detail member
```

### Memberships
```
GET /memberships — List semua paket membership
GET /memberships/{id} — Detail paket
```

### Trainers
```
GET /trainers — List semua trainer
GET /trainers/{id} — Detail trainer
```

### Classes
```
GET /classes — List semua kelas
GET /classes/{id} — Detail kelas
GET /classes/schedule — Jadwal kelas
```

### Attendance
```
GET /attendance/{userId} — Riwayat kehadiran user
POST /attendance/checkin/{userId} — Check-in user
```

### Payments
```
GET /payments — List semua pembayaran
GET /revenue — Ringkasan pendapatan
```

### Feedback
```
GET /feedback — List feedback
POST /feedback — Buat feedback baru
Body: { "userId": "...", "type": "General", "rating": 5, "comment": "..." }
```

### Events
```
GET /events — List event published
GET /events/{id} — Detail event
```

### Forum
```
GET /forum/posts — List post forum
GET /forum/posts/{id} — Detail post
```

### Gamification
```
GET /leaderboard — Top 20 leaderboard
GET /achievements/{userId} — Achievement user
```

### Notifications
```
GET /notifications/{userId} — Notifikasi user
```

### Chat
```
GET /chat/sessions/{userId} — Session chat user
POST /chat/send — Kirim pesan
Body: { "sessionId": 1, "message": "Halo Coach!" }
```

### Export
```
GET /export/members/csv — Export CSV
GET /export/members/excel — Export Excel
```
