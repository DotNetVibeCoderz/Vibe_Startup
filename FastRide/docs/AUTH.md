# 🔐 Authentication & Authorization — FastRide

> Complete authentication and authorization guide for the FastRide platform.

---

## 📋 Overview

FastRide uses **JWT (JSON Web Token)** Bearer authentication with the following flow:

```
┌─────────┐     ┌──────────┐     ┌────────────┐
│  Client  │────▶│  /login  │────▶│  JWT Token │
│  (App)   │     │  Endpoint│     │  Response  │
└─────────┘     └──────────┘     └────────────┘
     │                                  │
     │    ┌─────────────────────────────┘
     │    │  Authorization: Bearer <token>
     ▼    ▼
┌──────────────────────────────┐
│       Protected API          │
│  Validate Token → Allow/Deny │
└──────────────────────────────┘
```

---

## 👤 User Roles

| Role | Value | Description |
|------|-------|-------------|
| `Rider` | 1 | Regular user who books rides |
| `Driver` | 2 | Driver who accepts and fulfills orders |
| `Admin` | 3 | Platform administrator with full access |

---

## 🔑 Registration

### Endpoint

```
POST /api/auth/register
```

### Request

```json
{
  "fullName": "Budi Santoso",
  "email": "budi@email.com",
  "phoneNumber": "0812-3456-7890",
  "password": "SecurePass123!",
  "role": "Rider"
}
```

### Validation Rules

| Field | Rules |
|-------|-------|
| `fullName` | Required, 3-200 characters |
| `email` | Required, valid email format, unique |
| `phoneNumber` | Required, valid Indonesian phone format |
| `password` | Required, min 8 characters, 1 uppercase, 1 number |
| `role` | Optional, defaults to `Rider` |

---

## 🔓 Login

### Endpoint

```
POST /api/auth/login
```

### Request

```json
{
  "email": "budi@email.com",
  "password": "SecurePass123!"
}
```

### Response (planned)

```json
{
  "userId": "a1b2c3d4-...",
  "fullName": "Budi Santoso",
  "email": "budi@email.com",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "role": "Rider",
  "expiresAt": "2025-06-16T10:30:00Z"
}
```

### Token Structure

```json
{
  "sub": "a1b2c3d4-...",
  "name": "Budi Santoso",
  "email": "budi@email.com",
  "role": "Rider",
  "iat": 1718436600,
  "exp": 1718523000,
  "iss": "FastRide",
  "aud": "FastRide"
}
```

---

## 🔄 Password Reset Flow

### 1. Request Reset Code

```
POST /api/auth/forgot-password
```

```json
{
  "email": "budi@email.com"
}
```

### 2. Reset with Code

```
POST /api/auth/reset-password
```

```json
{
  "email": "budi@email.com",
  "resetCode": "ABC123",
  "newPassword": "NewSecurePass456!"
}
```

---

## 🛡️ Authorization Rules

### Rider Permissions
- ✅ Book rides
- ✅ View own orders
- ✅ View own profile
- ✅ Submit reviews
- ✅ Apply promo codes
- ❌ Access admin dashboard
- ❌ View other users' data

### Driver Permissions
- ✅ View available orders
- ✅ Accept/decline orders
- ✅ Update order status
- ✅ View own earnings
- ✅ View own profile
- ❌ Book rides
- ❌ Access admin dashboard

### Admin Permissions
- ✅ Full access to all resources
- ✅ User management
- ✅ Order management
- ✅ Driver verification
- ✅ Promo management
- ✅ View analytics & reports

---

## 🔧 Configuration

### JWT Settings (`appsettings.json`)

```json
{
  "Jwt": {
    "Secret": "your-256-bit-secret-key-here-min-32-chars!",
    "Issuer": "FastRide",
    "Audience": "FastRide",
    "AccessTokenExpirationMinutes": 1440,
    "RefreshTokenExpirationDays": 30
  }
}
```

---

## 🚧 Implementation Status

| Feature | Status | Priority |
|---------|--------|----------|
| Registration endpoint | 🟡 Scaffold | High |
| Login endpoint | 🟡 Scaffold | High |
| JWT token generation | ⚪ Planned | High |
| Password hashing (BCrypt) | ⚪ Planned | High |
| Role-based authorization | ⚪ Planned | Medium |
| Refresh tokens | ⚪ Planned | Medium |
| Password reset flow | ⚪ Planned | Low |
| Email verification | ⚪ Planned | Low |
| 2FA support | ⚪ Planned | Low |

---

## 🔒 Security Best Practices

1. **HTTPS Only** — All API communication over TLS 1.3
2. **Password Hashing** — BCrypt with salt (work factor 12)
3. **Token Expiry** — Short-lived access tokens (24h)
4. **Rate Limiting** — Prevent brute force attacks on login
5. **CORS Whitelist** — Restrict allowed origins in production
6. **Audit Logging** — Log all auth events
7. **Account Lockout** — After 5 failed attempts, lock for 15 minutes
