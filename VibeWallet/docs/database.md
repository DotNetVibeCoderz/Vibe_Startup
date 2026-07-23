# VibeWallet Database Schema

## Overview

VibeWallet uses Entity Framework Core with support for multiple database providers:
- **SQLite** (default, for development)
- **SQL Server**
- **MySQL**
- **PostgreSQL**

Configure the provider in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Provider": "SQLite",
    "SQLite": "Data Source=Data/VibeWallet.db"
  }
}
```

---

## Entity Relationship Diagram (Simplified)

```
VibeUser (extends IdentityUser<Guid>)
├── Wallet (1:1)
│   ├── WalletTransaction (1:N)
│   └── BalanceHistory (1:N)
├── BankAccount (1:N)
├── KycDocument (1:N)
├── KycSelfie (1:N)
├── ChatSession (1:N)
│   └── ChatMessage (1:N)
│       └── ChatAttachment (1:N)
├── OtpCode (1:N)
└── ...

SupportedBank
├── BankAccount (referenced by)
└── BankTransfer (referenced by)

SplitBill
└── SplitBillParticipant (1:N)
    └── VibeUser (referenced by)

Voucher
└── UserVoucher (1:N)
    └── VibeUser (referenced by)
```

---

## Core Tables

### VibeUser
Extends `IdentityUser<Guid>` with additional fields:

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid (PK) | User ID |
| FullName | nvarchar(200) | Full name |
| PhoneNumber | nvarchar(20) | Phone number (unique) |
| IdentityNumber | nvarchar(50) | KTP/NIK number |
| KycStatus | int | KYC status enum |
| TransactionPin | nvarchar(6) | Hashed transaction PIN |
| ThemePreference | nvarchar(10) | UI theme (light/dark) |

### Wallet

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid (PK) | Wallet ID |
| UserId | Guid (FK→VibeUser) | Owner user |
| WalletNumber | nvarchar(16) | Unique wallet number |
| Balance | decimal(18,2) | Current balance |
| HoldBalance | decimal(18,2) | Amount on hold |
| LoyaltyPoints | int | Loyalty points balance |

### WalletTransaction

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid (PK) | Transaction ID |
| WalletId | Guid (FK→Wallet) | Wallet |
| TransactionRef | nvarchar(50) | Unique reference |
| Type | int | Transaction type enum |
| Status | int | Transaction status enum |
| Amount | decimal(18,2) | Transaction amount |
| BalanceBefore | decimal(18,2) | Balance before |
| BalanceAfter | decimal(18,2) | Balance after |

---

## Payment Tables

### QrisPayment
QRIS payment records with QR content, merchant info, and tip amount.

### BillPayment
Bill payment records with bill type (electricity, water, etc.), provider, customer ID.

### MobileTopUp
Mobile top-up records with top-up type, provider, phone number, product code.

### EcommercePayment
E-commerce payment records with platform name, order ID.

---

## Transfer Tables

### P2PTransfer
Peer-to-peer transfer between VibeWallet users.

### SplitBill & SplitBillParticipant
Split bill management with participants and payment status.

---

## Reward Tables

### Cashback
Cashback earned from transactions.

### LoyaltyPoint
Points earned/redeemed with source tracking.

### Voucher & UserVoucher
Voucher catalog and user-claimed vouchers.

### Promo
Active promotions with category and validity.

---

## Investment Tables

### SavingsAccount & SavingsTransaction
Digital savings with interest calculation.

### Investment
Investment portfolio (mutual funds, gold, etc.).

### InsuranceProduct & UserInsurance
Insurance products and user enrollments.

---

## Security Tables

### OtpCode
One-time password codes for verification.

### FraudAlert
Fraud detection alerts.

### SecurityLog
Audit trail for security events.

### LoginAttempt
Login attempt tracking.

---

## Chat Tables

### ChatSession
Chat conversation sessions with provider/model config.

### ChatMessage
Chat messages with markdown rendering and token tracking.

### ChatAttachment
File attachments (images, documents) for chat messages.

---

## Indexes

Key indexes for performance:
- `WalletTransaction`: (TransactionRef) UNIQUE, (WalletId), (UserId), (CreatedAt), (Status)
- `P2PTransfer`: (TransferRef) UNIQUE
- `VibeUser`: (PhoneNumber) UNIQUE, (IdentityNumber) UNIQUE
- `ChatMessage`: (ChatSessionId), (Role)
- All date columns used in WHERE clauses

## Soft Delete

All entities inheriting `BaseEntity` support soft delete via `IsDeleted` field. When deleted, records are marked instead of physically removed.
