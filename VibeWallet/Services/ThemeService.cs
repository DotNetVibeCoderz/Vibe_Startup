using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace VibeWallet.Services;

/// <summary>
/// Scoped service untuk sinkronisasi tema Dark/Light antar komponen.
/// 
/// SettingsPage mengubah tema via SetThemeAsync() →
/// ThemeService mentrigger event OnThemeChanged →
/// MainLayout bereaksi dan re-render dengan tema baru.
/// </summary>
public class ThemeService
{
    private readonly ProtectedSessionStorage _session;
    private readonly ILogger<ThemeService> _logger;

    /// <summary>Event yang di-subscribe MainLayout untuk re-render saat tema berubah</summary>
    public event Func<Task>? OnThemeChanged;

    /// <summary>Tema saat ini (light / dark)</summary>
    public string CurrentTheme { get; private set; } = "light";

    /// <summary>True = dark mode aktif</summary>
    public bool IsDark => CurrentTheme == "dark";

    public ThemeService(ProtectedSessionStorage session, ILogger<ThemeService> logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>Load tema dari session storage (dipanggil oleh MainLayout saat inisialisasi)</summary>
    public async Task LoadThemeAsync()
    {
        try
        {
            var stored = await _session.GetAsync<string>("theme");
            CurrentTheme = stored is { Success: true, Value: not null } ? stored.Value : "light";
        }
        catch { CurrentTheme = "light"; }
    }

    /// <summary>Set tema baru. Dipanggil dari SettingsPage atau toggle di sidebar.</summary>
    public async Task SetThemeAsync(string theme)
    {
        if (CurrentTheme == theme) return;

        CurrentTheme = theme;
        await _session.SetAsync("theme", theme);
        _logger.LogInformation("Theme changed to: {Theme}", theme);

        // 🔥 Trigger re-render di MainLayout!
        if (OnThemeChanged != null)
            await OnThemeChanged.Invoke();
    }

    /// <summary>Toggle light ↔ dark</summary>
    public async Task ToggleAsync()
    {
        await SetThemeAsync(IsDark ? "light" : "dark");
    }
}
