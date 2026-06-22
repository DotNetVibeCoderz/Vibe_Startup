# 🔐 Authentication & Authorization

## Overview

JuraganKost menggunakan **ASP.NET Core Identity** dengan cookie-based authentication untuk Blazor Server.

## Roles

| Role | Deskripsi | Akses |
|---|---|---|
| **SuperAdmin** | Administrator sistem penuh | Semua menu + manajemen user |
| **Pemilik** | Pemilik kost | Dashboard, Kamar, Penghuni, Kontrak, Tagihan, Pembayaran, Komplain, Inventaris, Staff, IoT, Laporan |
| **Admin** | Admin operasional | Sama dengan Pemilik |
| **Penghuni** | Penyewa kost | Dashboard, Tagihan Saya, Komplain, Marketplace, Mpok Inem |
| **Staff** | Staff kost | Terbatas (future) |

## Identity Configuration

```csharp
// Program.cs
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
```

## ApplicationUser

Extended Identity user:

```csharp
public class ApplicationUser : IdentityUser
{
    public string NamaLengkap { get; set; }
    public string? Alamat { get; set; }
    public string? FotoUrl { get; set; }
    public UserRoleExt RoleExt { get; set; }  // SuperAdmin, Pemilik, Admin, Penghuni, Staff
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
}
```

## Authentication Flow

```
┌──────────┐     POST /auth/login/submit     ┌──────────────┐
│  Login   │ ───────────────────────────────► │  Program.cs  │
│  Page    │                                  │  SignInMgr   │
└──────────┘                                  └──────┬───────┘
                                                     │
                                          ┌──────────▼───────┐
                                          │  Cookie Created   │
                                          │  (7 day expiry)   │
                                          └──────────┬───────┘
                                                     │
                                          ┌──────────▼───────┐
                                          │  MainLayout.razor │
                                          │  AuthState check  │
                                          │  → Role-based menu│
                                          └───────────────────┘
```

## Halaman Auth

| URL | Deskripsi | Auth Required |
|---|---|---|
| `/auth/login` | Form login | ❌ |
| `/auth/register` | Form pendaftaran | ❌ |
| `/auth/logout` | Logout + redirect | ✅ |
| `/auth/forgot-password` | Reset password (coming soon) | ❌ |
| `/auth/access-denied` | 403 page | ❌ |
| `/profil` | Edit profil + ganti password | ✅ |

## Role-Based Menu (MainLayout)

Menu di sidebar **ditampilkan sesuai role** user:

```csharp
@if (_isPemilikOrAdmin)
{
    // Kamar, Penghuni, Kontrak, Tagihan, Pembayaran,
    // Komplain, Inventaris, Staff, IoT, Laporan
}
@if (_isPenghuni)
{
    // Tagihan Saya, Komplain
}
// Semua role: Marketplace, Mpok Inem, Profil, Notifikasi
```

## Akun Demo

| Role | Email | Password |
|---|---|---|
| Super Admin | `superadmin@juragankost.com` | `Admin123!` |
| Pemilik | `pemilik@juragankost.com` | `Pemilik123!` |
| Admin | `admin@juragankost.com` | `Admin123!` |
| Penghuni 1 | `penghuni1@juragankost.com` | `Penghuni123!` |
| Penghuni 2-9 | `penghuni2@juragankost.com` ... | `Penghuni123!` |

## Authorize Attribute

Semua halaman manajemen menggunakan `@attribute [Authorize]`:

```razor
@page "/kamar"
@attribute [Authorize]
```

Halaman publik: `/marketplace`, `/auth/*`

## Cookie Configuration

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});
```
