using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FastRide.AdminWeb.Services;

/// <summary>
/// Holds the signed-in admin for a Blazor circuit.
///
/// The dashboard previously called the API anonymously; now that every endpoint requires a
/// token, the console has to sign in like any other client. The token is kept in protected
/// session storage so a page refresh does not sign the operator out, and it is scoped to the
/// circuit so two browser tabs never share credentials.
/// </summary>
public sealed class AdminSession(ProtectedSessionStorage storage, ILogger<AdminSession> logger)
{
    private const string StorageKey = "fastride.admin.session";

    private bool _restored;

    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? PhotoUrl { get; private set; }
    public string? Token { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token) && ExpiresAt > DateTime.UtcNow;

    public event Action? Changed;

    public void Apply(AuthResponse auth)
    {
        UserId = auth.UserId;
        FullName = auth.FullName;
        Email = auth.Email;
        PhotoUrl = auth.PhotoUrl;
        Token = auth.Token;
        ExpiresAt = auth.ExpiresAt;

        Changed?.Invoke();
    }

    public void Clear()
    {
        UserId = default;
        FullName = string.Empty;
        Email = string.Empty;
        PhotoUrl = null;
        Token = null;
        ExpiresAt = default;

        Changed?.Invoke();
    }

    public async Task PersistAsync()
    {
        if (Token is null) return;

        await storage.SetAsync(StorageKey, new StoredSession(UserId, FullName, Email, PhotoUrl, Token, ExpiresAt));
    }

    public async Task ForgetAsync()
    {
        Clear();
        await storage.DeleteAsync(StorageKey);
    }

    /// <summary>
    /// Reload the session after a refresh. Must run after the first render — protected storage
    /// needs JavaScript, which is not available while the circuit is still prerendering.
    /// </summary>
    public async Task<bool> RestoreAsync()
    {
        if (_restored) return IsAuthenticated;
        _restored = true;

        try
        {
            var result = await storage.GetAsync<StoredSession>(StorageKey);
            if (!result.Success || result.Value is null) return false;

            var stored = result.Value;
            if (stored.ExpiresAt <= DateTime.UtcNow)
            {
                await storage.DeleteAsync(StorageKey);
                return false;
            }

            UserId = stored.UserId;
            FullName = stored.FullName;
            Email = stored.Email;
            PhotoUrl = stored.PhotoUrl;
            Token = stored.Token;
            ExpiresAt = stored.ExpiresAt;

            Changed?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not restore the admin session; asking for a fresh sign-in.");
            return false;
        }
    }

    private sealed record StoredSession(
        Guid UserId, string FullName, string Email, string? PhotoUrl, string Token, DateTime ExpiresAt);
}

/// <summary>Result of an API call that the UI has to explain to the operator.</summary>
public readonly record struct ApiCallResult<T>(bool Success, T? Value, string? Error)
{
    public static ApiCallResult<T> Ok(T value) => new(true, value, null);
    public static ApiCallResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>Raised when the API rejects the current token, so the shell can send the user back to the sign-in screen.</summary>
public sealed class SessionExpiredException() : Exception("Sesi berakhir. Silakan masuk kembali.");
