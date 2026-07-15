using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AppBender.Core.Services;

public interface IEmailService
{
    /// <summary>Sends an HTML email using the SMTP settings in configuration ("Email" section) or an override config.</summary>
    Task SendAsync(string to, string subject, string htmlBody, string? cc = null,
        IDictionary<string, string>? overrideConfig = null);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, string? cc = null,
        IDictionary<string, string>? overrideConfig = null)
    {
        string Get(string key, string fallback = "") =>
            overrideConfig is not null && overrideConfig.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)
                ? v : config[$"Email:{key}"] ?? fallback;

        var host = Get("Host");
        if (string.IsNullOrEmpty(host))
        {
            // No SMTP configured: log instead of failing so demo workflows still run.
            logger.LogWarning("Email not configured; skipping send. To={To} Subject={Subject}", to, subject);
            return;
        }

        using var client = new SmtpClient(host, int.TryParse(Get("Port", "587"), out var port) ? port : 587)
        {
            EnableSsl = !string.Equals(Get("UseSsl", "true"), "false", StringComparison.OrdinalIgnoreCase),
        };
        var user = Get("Username");
        if (!string.IsNullOrEmpty(user))
            client.Credentials = new NetworkCredential(user, Get("Password"));

        using var message = new MailMessage
        {
            From = new MailAddress(Get("From", user.Length > 0 ? user : "noreply@appbender.local"), Get("FromName", "AppBender")),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        foreach (var addr in to.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(addr);
        if (!string.IsNullOrEmpty(cc))
            foreach (var addr in cc.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                message.CC.Add(addr);

        await client.SendMailAsync(message);
    }
}
