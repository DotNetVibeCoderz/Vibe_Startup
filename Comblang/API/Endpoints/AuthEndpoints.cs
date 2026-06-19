using Comblang.Data;
using Comblang.Models;
using Comblang.Services.Auth;
using Comblang.Services.Matching;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Comblang.API.Endpoints;

/// <summary>
/// Authentication API endpoints: login, register, and token validation.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var authGroup = app.MapGroup("/api/auth");

        // POST /api/auth/register
        authGroup.MapPost("/register", async (RegisterRequest req, AuthService authService, AppDbContext db) =>
        {
            var (user, error) = await authService.RegisterAsync(req.Email, req.Username, req.Password);
            if (error != null)
                return Results.BadRequest(new { error });

            return Results.Ok(new
            {
                user!.Id,
                user.Email,
                user.Username,
                message = "Registrasi berhasil! Silakan lengkapi profil kamu."
            });
        }).AddApiKeyAuth();

        // POST /api/auth/login
        authGroup.MapPost("/login", async (LoginRequest req, AuthService authService) =>
        {
            var (token, user, error) = await authService.LoginAsync(req.Email, req.Password);
            if (error != null)
                return Results.BadRequest(new { error });

            return Results.Ok(new
            {
                token,
                user = new
                {
                    user!.Id,
                    user.Email,
                    user.Username,
                    user.Role,
                    user.IsPremium
                }
            });
        }).AddApiKeyAuth();

        // POST /api/auth/cookie-login
        authGroup.MapPost("/cookie-login", async (HttpContext ctx, LoginRequest req, AuthService authService) =>
        {
            var (token, user, error) = await authService.LoginAsync(req.Email, req.Password);
            if (error != null)
                return Results.BadRequest(new { error });

            var principal = AuthService.CreateClaimsPrincipal(user!);
            await ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddDays(7)
                });

            return Results.Ok(new
            {
                message = "Login berhasil!",
                user = new
                {
                    user!.Id,
                    user.Email,
                    user.Username,
                    user.Role,
                    user.IsPremium
                }
            });
        });

        // POST /api/auth/cookie-register
        authGroup.MapPost("/cookie-register", async (HttpContext ctx, RegisterRequest req, AuthService authService) =>
        {
            var (user, error) = await authService.RegisterAsync(req.Email, req.Username, req.Password);
            if (error != null)
                return Results.BadRequest(new { error });

            var principal = AuthService.CreateClaimsPrincipal(user!);
            await ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddDays(7)
                });

            return Results.Ok(new
            {
                message = "Registrasi berhasil! Mengalihkan...",
                user = new
                {
                    user!.Id,
                    user.Email,
                    user.Username,
                    user.Role,
                    user.IsPremium
                }
            });
        });

        // GET /api/auth/validate
        authGroup.MapGet("/validate", (HttpContext ctx) =>
        {
            var isAuth = ctx.User.Identity?.IsAuthenticated ?? false;
            return Results.Ok(new { isAuthenticated = isAuth, username = ctx.User.Identity?.Name });
        });
    }
}

public record RegisterRequest(string Email, string Username, string Password);
public record LoginRequest(string Email, string Password);
