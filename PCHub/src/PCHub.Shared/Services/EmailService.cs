using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using PCHub.Shared.DTOs;
using PCHub.Shared.Interfaces;

namespace PCHub.Shared.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration? _config;
    private readonly string _fromName, _fromEmail, _smtpServer, _smtpUser, _smtpPass;
    private readonly int _smtpPort;
    private readonly bool _useSsl;

    public EmailService(IConfiguration? config = null)
    {
        _config = config;
        _fromName = config?["Email:FromName"] ?? "PCHub Game Center";
        _fromEmail = config?["Email:FromEmail"] ?? "noreply@pchub.com";
        _smtpServer = config?["Email:Smtp:Server"] ?? "localhost";
        _smtpPort = int.Parse(config?["Email:Smtp:Port"] ?? "587");
        _smtpUser = config?["Email:Smtp:Username"] ?? "";
        _smtpPass = config?["Email:Smtp:Password"] ?? "";
        _useSsl = true;
    }

    public async Task<bool> SendEmailAsync(EmailRequest request)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
            message.To.Add(MailboxAddress.Parse(request.To));
            message.Subject = request.Subject;
            var bodyBuilder = new BodyBuilder { HtmlBody = request.IsHtml ? request.Body : null, TextBody = !request.IsHtml ? request.Body : null };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
            if (!string.IsNullOrEmpty(_smtpUser)) await client.AuthenticateAsync(_smtpUser, _smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return true;
        }
        catch { return false; }
    }

    public Task<bool> SendBookingConfirmationAsync(string to, string userName, DateTime bookingDate, string pcName)
    {
        var body = $"<h2>✅ Booking Confirmed!</h2><p>Hi {userName}, booking untuk <b>{pcName}</b> pada {bookingDate:dddd, dd MMM yyyy} jam {bookingDate:HH:mm} telah dikonfirmasi.</p><p>Datang 15 menit sebelum jadwal. See you! 🎉</p>";
        return SendEmailAsync(new EmailRequest(to, "✅ Booking Confirmed - PCHub", body));
    }

    public Task<bool> SendPaymentReceiptAsync(string to, string userName, decimal amount, string transactionId)
    {
        var body = $"<h2>✅ Payment Received</h2><p>Hi {userName}, pembayaran sebesar <b>Rp {amount:N0}</b> telah diterima.</p><p>Transaction: {transactionId}</p><p>Terima kasih!</p>";
        return SendEmailAsync(new EmailRequest(to, "✅ Payment Receipt - PCHub", body));
    }
}
