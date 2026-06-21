# 🔒 Keamanan

## Authentication

### Cookie Authentication (Web)
- ASP.NET Core Identity
- Password: minimum 8 karakter, wajib digit, lowercase, uppercase, non-alphanumeric
- Cookie expiration: 7 hari, sliding expiration
- Login path: `/login`
- Anti-forgery token diaktifkan

### API Key Authentication (REST API)
- Middleware: `ApiKeyMiddleware`
- Header: `X-Api-Key: {key}`
- Query: `?api_key={key}`
- **Constant-time comparison** — mencegah timing attacks
- Swagger UI dikecualikan dari auth
- API key bisa di-generate ulang via `ApiKeyMiddleware.GenerateApiKey()`

---

## Authorization

### Role-Based Access Control

| Role | Permissions |
|------|------------|
| **Admin** | Dashboard admin, verifikasi kendaraan, manajemen user, semua laporan, peta fleet |
| **Partner** | Dashboard partner, manajemen kendaraan sendiri, pesanan kendaraan sendiri, laporan |
| **Customer** | Booking kendaraan, pembayaran, review, GPS tracking kendaraan sendiri |

### Implementasi

```razor
@* Page-level *@
@attribute [Authorize(Roles = "Admin")]

@* Component-level *@
@if (user.IsInRole("Admin")) { ... }
```

---

## Data Protection

### Password
- Hashed dengan ASP.NET Core Identity (PBKDF2)
- Tidak pernah disimpan dalam plaintext
- Minimum complexity requirement

### API Key
- Disimpan di `appsettings.json` (bisa dipindahkan ke environment variable/user secrets)
- Support generate ulang secara kriptografis:

```csharp
public static string GenerateApiKey()
{
    return "rntl-" + Convert.ToBase64String(
        RandomNumberGenerator.GetBytes(24))
        .Replace("+", "").Replace("/", "").Replace("=", "")[..32];
}
```

### Connection Strings
- Disimpan di `appsettings.json`
- **Production**: gunakan environment variables, Azure Key Vault, atau User Secrets

---

## Input Validation

### Database Queries
- Semua query via **Entity Framework Core** (parameterized)
- Tidak ada raw SQL injection vector

### Search
- Semua string search di-lowercase-kan (case-insensitive)
- Tidak ada path traversal dalam file upload
- File upload dibatasi ukuran (2MB foto profil, 5MB dokumen)

---

## CORS

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
```

⚠️ **Production**: Batasi origin spesifik untuk API.

---

## Best Practices Checklist

| Check | Status |
|-------|--------|
| ✅ Password complexity | Diterapkan |
| ✅ Anti-forgery token | Diterapkan |
| ✅ API key auth | Diterapkan |
| ✅ Role-based access | Diterapkan |
| ✅ Parameterized queries (EF Core) | Diterapkan |
| ✅ Constant-time API key comparison | Diterapkan |
| ✅ File upload size limits | Diterapkan |
| ⚠️ HTTPS only | Gunakan di production |
| ⚠️ Rate limiting | Implementasi opsional |
| ⚠️ Audit logging | Implementasi opsional |
| ⚠️ CORS restriction | Batasi di production |
