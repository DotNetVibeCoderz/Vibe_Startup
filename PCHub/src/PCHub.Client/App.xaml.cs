using System.IO;
using System.Windows;
using System.Windows.Media;
using PCHub.Client.Services;

namespace PCHub.Client;

public partial class App : Application
{
    private static bool _isDarkMode = false;
    public static bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            _isDarkMode = value;
            ApplyTheme();
        }
    }

    public static string? AuthToken { get; set; }
    public static Guid UserId { get; set; }
    public static string Username { get; set; } = "";
    public static string ApiBaseUrl { get; set; } = "https://localhost:5001/api";
    public static LocalizationService Loc => LocalizationService.Instance;

    // Global services
    public static ScreenLockService? ScreenLock { get; set; }
    public static ResourceMonitorService? ResourceMonitor { get; set; }
    public static NotificationPopupService? NotificationService { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize services
        ScreenLock = new ScreenLockService();
        ResourceMonitor = new ResourceMonitorService();

        // Load saved settings
        LoadSettings();

        ApplyTheme();
    }

    /// <summary>Load settings dari App.config sederhana</summary>
    private static void LoadSettings()
    {
        try
        {
            var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config");
            if (System.IO.File.Exists(configPath))
            {
                var lines = System.IO.File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "ApiBaseUrl": ApiBaseUrl = value; break;
                            case "DarkMode": IsDarkMode = value == "true"; break;
                            case "Language": Loc.SetLanguage(value); break;
                        }
                    }
                }
            }
        }
        catch { /* Use defaults */ }
    }

    /// <summary>Simpan settings ke App.config</summary>
    public static void SaveSettings()
    {
        try
        {
            var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config");
            var lines = new[]
            {
                $"ApiBaseUrl={ApiBaseUrl}",
                $"DarkMode={IsDarkMode}",
                $"Language={Loc.CurrentLanguage}"
            };
            System.IO.File.WriteAllLines(configPath, lines);
        }
        catch { /* Silently ignore */ }
    }

    /// <summary>Toggle tema dark/light</summary>
    public static void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    /// <summary>Terapkan tema ke seluruh aplikasi</summary>
    private static void ApplyTheme()
    {
        if (Current == null) return;

        var isDark = IsDarkMode;

        // Update semua resource brush
        var resources = Current.Resources;

        resources["BgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x0F, 0x17, 0x2A) : Color.FromRgb(0xF0, 0xF2, 0xF5));
        resources["SurfaceBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x29, 0x3B) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        resources["TextBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0xE2, 0xE8, 0xF0) : Color.FromRgb(0x1A, 0x1A, 0x2E));
        resources["TextSecondaryBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x94, 0xA3, 0xB8) : Color.FromRgb(0x65, 0x67, 0x6B));
        resources["BorderBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x47, 0x55, 0x69) : Color.FromRgb(0x1A, 0x1A, 0x2E));
        resources["CardBorderBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x47, 0x55, 0x69) : Color.FromRgb(0x1A, 0x1A, 0x2E));
        resources["InputBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x0F, 0x17, 0x2A) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        resources["TimerBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x29, 0x3B) : Color.FromRgb(0xEE, 0xF2, 0xFF));
        resources["ChatUserBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x25, 0x63, 0xEB) : Color.FromRgb(0x25, 0x63, 0xEB));
        resources["ChatBotBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x29, 0x3B) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        resources["SidebarBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x0B, 0x0F, 0x19) : Color.FromRgb(0x1E, 0x29, 0x3B));
        resources["SidebarTextBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0xE2, 0xE8, 0xF0) : Color.FromRgb(0xE2, 0xE8, 0xF0));
        resources["SidebarHoverBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x40, 0xAF) : Color.FromRgb(0x33, 0x41, 0x55));
        resources["OverlayBrush"] = new SolidColorBrush(isDark ? Color.FromArgb(0xCC, 0x00, 0x00, 0x00) : Color.FromArgb(0x80, 0x00, 0x00, 0x00));
    }
}
