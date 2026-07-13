using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PCHub.Admin.Services;

/// <summary>
/// Custom AuthenticationStateProvider untuk Blazor Server.
/// Menyimpan state login user di circuit tanpa perlu cookie redirect.
/// </summary>
public class PCHubAuthStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private bool _isAuthenticated;

    public bool IsAuthenticated => _isAuthenticated;
    public string? UserName => _currentUser.Identity?.Name;
    public string? UserRole => _currentUser.FindFirst(ClaimTypes.Role)?.Value;
    public string? UserId => _currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? FullName => _currentUser.FindFirst("FullName")?.Value;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    /// <summary>Set user setelah login sukses</summary>
    public void Login(string userId, string username, string email, string role, string fullName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("FullName", fullName)
        };

        var identity = new ClaimsIdentity(claims, "pchub");
        _currentUser = new ClaimsPrincipal(identity);
        _isAuthenticated = true;

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>Clear user saat logout</summary>
    public void Logout()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _isAuthenticated = false;

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
