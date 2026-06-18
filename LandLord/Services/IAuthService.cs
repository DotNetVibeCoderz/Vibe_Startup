using LandLord.Models;

namespace LandLord.Services;

/// <summary>
/// Interface untuk autentikasi user
/// </summary>
public interface IAuthService
{
    Task<User?> LoginAsync(string username, string password);
    Task<User?> RegisterAsync(string username, string email, string password, string fullName);
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<bool> ResetPasswordAsync(string email);
    Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    Task<bool> UpdateProfileAsync(User user);
    Task LogoutAsync();
}
