using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VibeWallet.Models;
using VibeWallet.Services;

namespace VibeWallet.Api;

/// <summary>
/// Minimal API endpoints untuk Authentication (Login & Logout).
///
/// Kenapa butuh API endpoint terpisah?
/// Karena SignInManager.Set-Cookie (SignInAsync) tidak bisa dipanggil
/// dari Blazor InteractiveServer — header response sudah read-only.
///
/// Endpoint ini berjalan sebagai standard HTTP POST handler,
/// jadi bisa menulis cookie auth sebelum response dikirim.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // ===== LOGIN =====
        app.MapPost("/api/auth/login", async (
            HttpContext context,
            SignInManager<VibeUser> signInManager,
            ISecurityService securityService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");

            // 📝 Capture client info untuk audit
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();

            // Baca form data
            var form = await context.Request.ReadFormAsync();
            var username = form["user"].FirstOrDefault()?.Trim();
            var password = form["pass"].FirstOrDefault()?.Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                // 🔐 Catat login attempt gagal
                await RecordFailedLogin(securityService, username, ipAddress, userAgent, "Email dan password harus diisi.");

                return Results.Redirect("/login?error=" + Uri.EscapeDataString("Email dan password harus diisi."));
            }

            // Coba login
            var result = await signInManager.PasswordSignInAsync(
                username, password, isPersistent: false, lockoutOnFailure: false);

            // Kalau gagal, coba via nomor telepon
            if (!result.Succeeded)
            {
                var user = await signInManager.UserManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == username);
                if (user?.UserName != null)
                {
                    result = await signInManager.PasswordSignInAsync(
                        user.UserName, password, isPersistent: false, lockoutOnFailure: false);
                }
            }

            if (result.Succeeded)
            {
                logger.LogInformation("User {User} logged in successfully", username);

                // Cari user untuk dapatkan UserId
                var loggedInUser = await signInManager.UserManager.FindByNameAsync(username)
                    ?? await signInManager.UserManager.Users
                        .FirstOrDefaultAsync(u => u.PhoneNumber == username);

                // 🔐 Catat login attempt sukses
                await securityService.RecordLoginAttemptAsync(
                    username, ipAddress, isSuccess: true);

                // 📝 Catat audit log
                await securityService.LogSecurityEventAsync(
                    loggedInUser?.Id,
                    "Login",
                    ipAddress,
                    userAgent,
                    $"User '{username}' berhasil login.");

                // Baca ReturnUrl dari query string
                var returnUrl = form["ReturnUrl"].FirstOrDefault() ?? "/";
                if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
                    returnUrl = "/";

                return Results.Redirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                // 🔐 Catat login attempt — locked out
                await RecordFailedLogin(securityService, username, ipAddress, userAgent, "Akun terkunci.");

                return Results.Redirect("/login?error=" + Uri.EscapeDataString("Akun terkunci. Coba lagi nanti."));
            }

            // 🔐 Catat login attempt gagal — wrong credentials
            await RecordFailedLogin(securityService, username, ipAddress, userAgent, "Email atau password salah.");

            return Results.Redirect("/login?error=" + Uri.EscapeDataString("Email atau password salah."));
        })
        .DisableAntiforgery();

        // ===== LOGOUT =====
        app.MapPost("/api/auth/logout", async (
            HttpContext context,
            SignInManager<VibeUser> signInManager,
            ISecurityService securityService) =>
        {
            // 📝 Catat logout ke audit log sebelum sign out
            var user = await signInManager.UserManager.GetUserAsync(context.User);
            if (user != null)
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString();
                var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();

                await securityService.LogSecurityEventAsync(
                    user.Id,
                    "Logout",
                    ipAddress,
                    userAgent,
                    $"User '{user.UserName}' logout.");
            }

            await signInManager.SignOutAsync();
            return Results.Redirect("/login");
        })
        .DisableAntiforgery();

        // ===== ACCESS DENIED =====
        app.MapGet("/access-denied", () =>
            Results.Redirect("/login?error=" + Uri.EscapeDataString("Akses ditolak. Silakan login.")));
    }

    /// <summary>
    /// Helper: catat login attempt gagal + security log
    /// </summary>
    private static async Task RecordFailedLogin(
        ISecurityService securityService,
        string? username,
        string? ipAddress,
        string? userAgent,
        string reason)
    {
        await securityService.RecordLoginAttemptAsync(
            username, ipAddress, isSuccess: false, failureReason: reason);

        await securityService.LogSecurityEventAsync(
            null,
            "LoginFailed",
            ipAddress,
            userAgent,
            $"Login gagal untuk '{username}': {reason}");
    }
}
