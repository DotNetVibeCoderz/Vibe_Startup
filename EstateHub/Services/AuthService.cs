using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using EstateHub.Data;
using EstateHub.Models;

namespace EstateHub.Services;

/// <summary>
/// Authentication service
/// </summary>
public class AuthService
{
    private readonly AppDbContext _db;
    public AuthService(AppDbContext db) => _db = db;

    public async Task<ApplicationUser?> LoginAsync(string email, string password)
    {
        var input = email.Trim().ToLower();
        var demoMap = new Dictionary<string, (string Id, string Name, string Role)>
        {
            ["admin"]  = ("admin-001",  "Admin EstateHub",    "Admin"),
            ["agent"]  = ("agent-001",  "Budi Agen Properti", "Agent"),
            ["buyer"]  = ("buyer-001",  "Siti Pembeli",       "Buyer"),
            ["tenant"] = ("tenant-001", "Rudi Penyewa",       "Tenant"),
        };
        if (demoMap.TryGetValue(input, out var demo))
        {
            var existing = await _db.Users.FindAsync(demo.Id);
            if (existing != null) return existing;
            var u = new ApplicationUser { Id = demo.Id, FullName = demo.Name, Role = demo.Role, PhoneNumber = "08123456789", Address = "Indonesia", CreatedAt = DateTime.UtcNow };
            _db.Users.Add(u); await _db.SaveChangesAsync(); return u;
        }
        var byName = await _db.Users.FirstOrDefaultAsync(u => u.FullName.ToLower().Contains(input));
        if (byName != null) return byName;
        if (!string.IsNullOrWhiteSpace(input))
        {
            var role = input.Contains("admin") ? "Admin" : input.Contains("agent") ? "Agent" : input.Contains("tenant") || input.Contains("sewa") ? "Tenant" : "Buyer";
            var u = new ApplicationUser { FullName = char.ToUpper(input[0]) + input[1..], Role = role, PhoneNumber = "-", Address = "-", CreatedAt = DateTime.UtcNow };
            _db.Users.Add(u); await _db.SaveChangesAsync(); return u;
        }
        return null;
    }

    public async Task<ApplicationUser> RegisterAsync(RegisterModel model)
    {
        var u = new ApplicationUser { FullName = model.FullName, PhoneNumber = model.PhoneNumber, Role = model.Role, Address = "", CreatedAt = DateTime.UtcNow };
        _db.Users.Add(u); await _db.SaveChangesAsync(); return u;
    }

    public async Task UpdateProfileAsync(string userId, UserProfileModel model)
    {
        var u = await _db.Users.FindAsync(userId); if (u == null) return;
        u.FullName = model.FullName; u.PhoneNumber = model.PhoneNumber; u.Address = model.Address;
        u.AvatarUrl = model.AvatarUrl; u.PreferredLocation = model.PreferredLocation;
        u.MinBudget = model.MinBudget; u.MaxBudget = model.MaxBudget; u.PreferredType = model.PreferredType;
        u.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync();
    }
}

/// <summary>
/// AuthStateProvider — localStorage persistence + auto‑retry after prerender.
/// Removed _initialized flag so prerender failure doesn't block retry.
/// </summary>
public class EstateHubAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private ApplicationUser? _currentUser;

    public EstateHubAuthStateProvider(IJSRuntime js) => _js = js;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Already restored this circuit — return cached
        if (_currentUser != null)
            return new AuthenticationState(BuildPrincipal(_currentUser));

        // Try localStorage restore (may fail during prerender — that's ok, will retry)
        try
        {
            var json = await _js.InvokeAsync<string>("getAuthFromLocalStorage");
            if (!string.IsNullOrEmpty(json))
            {
                var s = JsonSerializer.Deserialize<StoredAuth>(json);
                if (s != null)
                {
                    _currentUser = new ApplicationUser { Id = s.Id, FullName = s.FullName, Role = s.Role, PhoneNumber = s.Phone };
                    return new AuthenticationState(BuildPrincipal(_currentUser));
                }
            }
        }
        catch
        {
            // JS not ready yet (prerender) — return anonymous, will retry on next call
            // DO NOT cache anything here
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Manually restore from localStorage — call after first render when JS is ready
    /// </summary>
    public async Task<bool> TryRestoreFromStorageAsync()
    {
        if (_currentUser != null) return true; // already restored
        try
        {
            var json = await _js.InvokeAsync<string>("getAuthFromLocalStorage");
            if (!string.IsNullOrEmpty(json))
            {
                var s = JsonSerializer.Deserialize<StoredAuth>(json);
                if (s != null)
                {
                    _currentUser = new ApplicationUser { Id = s.Id, FullName = s.FullName, Role = s.Role, PhoneNumber = s.Phone };
                    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal(_currentUser))));
                    return true;
                }
            }
        }
        catch { /* still not ready */ }
        return false;
    }

    public async Task MarkUserAsAuthenticated(ApplicationUser user)
    {
        _currentUser = user;
        var stored = new StoredAuth { Id = user.Id, FullName = user.FullName, Role = user.Role, Phone = user.PhoneNumber ?? "", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        var json = JsonSerializer.Serialize(stored);
        await _js.InvokeVoidAsync("setAuthToLocalStorage", json);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal(user))));
    }

    public async Task MarkUserAsLoggedOut()
    {
        _currentUser = null;
        await _js.InvokeVoidAsync("clearAuthFromLocalStorage");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    public ApplicationUser? GetCurrentUser() => _currentUser;

    private static ClaimsPrincipal BuildPrincipal(ApplicationUser u)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, u.Id), new(ClaimTypes.Name, u.FullName), new(ClaimTypes.Role, u.Role), new(ClaimTypes.MobilePhone, u.PhoneNumber ?? "") };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "EstateHubAuth"));
    }

    private class StoredAuth
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "Buyer";
        public string Phone { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }
}
