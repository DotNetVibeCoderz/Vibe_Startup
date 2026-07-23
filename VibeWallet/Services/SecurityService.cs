using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of security service
/// </summary>
public class SecurityService : ISecurityService
{
    private readonly VibeWalletDbContext _context;
    private readonly VibeWalletConfig _config;
    private readonly ILogger<SecurityService> _logger;

    public SecurityService(VibeWalletDbContext context, IOptions<VibeWalletConfig> config,
        ILogger<SecurityService> logger)
    {
        _context = context;
        _config = config.Value;
        _logger = logger;
    }

    // ===== PIN =====
    public async Task<bool> SetPinAsync(Guid userId, string pin)
    {
        if (pin.Length != 6 || !pin.All(char.IsDigit))
            throw new ArgumentException("PIN must be exactly 6 digits");

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.TransactionPin = BCrypt.Net.BCrypt.HashPassword(pin);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ValidatePinAsync(Guid userId, string pin)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.TransactionPin)) return false;

        return BCrypt.Net.BCrypt.Verify(pin, user.TransactionPin);
    }

    public async Task<bool> ResetPinAsync(Guid userId, string otpCode, string newPin)
    {
        var otpValid = await VerifyOtpAsync(userId, otpCode, "pin_reset");
        if (!otpValid) return false;

        return await SetPinAsync(userId, newPin);
    }

    // ===== OTP =====
    public async Task<OtpCode> GenerateOtpAsync(Guid userId, string purpose, string channel = "sms")
    {
        var code = new Random().Next(100000, 999999).ToString();

        var otp = new OtpCode
        {
            UserId = userId,
            Code = code,
            Purpose = purpose,
            Channel = channel,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        _context.OtpCodes.Add(otp);
        await _context.SaveChangesAsync();

        // Log OTP generation (in real app, send via SMS/Email)
        _logger.LogInformation("OTP generated for User:{UserId} Purpose:{Purpose}", userId, purpose);

        return otp;
    }

    public async Task<bool> VerifyOtpAsync(Guid userId, string code, string purpose)
    {
        var otp = await _context.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null) return false;

        otp.AttemptCount++;

        if (otp.ExpiresAt < DateTime.UtcNow)
            return false;

        if (otp.AttemptCount > 3)
            return false;

        if (otp.Code == code)
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        await _context.SaveChangesAsync();
        return false;
    }

    public async Task SendOtpViaSmsAsync(string phoneNumber, string code)
    {
        // Simulated SMS sending - in real app integrate with SMS gateway
        _logger.LogInformation("SMS sent to {Phone}: Your VibeWallet OTP code is {Code}", phoneNumber, code);
        await Task.CompletedTask;
    }

    // ===== Fraud Detection =====
    public async Task<FraudAlert?> CheckTransactionForFraudAsync(Guid userId, WalletTransaction transaction)
    {
        // Simple fraud detection rules
        var alerts = new List<string>();

        // Rule 1: Large amount
        if (transaction.Amount > 10_000_000)
            alerts.Add("Large transaction amount detected");

        // Rule 2: Multiple transactions in short time
        var recentCount = await _context.WalletTransactions
            .CountAsync(t => t.UserId == userId &&
                            t.CreatedAt >= DateTime.UtcNow.AddMinutes(-10));
        if (recentCount > 5)
            alerts.Add("Multiple rapid transactions detected");

        // Rule 3: Different location (simulated)
        // In real app, would check IP/location

        if (alerts.Count == 0) return null;

        var alertLevel = alerts.Count switch
        {
            1 => FraudAlertLevel.Low,
            2 => FraudAlertLevel.Medium,
            _ => FraudAlertLevel.High
        };

        var fraudAlert = new FraudAlert
        {
            UserId = userId,
            TransactionRef = transaction.TransactionRef,
            AlertLevel = alertLevel,
            Description = string.Join("; ", alerts),
            TriggerReason = string.Join("; ", alerts)
        };

        _context.FraudAlerts.Add(fraudAlert);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Fraud alert for User:{UserId} Level:{Level} Desc:{Desc}",
            userId, alertLevel, fraudAlert.Description);

        return fraudAlert;
    }

    public async Task<List<FraudAlert>> GetUserFraudAlertsAsync(Guid userId)
    {
        return await _context.FraudAlerts
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ResolveFraudAlertAsync(Guid alertId, string resolution, string resolvedBy)
    {
        var alert = await _context.FraudAlerts.FindAsync(alertId);
        if (alert == null) return false;

        alert.IsResolved = true;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.Resolution = resolution;
        alert.ResolvedBy = resolvedBy;
        await _context.SaveChangesAsync();
        return true;
    }

    // ===== Security Log =====
    public async Task LogSecurityEventAsync(Guid? userId, string action, string? ipAddress,
        string? userAgent, string? details = null)
    {
        var log = new SecurityLog
        {
            UserId = userId,
            Action = action,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = details
        };

        _context.SecurityLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<SecurityLog>> GetSecurityLogsAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await _context.SecurityLogs
            .Where(l => l.UserId == userId && !l.IsDeleted)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // ===== Login =====
    public async Task RecordLoginAttemptAsync(string? username, string? ipAddress, bool isSuccess,
        string? failureReason = null)
    {
        var attempt = new LoginAttempt
        {
            Username = username,
            IpAddress = ipAddress,
            IsSuccess = isSuccess,
            FailureReason = failureReason
        };

        _context.LoginAttempts.Add(attempt);
        await _context.SaveChangesAsync();
    }
}
