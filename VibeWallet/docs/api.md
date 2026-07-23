# VibeWallet API Documentation

## Overview

VibeWallet provides a REST API built with .NET Minimal API for external integrations. All endpoints are available at `/api/v1/`.

## Authentication

Most endpoints require authentication via ASP.NET Core Identity cookies. Include the authentication cookie in your requests.

```
GET /api/v1/wallet/balance
Cookie: .AspNetCore.Identity.Application=<token>
```

## Base URL

```
https://localhost:5001/api/v1
```

---

## Endpoints

### Health Check

#### `GET /api/v1/health`
Check if the API is running.

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2025-01-01T00:00:00Z",
  "app": "VibeWallet"
}
```

---

### Wallet

#### `GET /api/v1/wallet/balance`
Get current wallet balance.

**Response:**
```json
{
  "userId": "guid",
  "balance": 5000000.00
}
```

#### `GET /api/v1/wallet/{userId}`
Get wallet details for a specific user.

#### `POST /api/v1/wallet/topup`
Top up wallet balance.

**Request:**
```json
{
  "amount": 100000,
  "method": "BankTransfer",
  "notes": "Top up bulanan"
}
```

#### `POST /api/v1/wallet/withdraw`
Withdraw from wallet to bank account.

**Request:**
```json
{
  "amount": 50000,
  "bankAccount": "1234567890",
  "notes": "Tarik tunai"
}
```

#### `GET /api/v1/wallet/transactions?page=1&pageSize=20`
Get transaction history.

---

### Payments

#### `POST /api/v1/payments/qris`
Process QRIS payment.

**Request:**
```json
{
  "qrContent": "000201010211...",
  "amount": 50000,
  "notes": "Belanja di warung"
}
```

#### `POST /api/v1/payments/bill`
Pay a bill.

**Request:**
```json
{
  "billType": "Electricity",
  "providerName": "PLN",
  "customerId": "12345678901",
  "amount": 350000,
  "billPeriod": "202501"
}
```

#### `GET /api/v1/payments/bill/check?type=Electricity&customerId=12345678901`
Check bill amount before paying.

#### `POST /api/v1/payments/topup`
Process mobile top-up.

**Request:**
```json
{
  "topUpType": "Pulsa",
  "provider": "Telkomsel",
  "phoneNumber": "08123456789",
  "productCode": "P50K",
  "amount": 49500
}
```

---

### Transfers

#### `POST /api/v1/transfers/p2p`
Send P2P transfer.

**Request:**
```json
{
  "receiverWalletNumber": "1000000000000002",
  "amount": 100000,
  "notes": "Uang makan"
}
```

#### `POST /api/v1/transfers/split`
Create a split bill.

**Request:**
```json
{
  "title": "Makan bareng",
  "totalAmount": 500000,
  "participantIds": ["guid1", "guid2"],
  "description": "Makan di restoran"
}
```

#### `POST /api/v1/transfers/split/{splitBillId}/pay`
Pay your split bill share.

---

### Banks

#### `GET /api/v1/banks`
Get list of supported banks.

#### `GET /api/v1/banks/accounts`
Get user's linked bank accounts.

#### `POST /api/v1/banks/transfer`
Transfer to bank account.

**Request:**
```json
{
  "bankCode": "014",
  "accountNumber": "1234567890",
  "amount": 250000,
  "notes": "Transfer ke BCA"
}
```

---

### Rewards

#### `GET /api/v1/rewards/points`
Get user's loyalty points.

#### `GET /api/v1/rewards/vouchers`
Get available vouchers.

#### `GET /api/v1/rewards/promos?category=food`
Get active promos, optional category filter.

#### `POST /api/v1/rewards/vouchers/{voucherId}/claim`
Claim a voucher.

---

### Chat Bot

#### `GET /api/v1/chat/sessions`
Get user's chat sessions.

#### `POST /api/v1/chat/sessions`
Create new chat session.

**Request:**
```json
{
  "title": "Tanya promo",
  "provider": "OpenAI"
}
```

#### `POST /api/v1/chat/sessions/{sessionId}/messages`
Send message to chat.

**Request:**
```json
{
  "message": "Halo Mbak Selvi, cek saldo dong",
  "attachments": null
}
```

#### `GET /api/v1/chat/sessions/{sessionId}/messages`
Get session messages.

#### `DELETE /api/v1/chat/sessions/{sessionId}`
Delete session.

---

## Error Responses

All errors follow this format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Error description"
}
```

## Rate Limiting

- Daily transfer limit: Rp 25,000,000
- Daily top-up limit: Rp 10,000,000
- Daily payment limit: Rp 50,000,000
- Monthly transfer limit: Rp 100,000,000
