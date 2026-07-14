using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace BlazePoint.Components.Shared;

/// <summary>Base component exposing the current user's id/name/roles.</summary>
public abstract class BpComponentBase : ComponentBase
{
    [Inject] protected AuthenticationStateProvider AuthProvider { get; set; } = default!;

    protected string? UserId { get; private set; }
    protected string UserName { get; private set; } = "";
    protected bool IsAdmin { get; private set; }
    protected bool IsEditor { get; private set; } // Editor OR Admin

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthProvider.GetAuthenticationStateAsync();
        var user = auth.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            UserName = user.Identity.Name ?? "";
            IsAdmin = user.IsInRole("Admin");
            IsEditor = IsAdmin || user.IsInRole("Editor");
        }
        await OnInitializedCoreAsync();
    }

    /// <summary>Override this instead of OnInitializedAsync so user info is already available.</summary>
    protected virtual Task OnInitializedCoreAsync() => Task.CompletedTask;
}
