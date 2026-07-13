using System.IO;
using System.Windows;
using System.Windows.Media;
using PCHub.Client.Services;

namespace PCHub.Client;

public partial class App : Application
{
    private static bool _isDarkMode = false;
    public static bool IsDarkMode { get => _isDarkMode; set { _isDarkMode = value; ApplyTheme(); } }

    public static string? AuthToken { get; set; }
    public static Guid UserId { get; set; }
    public static string Username { get; set; } = "";
    public static string ApiBaseUrl { get; set; } = "https://localhost:5001/api";
    public static string Passkey { get; set; } = "123qweasd";
    public static bool IsLoggedIn { get; set; } = false;
    public static bool HasActiveSession { get; set; } = false;
    public static LocalizationService Loc => LocalizationService.Instance;
    public static ScreenLockService? ScreenLock { get; set; }
    public static ResourceMonitorService? ResourceMonitor { get; set; }
    public static NotificationPopupService? NotificationService { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ScreenLock = new ScreenLockService();
        ResourceMonitor = new ResourceMonitorService();
        LoadSettings();
        ApplyTheme();
    }

    private static void LoadSettings()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config");
            if (System.IO.File.Exists(path))
            {
                foreach (var line in System.IO.File.ReadAllLines(path))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        switch (parts[0].Trim())
                        {
                            case "ApiBaseUrl": ApiBaseUrl = parts[1].Trim(); break;
                            case "DarkMode": IsDarkMode = parts[1].Trim() == "true"; break;
                            case "Language": Loc.SetLanguage(parts[1].Trim()); break;
                            case "Passkey": Passkey = parts[1].Trim(); break;
                        }
                    }
                }
            }
        }
        catch { }
    }

    public static void SaveSettings()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config");
            System.IO.File.WriteAllLines(path, new[] { $"ApiBaseUrl={ApiBaseUrl}", $"DarkMode={IsDarkMode}", $"Language={Loc.CurrentLanguage}", $"Passkey={Passkey}" });
        }
        catch { }
    }

    public static void ToggleTheme() => IsDarkMode = !IsDarkMode;

    private static void ApplyTheme()
    {
        if (Current == null) return;
        var isDark = IsDarkMode;
        var r = Current.Resources;
        r["BgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x0F, 0x17, 0x2A) : Color.FromRgb(0xF0, 0xF2, 0xF5));
        r["SurfaceBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x29, 0x3B) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        r["TextBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0xE2, 0xE8, 0xF0) : Color.FromRgb(0x1A, 0x1A, 0x2E));
        r["TextSecondaryBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x94, 0xA3, 0xB8) : Color.FromRgb(0x65, 0x67, 0x6B));
        r["BorderBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x47, 0x55, 0x69) : Color.FromRgb(0x1A, 0x1A, 0x2E));
        r["CardBorderBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x47, 0x55, 0x69) : Color.FromRgb(0x1A, 0x1A, 0x2E));
        r["InputBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x0F, 0x17, 0x2A) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        r["TimerBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x29, 0x3B) : Color.FromRgb(0xEE, 0xF2, 0xFF));
        r["ChatUserBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x25, 0x63, 0xEB) : Color.FromRgb(0x25, 0x63, 0xEB));
        r["ChatBotBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x29, 0x3B) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        r["SidebarBgBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x0B, 0x0F, 0x19) : Color.FromRgb(0x1E, 0x29, 0x3B));
        r["SidebarTextBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0xE2, 0xE8, 0xF0) : Color.FromRgb(0xE2, 0xE8, 0xF0));
        r["SidebarHoverBrush"] = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x40, 0xAF) : Color.FromRgb(0x33, 0x41, 0x55));
        r["OverlayBrush"] = new SolidColorBrush(isDark ? Color.FromArgb(0xCC, 0x00, 0x00, 0x00) : Color.FromArgb(0x80, 0x00, 0x00, 0x00));
    }
}
