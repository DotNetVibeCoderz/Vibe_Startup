using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for user management and profile operations
/// </summary>
public interface IUserService
{
    Task<VibeUser?> GetUserByIdAsync(Guid userId);
    Task<VibeUser?> GetUserByPhoneAsync(string phoneNumber);
    Task<VibeUser?> GetUserByEmailAsync(string email);
    Task<bool> UpdateProfileAsync(Guid userId, VibeUser updatedProfile);
    Task<bool> UpdateProfilePictureAsync(Guid userId, string pictureUrl);
    Task<bool> SetTransactionPinAsync(Guid userId, string pin);
    Task<bool> VerifyTransactionPinAsync(Guid userId, string pin);
    Task<bool> UpdateThemePreferenceAsync(Guid userId, string theme);
    Task<string> GetUserFullNameAsync(Guid userId);
    Task<bool> IsKycVerifiedAsync(Guid userId);
}
