using FuelStation.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FuelStation.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpPost("/account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] string email, [FromForm] string password, [FromForm] bool rememberMe, [FromForm] string? returnUrl)
    {
        var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        return LocalRedirect(returnUrl ?? "/");
    }

    [AllowAnonymous]
    [HttpPost("/account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([FromForm] string fullName, [FromForm] string email, [FromForm] string password,
        [FromForm] string confirmPassword, [FromForm] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            return Redirect($"/register?error={Uri.EscapeDataString("Nama dan email wajib diisi")}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        if (password != confirmPassword)
        {
            return Redirect($"/register?error={Uri.EscapeDataString("Konfirmasi password tidak cocok")}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var message = string.Join(" | ", result.Errors.Select(e => e.Description));
            return Redirect($"/register?error={Uri.EscapeDataString(message)}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        await _userManager.AddToRoleAsync(user, "Operator");
        await _signInManager.SignInAsync(user, isPersistent: true);

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpGet("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/login");
    }
}
