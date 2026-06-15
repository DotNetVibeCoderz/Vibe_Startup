using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace SoccerWizard.Components;

/// <summary>
/// AuthenticationStateProvider untuk Blazor Server.
/// Mengambil auth state dari HttpContext User.
/// </summary>
public class IdentityRevalidatingAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IdentityRevalidatingAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult(new AuthenticationState(httpContext.User));
        }

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
