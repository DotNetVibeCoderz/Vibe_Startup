// Auth Service: Login, Register, Reset Password, Google OAuth, Profile Management
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Microsoft.AspNetCore.Identity;
using Joka.Models.Users;
using Joka.Models.Flights;
using Joka.Models.Hotels;
using Joka.Models.Trains;

namespace Joka.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    // ==========================================
    // PASSWORD HASHING
    // ==========================================
    // ASP.NET Core's PasswordHasher: PBKDF2, per-user random salt, iteration
    // count baked into the payload. The old scheme was a single SHA256 pass
    // with one hardcoded salt shared by every account - fast to brute-force
    // and identical hashes for identical passwords.
    private static readonly PasswordHasher<User> Hasher = new();

    /// <summary>Legacy scheme, kept only to verify hashes written before the change.</summary>
    private static string LegacyHash(string password) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + "JokaSalt2025!")));

    private static string HashPassword(User user, string password) => Hasher.HashPassword(user, password);

    private enum PasswordCheck { Fail, Ok, NeedsUpgrade }

    private static PasswordCheck VerifyPassword(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return PasswordCheck.Fail;

        // A legacy hash is plain base64 of 32 bytes; PasswordHasher payloads are
        // longer and start with a format marker byte.
        if (user.PasswordHash.Length == 44 && user.PasswordHash.EndsWith('='))
            return LegacyHash(password) == user.PasswordHash ? PasswordCheck.NeedsUpgrade : PasswordCheck.Fail;

        return Hasher.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Ok,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.NeedsUpgrade,
            _ => PasswordCheck.Fail
        };
    }

    // ==========================================
    // REGISTER
    // ==========================================
    public async Task<(bool Success, string Message, User? User)> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return (false, "Email sudah terdaftar.", null);

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return (false, "Username sudah digunakan.", null);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = null,   // set below, once the entity exists to salt against
            FullName = request.FullName,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            Role = "User"
        };

        user.PasswordHash = HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Send welcome notification
        _db.UserNotifications.Add(new UserNotification
        {
            UserId = user.Id,
            Title = "🎉 Selamat Datang di Joka!",
            Message = $"Halo {user.FullName ?? user.Username}! Selamat bergabung. Jelajahi berbagai layanan travel kami dan dapatkan promo spesial!",
            Type = "System",
            SentAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return (true, "Registrasi berhasil! Silakan login.", user);
    }

    // ==========================================
    // LOGIN (DB User)
    // ==========================================
    public async Task<(bool Success, string Message, User? User)> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.GoogleId == null);

        if (user == null)
            return (false, "Email atau password salah.", null);

        // Blacklist is an admin decision and outranks the automatic lockout.
        if (user.IsBlocked)
            return (false, $"Akun diblokir. {user.BlockedReason} Hubungi customer support.", null);

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            return (false, "Akun terkunci. Silakan coba lagi nanti.", null);

        var check = VerifyPassword(user, request.Password);

        if (check == PasswordCheck.Fail)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            await _db.SaveChangesAsync();
            return (false, "Email atau password salah.", null);
        }

        // Transparent migration: an account still on the old SHA256 hash gets
        // re-hashed with PBKDF2 the first time it signs in successfully.
        if (check == PasswordCheck.NeedsUpgrade)
            user.PasswordHash = HashPassword(user, request.Password);

        // Reset failed attempts on success
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (true, "Login berhasil!", user);
    }

    // ==========================================
    // GOOGLE OAUTH LOGIN/REGISTER
    // ==========================================
    public async Task<(bool Success, string Message, User? User)> GoogleLoginAsync(string googleId, string email, string name, string? avatarUrl)
    {
        // Cari user yang sudah terdaftar via Google
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);

        if (user == null)
        {
            // Cek apakah email sudah dipakai untuk akun biasa
            var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
            {
                // Link Google ID ke akun existing
                existingUser.GoogleId = googleId;
                existingUser.LastLoginAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return (true, "Google login berhasil! Akun kamu sudah terhubung.", existingUser);
            }

            // Auto-register via Google
            user = new User
            {
                Username = email.Split('@')[0],
                Email = email,
                FullName = name,
                AvatarUrl = avatarUrl,
                GoogleId = googleId,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                Role = "User"
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Welcome notification
            _db.UserNotifications.Add(new UserNotification
            {
                UserId = user.Id,
                Title = "🎉 Selamat Datang di Joka!",
                Message = $"Halo {user.FullName ?? user.Username}! Akun Google kamu berhasil terhubung.",
                Type = "System",
                SentAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return (true, "Google login berhasil!", user);
    }

    // ==========================================
    // RESET PASSWORD
    // ==========================================
    public async Task<(bool Success, string Message)> RequestPasswordResetAsync(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.GoogleId == null);
        if (user == null)
            return (true, "Jika email terdaftar, link reset password akan dikirim."); // Don't reveal existence

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
        user.ResetToken = token;
        user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        // In production: send email with reset link
        // For demo: we'll display the token in the UI (not secure, just for demo)
        return (true, $"Link reset: /reset-password?token={token}");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(ChangePasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.ResetToken == request.Token && u.ResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
            return (false, "Token tidak valid atau sudah kadaluarsa.");

        user.PasswordHash = HashPassword(user, request.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync();

        return (true, "Password berhasil direset! Silakan login dengan password baru.");
    }

    // ==========================================
    // PROFILE MANAGEMENT
    // ==========================================
    public async Task<User?> GetUserByIdAsync(Guid userId)
        => await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);

    public async Task<(bool Success, string Message)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User tidak ditemukan.");

        user.FullName = request.FullName ?? user.FullName;
        user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
        // Null means "leave it alone"; an empty string is an explicit "clear it",
        // which is how the profile page removes a photo.
        user.AvatarUrl = request.AvatarUrl is null
            ? user.AvatarUrl
            : string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl;
        user.PreferredLanguage = request.PreferredLanguage ?? user.PreferredLanguage;
        user.PreferredCurrency = request.PreferredCurrency ?? user.PreferredCurrency;
        user.Theme = request.Theme ?? user.Theme;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, "Profil berhasil diperbarui!");
    }

    // ==========================================
    // BOOKINGS
    // ==========================================
    public async Task<List<FlightBooking>> GetUserFlightBookingsAsync(Guid userId)
        => await _db.FlightBookings
            .Include(b => b.Flight!).ThenInclude(f => f.Airline)
            .Include(b => b.Flight!).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight!).ThenInclude(f => f.ArrivalAirport)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();

    public async Task<List<HotelBooking>> GetUserHotelBookingsAsync(Guid userId)
        => await _db.HotelBookings
            .Include(b => b.Room!).ThenInclude(r => r.Hotel)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();

    public async Task<List<TrainBooking>> GetUserTrainBookingsAsync(Guid userId)
        => await _db.TrainBookings
            .Include(b => b.TrainSchedule!).ThenInclude(s => s.Train)
            .Include(b => b.TrainSchedule!).ThenInclude(s => s.DepartureStation)
            .Include(b => b.TrainSchedule!).ThenInclude(s => s.ArrivalStation)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();

    public async Task<List<WishlistItem>> GetUserWishlistAsync(Guid userId)
        => await _db.WishlistItems
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .ToListAsync();

    public async Task<List<UserNotification>> GetUserNotificationsAsync(Guid userId)
        => await _db.UserNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.SentAt)
            .Take(50)
            .ToListAsync();

    public async Task AddToWishlistAsync(Guid userId, string type, string itemId, string name, decimal? price, string? imageUrl)
    {
        if (await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ItemId == itemId && w.ItemType == type))
            return; // Already in wishlist

        _db.WishlistItems.Add(new WishlistItem
        {
            UserId = userId,
            ItemType = type,
            ItemId = itemId,
            ItemName = name,
            Price = price,
            ItemImageUrl = imageUrl,
            AddedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveFromWishlistAsync(Guid userId, Guid wishlistId)
    {
        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.Id == wishlistId && w.UserId == userId);
        if (item != null)
        {
            _db.WishlistItems.Remove(item);
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkNotificationReadAsync(Guid notificationId)
    {
        var notif = await _db.UserNotifications.FindAsync(notificationId);
        if (notif != null)
        {
            notif.IsRead = true;
            notif.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}


