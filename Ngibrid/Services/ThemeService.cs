namespace Ngibrid.Services;

/// <summary>
/// Per-user UI preferences (dark/light mode).
///
/// Registered scoped, not singleton: a Blazor Server scope is one user's circuit, so a singleton
/// would make one visitor's theme toggle change the theme for everyone connected.
/// The choice is persisted to localStorage by the JS helper and restored on the next visit.
/// </summary>
public class ThemeService
{
    public bool IsDarkMode { get; private set; }

    public event Action? OnThemeChanged;

    public string ThemeName => IsDarkMode ? "dark" : "light";

    /// <summary>CSS class applied to the layout root, kept for the class-based selectors in ngibrid.css.</summary>
    public string ThemeClass => IsDarkMode ? "dark-theme" : "light-theme";

    public void ToggleTheme() => SetTheme(!IsDarkMode);

    public void SetTheme(bool isDark)
    {
        if (IsDarkMode == isDark) return;
        IsDarkMode = isDark;
        OnThemeChanged?.Invoke();
    }

    /// <summary>Apply a theme restored from localStorage without re-notifying if unchanged.</summary>
    public void InitializeFrom(string? storedTheme)
    {
        var isDark = string.Equals(storedTheme, "dark", StringComparison.OrdinalIgnoreCase);
        if (IsDarkMode == isDark) return;
        IsDarkMode = isDark;
        OnThemeChanged?.Invoke();
    }
}
