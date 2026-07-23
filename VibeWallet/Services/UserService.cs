using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of user management service
/// </summary>
public class UserService : IUserService
{
    private readonly VibeWalletDbContext _context;
    private readonly UserManager<VibeUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(VibeWalletDbContext context, UserManager<VibeUser> userManager, ILogger<UserService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<VibeUser?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<VibeUser?> GetUserByPhoneAsync(string phoneNumber)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
    }

    public async Task<VibeUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<bool> UpdateProfileAsync(Guid userId, VibeUser updatedProfile)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.FullName = updatedProfile.FullName;
        user.Address = updatedProfile.Address;
        user.City = updatedProfile.City;
        user.Province = updatedProfile.Province;
        user.PostalCode = updatedProfile.PostalCode;
        user.DateOfBirth = updatedProfile.DateOfBirth;
        user.Gender = updatedProfile.Gender;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateProfilePictureAsync(Guid userId, string pictureUrl)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.ProfilePictureUrl = pictureUrl;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetTransactionPinAsync(Guid userId, string pin)
    {
        if (pin.Length != 6 || !pin.All(char.IsDigit))
            throw new ArgumentException("PIN must be exactly 6 digits");

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.TransactionPin = BCrypt.Net.BCrypt.HashPassword(pin);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> VerifyTransactionPinAsync(Guid userId, string pin)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.TransactionPin))
            return false;

        // Check if PIN is locked
        if (user.PinLockedUntil.HasValue && user.PinLockedUntil > DateTime.UtcNow)
            throw new InvalidOperationException("PIN is locked. Please try again later.");

        var isValid = BCrypt.Net.BCrypt.Verify(pin, user.TransactionPin);

        if (!isValid)
        {
            user.FailedPinAttempts++;
            if (user.FailedPinAttempts >= 5)
                user.PinLockedUntil = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();
        }
        else
        {
            user.FailedPinAttempts = 0;
            user.PinLockedUntil = null;
            await _context.SaveChangesAsync();
        }

        return isValid;
    }

    public async Task<bool> UpdateThemePreferenceAsync(Guid userId, string theme)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.ThemePreference = theme;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> GetUserFullNameAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.FullName ?? "Unknown";
    }

    public async Task<bool> IsKycVerifiedAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.KycStatus == KycStatus.Verified;
    }
}
