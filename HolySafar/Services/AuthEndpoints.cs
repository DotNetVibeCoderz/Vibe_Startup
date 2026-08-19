using System.Globalization;
using HolySafar.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;

namespace HolySafar.Api;

/// <summary>
/// Endpoint HTTP untuk hal-hal yang tidak bisa dikerjakan dari dalam circuit Blazor:
/// menulis/menghapus cookie autentikasi (HttpOnly) dan cookie bahasa.
/// Form login/logout mengirim antiforgery token dan divalidasi di sini.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuth(WebApplication app)
    {
        // ==================== LOGIN ====================
        app.MapPost("/auth/login", async (HttpContext ctx, AuthService auth, IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch (AntiforgeryValidationException) { return Results.Redirect("/login?error=" + Uri.EscapeDataString("Sesi kedaluwarsa, silakan coba lagi.")); }

            var form = await ctx.Request.ReadFormAsync();
            var username = form["username"].ToString().Trim();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return Results.Redirect("/login?error=" + Uri.EscapeDataString("Username dan password wajib diisi."));

            var (ok, msg, user) = await auth.ValidateCredentialsAsync(username, password);
            if (!ok || user == null)
                return Results.Redirect("/login?error=" + Uri.EscapeDataString(msg));

            var principal = AuthService.BuildPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = true, IssuedUtc = DateTimeOffset.UtcNow });

            // hanya terima path lokal supaya tidak bisa dipakai open redirect
            var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
                ? returnUrl : "/";
            return Results.Redirect(target);
        }).DisableAntiforgery();   // divalidasi manual di atas

        // ==================== LOGOUT ====================
        app.MapPost("/auth/logout", async (HttpContext ctx, IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch (AntiforgeryValidationException) { return Results.Redirect("/"); }

            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }).DisableAntiforgery();

        // ==================== BAHASA (multi bahasa ID/EN) ====================
        app.MapGet("/set-culture", (HttpContext ctx, string culture, string? redirectUri) =>
        {
            if (culture is not ("id" or "en")) culture = "id";
            var cultureName = culture == "en" ? "en-US" : "id-ID";
            ctx.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(new CultureInfo(cultureName))),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = false, SameSite = SameSiteMode.Lax });

            var target = !string.IsNullOrEmpty(redirectUri) && redirectUri.StartsWith('/') && !redirectUri.StartsWith("//")
                ? redirectUri : "/";
            return Results.Redirect(target);
        });
    }
}
