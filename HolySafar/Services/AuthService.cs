using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using System.Security.Cryptography;
using System.Text;

namespace HolySafar.Services;

/// <summary>
/// Auth service — state disimpan di memory (scoped) + cookie untuk cross-tab persistence.
/// Cookie: "hsauth" = "userId|token" — token disimpan di DB (ApplicationUser.AuthToken).
/// </summary>
public class AuthService
{
    private readonly AppDbContext _db;

    private int? _currentUserId;
    private string _currentUserRole = "";
    private string _currentUsername = "";
    private ApplicationUser? _cachedUser;
    private IJSRuntime? _js;

    public AuthService(AppDbContext db) { _db = db; }

    /// <summary>Dipanggil oleh MainLayout/Login saat circuit pertama kali render</summary>
    public void SetJsRuntime(IJSRuntime js) => _js = js;

    // ===== State =====
    public int? CurrentUserId => _currentUserId;
    public string CurrentUserRole => _currentUserRole;
    public string CurrentUsername => _currentUsername;
    public bool IsAuthenticated => _currentUserId != null;

    public async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        if (_currentUserId == null) return null;
        if (_cachedUser?.Id == _currentUserId) return _cachedUser;
        _cachedUser = await _db.Users.FindAsync(_currentUserId.Value);
        return _cachedUser;
    }

    // ===== INIT FROM COOKIE (called once per circuit/tab) =====
    public async Task<bool> InitializeFromCookieAsync()
    {
        if (_js == null) return false;
        if (_currentUserId != null) return true;

        try
        {
            var cookie = await _js.InvokeAsync<string>("eval",
                "document.cookie.split('; ').find(r=>r.startsWith('hsauth='))?.split('=')[1] || ''");

            if (string.IsNullOrEmpty(cookie) || !cookie.Contains('|'))
                return false;

            var parts = cookie.Split('|');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out var uid)) return false;
            var token = parts[1];

            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Id == uid && u.AuthToken == token && u.IsActive);

            if (user == null)
            {
                await ClearCookieAsync();
                return false;
            }

            _currentUserId = user.Id;
            _currentUserRole = user.Role.ToString();
            _currentUsername = user.Username;
            _cachedUser = user;
            return true;
        }
        catch { return false; }
    }

    // ===== LOGIN =====
    public async Task<(bool Success, string Message, ApplicationUser? User)> LoginAsync(string username, string password)
    {
        var hash = HashPassword(password);
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username == username && u.PasswordHash == hash);

        if (user == null)
            return (false, "Username atau password salah.", null);
        if (!user.IsActive)
            return (false, "Akun tidak aktif. Hubungi admin.", null);

        var token = GenerateAuthToken();
        user.AuthToken = token;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (_js != null)
        {
            try
            {
                var cookieValue = $"{user.Id}|{token}";
                await _js.InvokeVoidAsync("eval",
                    $"document.cookie = 'hsauth={cookieValue};path=/;max-age=2592000;SameSite=Lax'");
            }
            catch { }
        }

        _currentUserId = user.Id;
        _currentUserRole = user.Role.ToString();
        _currentUsername = user.Username;
        _cachedUser = user;

        return (true, "Login berhasil!", user);
    }

    // ===== LOGOUT =====
    public async Task LogoutAsync()
    {
        if (_currentUserId != null)
        {
            var user = await _db.Users.FindAsync(_currentUserId.Value);
            if (user != null) { user.AuthToken = null; await _db.SaveChangesAsync(); }
        }
        await ClearCookieAsync();
        _currentUserId = null; _currentUserRole = ""; _currentUsername = ""; _cachedUser = null;
    }

    // ===== REGISTER / RESET / CHANGE PASSWORD / PROFILE =====
    public async Task<(bool Success, string Message)> RegisterAsync(string username, string password, string fullName, string email, string phone)
    {
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (false, "Username sudah digunakan.");
        if (await _db.Users.AnyAsync(u => u.Email == email && !string.IsNullOrEmpty(email)))
            return (false, "Email sudah digunakan.");
        if (password.Length < 6) return (false, "Password minimal 6 karakter.");

        _db.Users.Add(new ApplicationUser
        {
            Username = username, PasswordHash = HashPassword(password),
            FullName = fullName, Email = email, Phone = phone,
            Role = UserRole.Jamaah, IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return (true, "Registrasi berhasil! Silakan login.");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string emailOrUsername)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailOrUsername || u.Username == emailOrUsername);
        if (user == null) return (false, "User tidak ditemukan.");
        if (!user.IsActive) return (false, "Akun tidak aktif.");
        var pw = GenerateRandomPassword(8);
        user.PasswordHash = HashPassword(pw);
        await _db.SaveChangesAsync();
        return (true, $"Password direset! Baru: **{pw}**\nLogin & segera ubah password.");
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User tidak ditemukan.");
        if (user.PasswordHash != HashPassword(oldPassword)) return (false, "Password lama salah.");
        if (newPassword.Length < 6) return (false, "Min. 6 karakter.");
        user.PasswordHash = HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return (true, "Password berhasil diubah!");
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string fullName, string email, string phone)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User tidak ditemukan.");
        if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email == email && !string.IsNullOrEmpty(email)))
            return (false, "Email sudah digunakan.");
        user.FullName = fullName; user.Email = email; user.Phone = phone;
        await _db.SaveChangesAsync();
        _currentUsername = user.Username; _cachedUser = user;
        return (true, "Profil berhasil diupdate!");
    }

    // ===== USER MANAGEMENT =====
    public async Task<List<ApplicationUser>> GetAllUsersAsync() =>
        await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    public async Task<ApplicationUser?> GetUserByIdAsync(int id) => await _db.Users.FindAsync(id);
    public async Task UpdateUserAsync(ApplicationUser user) { _db.Users.Update(user); await _db.SaveChangesAsync(); }
    public async Task DeleteUserAsync(int id) { var u = await _db.Users.FindAsync(id); if (u != null) { _db.Users.Remove(u); await _db.SaveChangesAsync(); } }

    // ===== HELPERS =====
    public static string HashPassword(string p) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(p + "HolySafarSalt")));

    private static string GenerateAuthToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+","").Replace("/","").Replace("=","")[..40];

    private static string GenerateRandomPassword(int len)
    {
        const string c = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        return new string(Enumerable.Range(0, len).Select(_ => c[Random.Shared.Next(c.Length)]).ToArray());
    }

    private async Task ClearCookieAsync()
    {
        if (_js != null)
        {
            try { await _js.InvokeVoidAsync("eval", "document.cookie = 'hsauth=;path=/;max-age=0'"); }
            catch { }
        }
    }
}
