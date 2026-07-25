using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Resolves the signed-in user for Blazor components.
///
/// Replaces the hardcoded "user id 1" the pages previously used: every page that shows
/// personal data (orders, chat, tickets, loyalty) asks this service instead.
/// </summary>
public class CurrentUserService
{
    private readonly AuthenticationStateProvider _authState;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserService(AuthenticationStateProvider authState, UserManager<ApplicationUser> userManager)
    {
        _authState = authState;
        _userManager = userManager;
    }

    /// <summary>
    /// Raised when the signed-in user edits their own profile. The service is scoped, so this is
    /// one circuit's own event — it lets the layout refresh the sidebar (name, avatar) without the
    /// user having to navigate, and never leaks across users.
    /// </summary>
    public event Action? OnProfileChanged;

    public void NotifyProfileChanged() => OnProfileChanged?.Invoke();

    public async Task<ClaimsPrincipal> GetPrincipalAsync() =>
        (await _authState.GetAuthenticationStateAsync()).User;

    public async Task<bool> IsAuthenticatedAsync() =>
        (await GetPrincipalAsync()).Identity?.IsAuthenticated == true;

    /// <summary>User id, or null when nobody is signed in.</summary>
    public async Task<long?> GetUserIdAsync()
    {
        var principal = await GetPrincipalAsync();
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : null;
    }

    public async Task<ApplicationUser?> GetUserAsync()
    {
        var principal = await GetPrincipalAsync();
        if (principal.Identity?.IsAuthenticated != true) return null;
        return await _userManager.GetUserAsync(principal);
    }

    public async Task<string> GetDisplayNameAsync()
    {
        var user = await GetUserAsync();
        return user?.FullName ?? user?.Email ?? "Tamu";
    }

    public async Task<bool> IsInRoleAsync(params string[] roles)
    {
        var principal = await GetPrincipalAsync();
        return roles.Any(principal.IsInRole);
    }

    /// <summary>True for Admin, Manager, or WarehouseStaff — the operational back-office roles.</summary>
    public async Task<bool> IsStaffAsync() => await IsInRoleAsync("Admin", "Manager", "WarehouseStaff");

    public async Task<bool> IsAdminAsync() => await IsInRoleAsync("Admin");
}
