using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Comblang.Data;
using Comblang.Models;
using Comblang.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Comblang.Services.Auth;

/// <summary>
/// Handles user registration, login, password reset, profile management,
/// JWT token generation, and API-key validation.
/// </summary>
public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IStorageProvider _storage;

    public AuthService(AppDbContext db, IConfiguration config, IStorageProvider storage)
    {
        _db = db;
        _config = config;
        _storage = storage;
    }

    // ──────────────────────────────────────────────
    //  Password hashing
    // ──────────────────────────────────────────────

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    // ──────────────────────────────────────────────
    //  Registration
    // ──────────────────────────────────────────────

    public async Task<(User? user, string? error)> RegisterAsync(
        string email, string username, string password)
    {
        email = email.Trim().ToLowerInvariant();
        username = username.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (null, "Email sudah terdaftar.");
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (null, "Username sudah digunakan.");
        if (password.Length < 6)
            return (null, "Password minimal 6 karakter.");

        var user = new User
        {
            Email = email,
            Username = username,
            PasswordHash = HashPassword(password),
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Profiles.Add(new Profile { UserId = user.Id });
        await _db.SaveChangesAsync();

        return (user, null);
    }

    // ──────────────────────────────────────────────
    //  Login
    // ──────────────────────────────────────────────

    public async Task<(string? token, User? user, string? error)> LoginAsync(
        string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var hashed = HashPassword(password);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hashed);

        if (user == null)
            return (null, null, "Email atau password salah.");
        if (user.IsBanned)
            return (null, null, "Akun telah diblokir.");

        user.LastActiveAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        return (token, user, null);
    }

    /// <summary>
    /// Builds a ClaimsPrincipal from a user for cookie authentication.
    /// </summary>
    public static ClaimsPrincipal CreateClaimsPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, "ComblangCookie");
        return new ClaimsPrincipal(identity);
    }

    // ──────────────────────────────────────────────
    //  Password Reset
    // ──────────────────────────────────────────────

    /// <summary>
    /// Generates a reset token for the given email and returns it.
    /// </summary>
    public async Task<(bool success, string? message, string? token)> RequestPasswordResetAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
            // Don't reveal whether the email exists
            return (true, "Jika email terdaftar, link reset password telah dikirim.", null);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.ResetToken = token;
        user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        return (true, "Link reset password telah dikirim ke email kamu.", token);
    }

    /// <summary>
    /// Resets password using a valid reset token.
    /// </summary>
    public async Task<(bool success, string message)> ResetPasswordAsync(
        string email, string token, string newPassword)
    {
        email = email.Trim().ToLowerInvariant();

        if (newPassword.Length < 6)
            return (false, "Password baru minimal 6 karakter.");

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == email && u.ResetToken == token && u.ResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
            return (false, "Link reset tidak valid atau sudah kadaluarsa.");

        user.PasswordHash = HashPassword(newPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        return (true, "Password berhasil direset! Silakan login dengan password baru.");
    }

    /// <summary>
    /// Changes password for authenticated user.
    /// </summary>
    public async Task<(bool success, string message)> ChangePasswordAsync(
        Guid userId, string oldPassword, string newPassword)
    {
        if (newPassword.Length < 6)
            return (false, "Password baru minimal 6 karakter.");

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, "User tidak ditemukan.");

        if (user.PasswordHash != HashPassword(oldPassword))
            return (false, "Password lama salah.");

        user.PasswordHash = HashPassword(newPassword);
        await _db.SaveChangesAsync();

        return (true, "Password berhasil diubah!");
    }

    // ──────────────────────────────────────────────
    //  User queries
    // ──────────────────────────────────────────────

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.InterestTags)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    // ──────────────────────────────────────────────
    //  Profile update
    // ──────────────────────────────────────────────

    /// <summary>
    /// Updates user profile fields. Returns updated profile.
    /// </summary>
    public async Task<(Profile? profile, string? error)> UpdateProfileAsync(
        Guid userId, string? bio, string? gender, DateTime? dateOfBirth,
        string? occupation, string? education, string? relationshipGoal,
        int heightCm, string? city, string? country)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new Profile { UserId = userId };
            _db.Profiles.Add(profile);
        }

        if (bio != null) profile.Bio = bio;
        if (gender != null) profile.Gender = gender;
        if (dateOfBirth.HasValue) profile.DateOfBirth = dateOfBirth.Value;
        if (occupation != null) profile.Occupation = occupation;
        if (education != null) profile.Education = education;
        if (relationshipGoal != null) profile.RelationshipGoal = relationshipGoal;
        if (heightCm > 0) profile.HeightCm = heightCm;

        // Update location on User entity too
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            if (city != null) user.City = city;
            if (country != null) user.Country = country;
        }

        await _db.SaveChangesAsync();
        return (profile, null);
    }

    /// <summary>
    /// Uploads a profile photo and returns the URL.
    /// </summary>
    public async Task<(string? url, string? error)> UploadProfilePhotoAsync(
        Guid userId, Stream fileStream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif" && ext != ".webp")
            return (null, "Format file tidak didukung. Gunakan JPG, PNG, GIF, atau WebP.");

        var storedName = $"profiles/{userId}/{Guid.NewGuid()}{ext}";
        var url = await _storage.UploadAsync(storedName, fileStream, contentType);

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new Profile { UserId = userId };
            _db.Profiles.Add(profile);
        }
        profile.ProfilePictureUrl = url;
        await _db.SaveChangesAsync();

        return (url, null);
    }

    // ──────────────────────────────────────────────
    //  JWT Token
    // ──────────────────────────────────────────────

    public string GenerateJwtToken(User user)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JWT secret is not configured.");
        var issuer = jwtSettings["Issuer"] ?? "Comblang";
        var audience = jwtSettings["Audience"] ?? "ComblangApp";
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "1440");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer, audience: audience, claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ──────────────────────────────────────────────
    //  API Key
    // ──────────────────────────────────────────────

    public bool ValidateApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;
        var expected = _config["ApiKey"];
        return string.Equals(apiKey?.Trim(), expected, StringComparison.Ordinal);
    }
}
