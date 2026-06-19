# 📋 Comblang — Development Plan

## ✅ Build: SUCCESS (0 Errors) | Runtime: RUNNING ✅

---

## 🔐 Auth Flow (Batch 5 — Complete Overhaul)

### Yang Diperbaiki / Ditambahkan:

| Fitur | Sebelum | Sekarang |
|-------|---------|----------|
| **Login** | Cuma navigate ke /swipe | ✅ `AuthService.LoginAsync()` → `HttpContext.SignInAsync()` cookie auth |
| **Register** | Cuma navigate ke /swipe | ✅ `AuthService.RegisterAsync()` → auto-login via cookie |
| **Logout** | Tidak ada | ✅ `HttpContext.SignOutAsync()` di NavMenu, redirect ke home |
| **Reset Password** | Tidak ada | ✅ 2-step: request token → reset password. Token ditampilkan di DEV mode |
| **Profile Edit** | Tidak ada | ✅ Form lengkap: bio, gender, DOB, pekerjaan, lokasi, foto profil upload, ganti password |
| **Foto Profil** | Tidak ada | ✅ Upload via `InputFile` → `IStorageProvider` |
| **Authorize Guard** | Tidak ada | ✅ `AuthorizeRouteView` di Routes.razor, redirect ke login |
| **NavMenu User Info** | ❌ Static "Guest" | ✅ `AuthenticationStateProvider` → tampilkan username + logout button |
| **Password Hashing** | ✅ SHA256 | ✅ SHA256 + validation (min 6 chars) |
| **Change Password** | Tidak ada | ✅ `AuthService.ChangePasswordAsync()` di Edit Profile |
| **HttpContextAccessor** | Tidak ada | ✅ Registered di DI, digunakan untuk SignIn/SignOut |

### Auth Flow Lengkap:
```
1. User buka /swipe → redirect ke /auth/login (belum login)
2. Register: isi email+username+password → AuthService.RegisterAsync() → auto SignIn cookie → /swipe
3. Login: isi email+password → AuthService.LoginAsync() → SignIn cookie → /swipe
4. Lupa password: /auth/reset-password → email → dapat token → reset → login
5. Edit profil: /profile/edit → upload foto, isi bio, ganti password → simpan
6. Logout: klik 🚪 Logout di sidebar → SignOut cookie → home
```

### Halaman Baru:
- `/auth/reset-password` — Reset password 2-step
- `/profile/edit` — Edit profil + upload foto + ganti password (perlu login `[Authorize]`)

### File yang Diupdate:
| File | Perubahan |
|------|-----------|
| `Models/User.cs` | +ResetToken, +ResetTokenExpiry |
| `Services/Auth/AuthService.cs` | +RegisterAsync, +LoginAsync, +CreateClaimsPrincipal, +RequestPasswordResetAsync, +ResetPasswordAsync, +ChangePasswordAsync, +GetUserByIdAsync, +UpdateProfileAsync, +UploadProfilePhotoAsync |
| `Components/Pages/Auth/Login.razor` | Panggil AuthService beneran, SignIn cookie |
| `Components/Pages/Auth/ResetPassword.razor` | **BARU** — 2-step reset |
| `Components/Pages/Profile/Edit.razor` | **BARU** — Foto + profil + password |
| `Components/Layout/NavMenu.razor` | User info + logout button |
| `Components/Routes.razor` | `AuthorizeRouteView` + `CascadingAuthenticationState` |
| `Program.cs` | `AddHttpContextAccessor()`, `AddCascadingAuthenticationState()`, `SlidingExpiration` |
| `Components/_Imports.razor` | +Profile namespace |
