using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HolySafar.Data;
using HolySafar.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Auth service di atas ASP.NET Core cookie authentication.
///
/// Cookie "hsauth" ditulis SERVER-SIDE oleh endpoint /auth/login (lihat AuthEndpoints)
/// dengan flag HttpOnly + SameSite=Lax + Secure, jadi tidak bisa dibaca/dipalsukan
/// dari JavaScript. Identitas dibawa ke circuit Blazor lewat AuthenticationStateProvider.
///
/// Password disimpan sebagai PBKDF2-SHA256 dengan salt acak per user
/// (format: pbkdf2$iterasi$salt$hash). Hash SHA256 lama masih bisa login dan
/// otomatis di-upgrade ke PBKDF2 saat login berhasil.
/// </summary>
public class AuthService
{
    private readonly AppDbContext _db;
    private readonly AuthenticationStateProvider _authState;

    private bool _initialized;
    private int? _currentUserId;
    private string _currentUserRole = "";
    private string _currentUsername = "";
    private ApplicationUser? _cachedUser;

    public AuthService(AppDbContext db, AuthenticationStateProvider authState)
    { _db = db; _authState = authState; }

    // ===== State (baca setelah EnsureAsync) =====
    public int? CurrentUserId => _currentUserId;
    public string CurrentUserRole => _currentUserRole;
    public string CurrentUsername => _currentUsername;
    public bool IsAuthenticated => _currentUserId != null;

    /// <summary>
    /// Memuat identitas dari cookie auth. Panggil di awal OnInitializedAsync
    /// pada komponen yang membaca CurrentUserId/CurrentUserRole.
    /// </summary>
    public async Task EnsureAsync()
    {
        if (_initialized) return;
        _initialized = true;

        var state = await _authState.GetAuthenticationStateAsync();
        var user = state.User;
        if (user.Identity?.IsAuthenticated != true) return;

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var uid)) return;

        _currentUserId = uid;
        _currentUsername = user.FindFirst(ClaimTypes.Name)?.Value ?? "";
        _currentUserRole = user.FindFirst(ClaimTypes.Role)?.Value ?? "";
    }

    public async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        await EnsureAsync();
        if (_currentUserId == null) return null;
        if (_cachedUser?.Id == _currentUserId) return _cachedUser;
        _cachedUser = await _db.Users.FindAsync(_currentUserId.Value);
        return _cachedUser;
    }

    /// <summary>Buang cache user (dipakai setelah profil diubah).</summary>
    public void InvalidateCache() => _cachedUser = null;

    // ===== KREDENSIAL =====

    /// <summary>
    /// Validasi username/password. Dipakai endpoint /auth/login — tidak menyentuh cookie.
    /// Hash SHA256 warisan otomatis di-upgrade ke PBKDF2 di sini.
    /// </summary>
    public async Task<(bool Success, string Message, ApplicationUser? User)> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !VerifyPassword(password, user.PasswordHash))
            return (false, "Username atau password salah.", null);
        if (!user.IsActive)
            return (false, "Akun tidak aktif. Hubungi admin.", null);

        if (NeedsRehash(user.PasswordHash)) user.PasswordHash = HashPassword(password);
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (true, "Login berhasil!", user);
    }

    /// <summary>Claim yang disimpan di cookie auth.</summary>
    public static ClaimsPrincipal BuildPrincipal(ApplicationUser user, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("FullName", user.FullName)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    // ===== REGISTER / RESET / CHANGE PASSWORD / PROFILE =====

    public async Task<(bool Success, string Message)> RegisterAsync(string username, string password, string fullName, string email, string phone)
    {
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (false, "Username sudah digunakan.");
        if (!string.IsNullOrEmpty(email) && await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Email sudah digunakan.");
        if (password.Length < 8) return (false, "Password minimal 8 karakter.");

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
        var pw = GenerateRandomPassword(10);
        user.PasswordHash = HashPassword(pw);
        await _db.SaveChangesAsync();
        return (true, $"Password direset! Baru: **{pw}**\nLogin & segera ubah password.");
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User tidak ditemukan.");
        if (!VerifyPassword(oldPassword, user.PasswordHash)) return (false, "Password lama salah.");
        if (newPassword.Length < 8) return (false, "Password minimal 8 karakter.");
        user.PasswordHash = HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return (true, "Password berhasil diubah!");
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string fullName, string email, string phone)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User tidak ditemukan.");
        if (!string.IsNullOrEmpty(email) && await _db.Users.AnyAsync(u => u.Id != userId && u.Email == email))
            return (false, "Email sudah digunakan.");
        user.FullName = fullName; user.Email = email; user.Phone = phone;
        await _db.SaveChangesAsync();
        _cachedUser = user;
        return (true, "Profil berhasil diupdate!");
    }

    // ===== USER MANAGEMENT =====
    public async Task<List<ApplicationUser>> GetAllUsersAsync() =>
        await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    public async Task<ApplicationUser?> GetUserByIdAsync(int id) => await _db.Users.FindAsync(id);
    public async Task UpdateUserAsync(ApplicationUser user) { _db.Users.Update(user); await _db.SaveChangesAsync(); }
    public async Task DeleteUserAsync(int id) { var u = await _db.Users.FindAsync(id); if (u != null) { _db.Users.Remove(u); await _db.SaveChangesAsync(); } }

    // ===== HASHING =====

    private const int Pbkdf2Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string LegacySalt = "HolySafarSalt";

    /// <summary>PBKDF2-SHA256, salt acak per user. Format: pbkdf2$iterasi$saltB64$hashB64</summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>Verifikasi tahan timing-attack; menerima hash PBKDF2 baru maupun SHA256 lama.</summary>
    public static bool VerifyPassword(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;

        if (stored.StartsWith("pbkdf2$", StringComparison.Ordinal))
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iter)) return false;
            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch { return false; }
        }

        // hash warisan: Base64(SHA256(password + "HolySafarSalt"))
        var legacy = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + LegacySalt)));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(legacy), Encoding.UTF8.GetBytes(stored));
    }

    public static bool NeedsRehash(string stored) => !stored.StartsWith("pbkdf2$", StringComparison.Ordinal);

    private static string GenerateRandomPassword(int len)
    {
        const string c = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        return new string(Enumerable.Range(0, len).Select(_ => c[RandomNumberGenerator.GetInt32(c.Length)]).ToArray());
    }
}
