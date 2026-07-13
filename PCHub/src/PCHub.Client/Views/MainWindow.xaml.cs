using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Icon = System.Drawing.Icon;
using Point = System.Windows.Point;

namespace PCHub.Client.Views;

public partial class MainWindow : Window
{
    private LoginPage? _loginPage;
    private ClientShell? _clientShell;
    private TrayIcon? _tray;

    public MainWindow()
    {
        InitializeComponent();
        ShowLogin();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _tray = new TrayIcon(this);
    }

    public static bool RequestPasskey(string reason)
    {
        var d = new Window { Title = $"🔒 {reason}", Width = 360, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterScreen, WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize, Topmost = true };
        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new TextBlock { Text = "Enter passkey:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        var pw = new PasswordBox { Margin = new Thickness(0, 0, 0, 12), FontSize = 16 }; sp.Children.Add(pw);
        var err = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), FontSize = 12, Margin = new Thickness(0, 0, 0, 8) }; sp.Children.Add(err);
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cb = new Button { Content = "Cancel", Width = 80, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)), BorderThickness = new Thickness(2), FontWeight = FontWeights.SemiBold };
        cb.Click += (_, _) => d.Close(); bp.Children.Add(cb);
        var ob = new Button { Content = "OK", Width = 80, Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)), BorderThickness = new Thickness(2), FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0) };
        ob.Click += (_, _) => { if (pw.Password == App.Passkey) { d.Tag = true; d.Close(); } else { err.Text = "❌ Invalid passkey!"; pw.Password = ""; } };
        bp.Children.Add(ob); sp.Children.Add(bp); d.Content = sp; d.ShowDialog();
        return d.Tag is true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { if (RequestPasskey("Exit Application")) ForceClose(); e.Handled = true; } }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            ShowInTaskbar = true;
            
            //Hide(); 
            //_tray?.ShowBalloon("PCHub", "Minimized to system tray. Double-click to restore.");
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e) { e.Cancel = true; if (RequestPasskey("Close Application")) ForceClose(); }

    private void ForceClose() { App.ResourceMonitor?.Stop(); App.NotificationService?.StopPolling(); App.ScreenLock?.Unlock(); _tray?.Dispose(); _tray = null; Application.Current.Shutdown(); }

    internal void RestoreFromTray() { ShowInTaskbar = true; Show(); WindowState = WindowState.Maximized; Activate(); }

    public void ShowLogin() { App.IsLoggedIn = false; _loginPage = new LoginPage(); _loginPage.LoginSucceeded += OnLoginSucceeded; MainContent.Content = _loginPage; }
    public void ShowClientShell()
    {
        App.IsLoggedIn = true; _clientShell = new ClientShell(); _clientShell.LogoutRequested += OnLogoutRequested; MainContent.Content = _clientShell;
        App.ResourceMonitor?.Start();
        if (App.NotificationService == null && App.AuthToken != null) { var a = new Services.ApiService(App.ApiBaseUrl); a.SetToken(App.AuthToken); App.NotificationService = new Services.NotificationPopupService(a); }
        App.NotificationService?.StartPolling();
        if (App.ScreenLock != null) App.ScreenLock.SessionExpiring += (_, _) => Dispatcher.Invoke(() => App.NotificationService?.ShowLocal("⚠️ Session Expiring", "5 menit lagi!", Services.NotificationType.Warning));
    }
    private void OnLoginSucceeded(object? _, EventArgs __) => Dispatcher.Invoke(() => ShowClientShell());
    private void OnLogoutRequested(object? _, EventArgs __) { App.ResourceMonitor?.Stop(); App.NotificationService?.StopPolling(); App.ScreenLock?.StopMonitoring(); App.ScreenLock?.Unlock(); App.AuthToken = null; App.UserId = Guid.Empty; App.Username = ""; App.HasActiveSession = false; App.IsLoggedIn = false; Dispatcher.Invoke(() => ShowLogin()); }
    protected override void OnClosed(EventArgs e) { _tray?.Dispose(); base.OnClosed(e); }
}

// ==================================================================
internal class TrayIcon : IDisposable
{
    private readonly MainWindow _owner;
    private readonly IntPtr _hWnd;
    private readonly uint _uid = 100;
    private IntPtr _hIcon;

    public TrayIcon(MainWindow owner)
    {
        _owner = owner;
        _hWnd = new WindowInteropHelper(owner).Handle;
        _hIcon = GetAppIcon();
        AddToTray();
        HwndSource.FromHwnd(_hWnd)?.AddHook(WndProc);
    }

    private static IntPtr GetAppIcon()
    {
        try { var icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? ""); return icon?.Handle ?? IntPtr.Zero; }
        catch { return LoadIcon(IntPtr.Zero, 32512); }
    }

    private void AddToTray()
    {
        var nid = new NOTIFYICONDATA { cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hWnd, uID = _uid, uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP, uCallbackMessage = WM_TRAYICON, hIcon = _hIcon, szTip = "PCHub Game Center" };
        Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    public void ShowBalloon(string title, string text)
    {
        var nid = new NOTIFYICONDATA { cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hWnd, uID = _uid, uFlags = NIF_INFO, szInfoTitle = title, szInfo = text, dwInfoFlags = NIIF_INFO, uTimeoutOrVersion = 3000 };
        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON && lParam == WM_LBUTTONDBLCLK) { _owner.Dispatcher.Invoke(() => _owner.RestoreFromTray()); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        var nid = new NOTIFYICONDATA { cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hWnd, uID = _uid };
        Shell_NotifyIcon(NIM_DELETE, ref nid);
        if (_hIcon != IntPtr.Zero) { DestroyIcon(_hIcon); _hIcon = IntPtr.Zero; }
    }

    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIF_ICON = 2, NIF_MESSAGE = 1, NIF_TIP = 4, NIF_INFO = 0x10, NIIF_INFO = 1;
    private const int WM_TRAYICON = 0x8001, WM_LBUTTONDBLCLK = 0x0203;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint m, ref NOTIFYICONDATA d);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    private struct NOTIFYICONDATA { public uint cbSize; public IntPtr hWnd; public uint uID; public uint uFlags; public uint uCallbackMessage; public IntPtr hIcon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip; public uint dwState; public uint dwStateMask; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo; public uint uTimeoutOrVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle; public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon; }
}
