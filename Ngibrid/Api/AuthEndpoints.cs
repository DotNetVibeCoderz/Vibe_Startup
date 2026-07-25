using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ngibrid.Models;
using Ngibrid.Services;

namespace Ngibrid.Api;

/// <summary>
/// Cookie authentication endpoints.
///
/// These exist because sign-in must happen on a real HTTP request: an interactive Blazor Server
/// component runs over an established SignalR circuit, where the response headers are long gone and
/// SignInManager cannot write the auth cookie. The auth pages therefore POST here (via fetch) and
/// then force a full page reload so the new cookie is picked up.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        // ─── Login ───
        auth.MapPost("/login", async (
            [FromBody] LoginRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            AuditService audit) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new AuthResponse(false, "Email dan password wajib diisi."));

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user == null)
                // Same message for unknown email and wrong password: revealing which one is
                // wrong lets an attacker enumerate valid accounts.
                return Results.BadRequest(new AuthResponse(false, "Email atau password salah."));

            if (!user.IsActive)
                return Results.BadRequest(new AuthResponse(false, "Akun Anda nonaktif. Hubungi administrator."));

            var result = await signInManager.PasswordSignInAsync(
                user, request.Password, request.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await audit.LogAsync("LOGIN", "User", user.Id, notes: $"{user.Email} signed in",
                    userId: user.Id.ToString());
                return Results.Ok(new AuthResponse(true, "Login berhasil.", "/"));
            }

            if (result.IsLockedOut)
                return Results.BadRequest(new AuthResponse(false,
                    "Akun terkunci sementara karena terlalu banyak percobaan. Coba lagi nanti."));

            if (result.IsNotAllowed)
                return Results.BadRequest(new AuthResponse(false, "Akun belum diizinkan masuk."));

            return Results.BadRequest(new AuthResponse(false, "Email atau password salah."));
        }).AllowAnonymous();

        // ─── Register ───
        auth.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            NotificationService notifications,
            AuditService audit) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new AuthResponse(false, "Email dan password wajib diisi."));

            if (request.Password != request.ConfirmPassword)
                return Results.BadRequest(new AuthResponse(false, "Konfirmasi password tidak cocok."));

            if (await userManager.FindByEmailAsync(request.Email) != null)
                return Results.BadRequest(new AuthResponse(false, "Email sudah terdaftar."));

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = string.IsNullOrWhiteSpace(request.FullName) ? request.Email : request.FullName,
                PhoneNumber = request.Phone,
                Address = request.Address,
                City = request.City,
                UserType = "Customer",
                EmailConfirmed = true,
                IsActive = true
            };

            var created = await userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded)
                return Results.BadRequest(new AuthResponse(false,
                    string.Join(" ", created.Errors.Select(e => e.Description))));

            await userManager.AddToRoleAsync(user, "Customer");
            await signInManager.SignInAsync(user, isPersistent: false);

            await notifications.SendAsync(user.Id, "Selamat datang di Ngibrid! 🚚",
                "Akun Anda berhasil dibuat. Mulai kirim paket pertama Anda dan kumpulkan poin loyalty.",
                "SUCCESS", "/orders");
            await audit.LogAsync("REGISTER", "User", user.Id, notes: $"{user.Email} registered",
                userId: user.Id.ToString());

            return Results.Ok(new AuthResponse(true, "Registrasi berhasil.", "/"));
        }).AllowAnonymous();

        // ─── Logout ───
        // GET so the sidebar can link to it directly; the cookie is cleared server-side.
        auth.MapGet("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/login");
        }).AllowAnonymous();

        auth.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Ok(new AuthResponse(true, "Logout berhasil.", "/login"));
        }).AllowAnonymous();

        // ─── Forgot password ───
        auth.MapPost("/forgot-password", async (
            [FromBody] ForgotPasswordRequest request,
            UserManager<ApplicationUser> userManager,
            NotificationService notifications,
            IWebHostEnvironment env) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email ?? "");

            // Always report success so this endpoint can't be used to discover registered emails.
            if (user == null)
                return Results.Ok(new AuthResponse(true,
                    "Jika email terdaftar, tautan reset telah dikirim."));

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = Uri.EscapeDataString(token);
            var resetUrl = $"/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={encoded}";

            await notifications.SendEmailAsync(user.Email!, "Reset Password Ngibrid",
                $"Buka tautan berikut untuk mengatur ulang password Anda: {resetUrl}");

            // In development the token is returned so the flow is testable without a mail server.
            return Results.Ok(new AuthResponse(true,
                "Jika email terdaftar, tautan reset telah dikirim.",
                env.IsDevelopment() ? resetUrl : null));
        }).AllowAnonymous();

        // ─── Reset password ───
        auth.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest request,
            UserManager<ApplicationUser> userManager,
            AuditService audit) =>
        {
            if (request.NewPassword != request.ConfirmPassword)
                return Results.BadRequest(new AuthResponse(false, "Konfirmasi password tidak cocok."));

            var user = await userManager.FindByEmailAsync(request.Email ?? "");
            if (user == null)
                return Results.BadRequest(new AuthResponse(false, "Token reset tidak valid atau sudah kedaluwarsa."));

            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new AuthResponse(false, "Token reset tidak ditemukan."));

            var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword ?? "");
            if (!result.Succeeded)
                return Results.BadRequest(new AuthResponse(false,
                    string.Join(" ", result.Errors.Select(e => e.Description))));

            await audit.LogAsync("RESET_PASSWORD", "User", user.Id, notes: $"{user.Email} reset password",
                userId: user.Id.ToString());
            return Results.Ok(new AuthResponse(true, "Password berhasil diubah. Silakan login.", "/login"));
        }).AllowAnonymous();

        // ─── Change password (signed in) ───
        auth.MapPost("/change-password", async (
            [FromBody] ChangePasswordRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            HttpContext http) =>
        {
            var user = await userManager.GetUserAsync(http.User);
            if (user == null) return Results.Unauthorized();

            if (request.NewPassword != request.ConfirmPassword)
                return Results.BadRequest(new AuthResponse(false, "Konfirmasi password tidak cocok."));

            var result = await userManager.ChangePasswordAsync(user,
                request.CurrentPassword ?? "", request.NewPassword ?? "");

            if (!result.Succeeded)
                return Results.BadRequest(new AuthResponse(false,
                    string.Join(" ", result.Errors.Select(e => e.Description))));

            // Refresh the cookie so the security stamp change doesn't sign the user out.
            await signInManager.RefreshSignInAsync(user);
            return Results.Ok(new AuthResponse(true, "Password berhasil diubah."));
        }).RequireAuthorization();
    }

    public record LoginRequest(string? Email, string? Password, bool RememberMe = false);

    public record RegisterRequest(string? Email, string? Password, string? ConfirmPassword,
        string? FullName, string? Phone, string? Address, string? City);

    public record ForgotPasswordRequest(string? Email);

    public record ResetPasswordRequest(string? Email, string? Token, string? NewPassword, string? ConfirmPassword);

    public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? ConfirmPassword);

    public record AuthResponse(bool Success, string Message, string? RedirectUrl = null);
}
