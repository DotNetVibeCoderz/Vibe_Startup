using System.Text;
using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>Register, login, logout, password reset and "who am I".</summary>
public static class AuthEndpoints
{
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        // Anonymous, and rate limited — these are the endpoints worth brute-forcing.
        var anonymous = group.MapGroup(string.Empty).AllowAnonymous().RequireRateLimiting("auth");

        anonymous.MapPost("/register", Register)
            .WithSummary("Create a rider or driver account");

        anonymous.MapPost("/login", Login)
            .WithSummary("Exchange credentials for a JWT");

        anonymous.MapPost("/forgot-password", ForgotPassword)
            .WithSummary("Request a password reset code");

        anonymous.MapPost("/reset-password", ResetPassword)
            .WithSummary("Set a new password using a reset code");

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .WithSummary("End the session and invalidate every token issued so far");

        group.MapPost("/change-password", ChangePassword)
            .RequireAuthorization()
            .WithSummary("Change the password of the signed-in user");

        group.MapGet("/me", Me)
            .RequireAuthorization()
            .WithSummary("Profile of the signed-in user");

        return api;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, FastRideDbContext db, TokenService tokens, CancellationToken ct)
    {
        if (request.Role == UserRole.Admin)
            return Results.BadRequest(new ApiError("Invalid", "Akun admin tidak bisa dibuat lewat pendaftaran publik."));

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Results.Conflict(new ApiError("Conflict", "Email sudah terdaftar."));

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = HashPassword(request.Password),
            Role = request.Role,
            PhotoUrl = GenerateAvatar(request.FullName),
            ProfilePhotoMimeType = "image/svg+xml",
            IsActive = true
        };

        db.Users.Add(user);

        if (request.Role == UserRole.Driver)
        {
            db.DriverProfiles.Add(new DriverProfile
            {
                UserId = user.Id,
                LicenseNumber = request.LicenseNumber ?? "PENDING",
                VehicleType = request.VehicleType ?? "Unknown",
                VehiclePlate = request.VehiclePlate ?? "PENDING",
                VehicleCategory = request.VehicleCategory,
                Status = DriverStatus.Offline,
                // A new driver cannot take trips until an admin approves their documents.
                IsDocumentVerified = false
            });
        }

        db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Selamat datang di FastRide",
            Message = request.Role == UserRole.Driver
                ? "Unggah dokumen kamu (SIM, STNK, KTP) agar bisa mulai menerima order."
                : "Pakai kode WELCOME50 untuk diskon 50% di perjalanan pertama kamu.",
            Type = NotificationType.Info
        });

        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokens.Issue(user);
        return Results.Created($"/api/profile/{user.Id}", new AuthResponse(
            user.Id, user.FullName, user.Email, token, user.Role, expiresAt,
            user.PhotoUrl, user.ProfilePhotoMimeType, user.IsVerified));
    }

    private static async Task<IResult> Login(
        LoginRequest request, FastRideDbContext db, TokenService tokens, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same response for "no such user" and "wrong password" so the endpoint cannot be
        // used to enumerate which emails are registered.
        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
            return Results.Json(new ApiError("Unauthorized", "Email atau kata sandi salah."), statusCode: StatusCodes.Status401Unauthorized);

        if (!user.IsActive)
            return Results.Json(new ApiError("Forbidden", "Akun ini dinonaktifkan. Hubungi dukungan FastRide."), statusCode: StatusCodes.Status403Forbidden);

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokens.Issue(user);
        return Results.Ok(new AuthResponse(
            user.Id, user.FullName, user.Email, token, user.Role, expiresAt,
            user.PhotoUrl, user.ProfilePhotoMimeType, user.IsVerified));
    }

    private static async Task<IResult> Logout(
        HttpContext http, FastRideDbContext db, ICacheService cache, CancellationToken ct)
    {
        var userId = http.User.UserId();
        if (userId is null) return Results.Unauthorized();

        // Bumping the stamp is what actually kills the token — a JWT cannot be recalled.
        await db.Users
            .Where(u => u.Id == userId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, u => u.SecurityStamp + 1), ct);

        await cache.RemoveAsync(CacheKeys.SecurityStamp(userId.Value), ct);
        return Results.Ok(new MessageResponse("Berhasil keluar."));
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request, FastRideDbContext db, ICacheService cache,
        IWebHostEnvironment environment, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(u => u.Email == email && u.IsActive, ct);
        var expiresAt = DateTime.UtcNow.Add(ResetCodeLifetime);

        // Always answer the same way, whether or not the address is registered.
        const string message = "Kalau email terdaftar, kode reset sudah dikirim.";

        if (!exists) return Results.Ok(new ForgotPasswordResponse(message, null, expiresAt));

        var code = TokenService.GenerateResetCode();
        await cache.SetAsync(CacheKeys.PasswordReset(email), code, ResetCodeLifetime, ct);

        loggerFactory.CreateLogger("FastRide.Auth")
            .LogInformation("Password reset code issued for {Email}.", email);

        // No mail transport is wired up yet, so development returns the code directly.
        // In any other environment it is withheld — see docs/AUTH.md for the SMTP hook.
        return Results.Ok(new ForgotPasswordResponse(
            message,
            environment.IsDevelopment() ? code : null,
            expiresAt));
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request, FastRideDbContext db, ICacheService cache, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var expected = await cache.GetAsync<string>(CacheKeys.PasswordReset(email), ct);

        if (expected is null || expected != request.ResetCode.Trim())
            return Results.BadRequest(new ApiError("Invalid", "Kode reset salah atau sudah kedaluwarsa."));

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null) return Results.NotFound(new ApiError("NotFound", "Pengguna tidak ditemukan."));

        user.PasswordHash = HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        user.SecurityStamp++;                       // every existing session is now invalid
        await db.SaveChangesAsync(ct);

        await cache.RemoveAsync(CacheKeys.PasswordReset(email), ct);
        await cache.RemoveAsync(CacheKeys.SecurityStamp(user.Id), ct);

        return Results.Ok(new MessageResponse("Kata sandi berhasil diubah. Silakan masuk kembali."));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request, HttpContext http, FastRideDbContext db, ICacheService cache, CancellationToken ct)
    {
        var userId = http.User.UserId();
        if (userId is null) return Results.Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null) return Results.NotFound(new ApiError("NotFound", "Pengguna tidak ditemukan."));

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return Results.BadRequest(new ApiError("Invalid", "Kata sandi lama salah."));

        user.PasswordHash = HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        user.SecurityStamp++;
        await db.SaveChangesAsync(ct);

        await cache.RemoveAsync(CacheKeys.SecurityStamp(user.Id), ct);
        return Results.Ok(new MessageResponse("Kata sandi berhasil diubah. Silakan masuk kembali."));
    }

    private static async Task<IResult> Me(HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        var userId = http.User.UserId();
        if (userId is null) return Results.Unauthorized();

        var profile = await ProfileEndpoints.LoadProfileAsync(db, userId.Value, ct);
        return profile is null
            ? Results.NotFound(new ApiError("NotFound", "Pengguna tidak ditemukan."))
            : Results.Ok(profile);
    }

    // ─────────────────────────── helpers ───────────────────────────

    internal static string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));

    internal static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    /// <summary>Initials avatar as an inline SVG, so a new account is never a broken image.</summary>
    internal static string GenerateAvatar(string fullName)
    {
        var initials = string.Concat(fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0])));

        if (initials.Length == 0) initials = "FR";

        var palette = new[] { "#FF6B35", "#FFB020", "#23C48E", "#2979FF", "#AA00FF", "#FF5A45" };
        var color = palette[Math.Abs(fullName.GetHashCode(StringComparison.Ordinal)) % palette.Length];

        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200'>" +
                  $"<rect width='200' height='200' rx='100' fill='{color}'/>" +
                  $"<text x='100' y='132' font-size='88' font-family='Arial,sans-serif' font-weight='bold' " +
                  $"fill='white' text-anchor='middle'>{initials}</text></svg>";

        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }
}
