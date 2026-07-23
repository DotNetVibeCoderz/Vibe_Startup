using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for security operations: PIN, OTP, fraud detection
/// </summary>
public interface ISecurityService
{
    // PIN
    Task<bool> SetPinAsync(Guid userId, string pin);
    Task<bool> ValidatePinAsync(Guid userId, string pin);
    Task<bool> ResetPinAsync(Guid userId, string otpCode, string newPin);

    // OTP
    Task<OtpCode> GenerateOtpAsync(Guid userId, string purpose, string channel = "sms");
    Task<bool> VerifyOtpAsync(Guid userId, string code, string purpose);
    Task SendOtpViaSmsAsync(string phoneNumber, string code);

    // Fraud Detection
    Task<FraudAlert?> CheckTransactionForFraudAsync(Guid userId, WalletTransaction transaction);
    Task<List<FraudAlert>> GetUserFraudAlertsAsync(Guid userId);
    Task<bool> ResolveFraudAlertAsync(Guid alertId, string resolution, string resolvedBy);

    // Security Log
    Task LogSecurityEventAsync(Guid? userId, string action, string? ipAddress, string? userAgent, string? details = null);
    Task<List<SecurityLog>> GetSecurityLogsAsync(Guid userId, int page = 1, int pageSize = 20);

    // Login
    Task RecordLoginAttemptAsync(string? username, string? ipAddress, bool isSuccess, string? failureReason = null);
}
