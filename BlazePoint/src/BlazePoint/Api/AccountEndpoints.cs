using BlazePoint.Data;
using BlazePoint.Services;
using Microsoft.AspNetCore.Identity;

namespace BlazePoint.Api;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        // NOTE: form-post endpoints live under /auth/* — the /account/* URLs are Blazor pages
        // and would create ambiguous route matches for POST requests.
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (
            HttpContext http,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            AuditService audit) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();
            if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/')) returnUrl = "/";

            var user = await userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    await audit.LogAsync("Auth", $"Login: {user.DisplayName}", user.Id, user.DisplayName);
                    return Results.LocalRedirect(returnUrl);
                }
            }
            return Results.LocalRedirect($"/account/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
        });

        group.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.LocalRedirect("/account/login");
        });

        group.MapPost("/register", async (
            HttpContext http,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AuditService audit) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var displayName = form["displayName"].ToString();
            var password = form["password"].ToString();
            var confirm = form["confirmPassword"].ToString();

            if (password != confirm)
                return Results.LocalRedirect("/account/register?error=Password+tidak+sama");

            var colors = new[] { "#1877f2", "#31a24c", "#f7b928", "#f02849", "#9360f7", "#0ea5e9" };
            var user = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName,
                AvatarColor = colors[Random.Shared.Next(colors.Length)]
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                return Results.LocalRedirect($"/account/register?error={Uri.EscapeDataString(error)}");
            }
            await userManager.AddToRoleAsync(user, "Viewer"); // new users start as Viewer
            await signInManager.SignInAsync(user, isPersistent: true);
            await audit.LogAsync("Auth", $"Registrasi user baru: {user.DisplayName}", user.Id, user.DisplayName);
            return Results.LocalRedirect("/");
        });

        group.MapPost("/forgot", async (HttpContext http, UserManager<ApplicationUser> userManager) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                return Results.LocalRedirect("/account/forgot?sent=1"); // do not reveal existence

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            // Demo mode (no SMTP configured): surface the reset link directly on screen.
            var link = $"/account/reset?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            return Results.LocalRedirect($"/account/forgot?sent=1&link={Uri.EscapeDataString(link)}");
        });

        group.MapPost("/reset", async (HttpContext http, UserManager<ApplicationUser> userManager) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var token = form["token"].ToString();
            var password = form["password"].ToString();

            var user = await userManager.FindByEmailAsync(email);
            if (user is null) return Results.LocalRedirect("/account/login");
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                return Results.LocalRedirect(
                    $"/account/reset?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}&error={Uri.EscapeDataString(error)}");
            }
            return Results.LocalRedirect("/account/login?reset=1");
        });
    }
}
