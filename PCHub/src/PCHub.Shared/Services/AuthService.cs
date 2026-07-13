using Microsoft.EntityFrameworkCore;
using PCHub.Shared.Data;
using PCHub.Shared.DTOs;
using PCHub.Shared.Interfaces;
using PCHub.Shared.Models;

namespace PCHub.Shared.Services;

/// <summary>
/// Auth service untuk login, register, reset password, dan profil
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username == request.Username || u.Email == request.Username);

        if (user == null || !user.IsActive)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToAuthResponse(user);
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email))
            return null;

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = Enums.UserRole.Member,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return MapToAuthResponse(user);
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null) return false;

        user.ResetToken = Guid.NewGuid().ToString("N");
        user.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);
        await _db.SaveChangesAsync();

        // In production: send email with reset link
        return true;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<UserProfileResponse?> GetProfileAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user == null ? null : MapToProfile(user);
    }

    private static AuthResponse MapToAuthResponse(User user) => new(
        UserId: user.Id,
        Username: user.Username,
        Email: user.Email,
        FullName: user.FullName,
        Role: user.Role.ToString(),
        Token: GenerateSimpleToken(user.Id)
    );

    private static UserProfileResponse MapToProfile(User user) => new(
        Id: user.Id,
        Username: user.Username,
        Email: user.Email,
        FullName: user.FullName,
        PhoneNumber: user.PhoneNumber,
        Role: user.Role,
        MembershipTier: user.MembershipTier,
        LoyaltyPoints: user.LoyaltyPoints,
        Balance: user.Balance,
        CreatedAt: user.CreatedAt
    );

    private static string GenerateSimpleToken(Guid userId)
    {
        return Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{userId}:{DateTime.UtcNow.Ticks}:{Guid.NewGuid()}")
        );
    }
}
