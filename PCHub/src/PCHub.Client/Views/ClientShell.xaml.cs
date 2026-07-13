using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCHub.Client.Services;

namespace PCHub.Client.Views;

public partial class ClientShell : UserControl
{
    public event EventHandler? LogoutRequested;
    private readonly ApiService _api;
    private readonly Dictionary<string, UserControl> _pages = [];
    private readonly Dictionary<string, Button> _navButtons = [];

    public ClientShell()
    {
        InitializeComponent();
        _api = new ApiService(App.ApiBaseUrl);
        if (App.AuthToken != null) _api.SetToken(App.AuthToken);

        _pages["Dashboard"] = new DashboardPage(_api);
        _pages["Games"] = new GameLauncherPage(_api);
        _pages["Billing"] = new BillingPage(_api);
        _pages["Chat"] = new ChatPage(_api);
        _pages["Reservations"] = new ReservationsPage(_api);
        _pages["Settings"] = new SettingsPage();

        _navButtons["Dashboard"] = BtnDashboard;
        _navButtons["Games"] = BtnGames;
        _navButtons["Billing"] = BtnBilling;
        _navButtons["Chat"] = BtnChat;
        _navButtons["Reservations"] = BtnReservations;
        _navButtons["Settings"] = BtnSettings;

        TxtTitleUser.Text = $" | {App.Username}";
        Navigate("Dashboard");
        ApplyLocalization();
        App.Loc.LanguageChanged += ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        var loc = App.Loc;
        BtnDashboard.Content = loc["nav.dashboard"];
        BtnGames.Content = loc["nav.games"];
        BtnBilling.Content = loc["nav.billing"];
        BtnChat.Content = loc["nav.chat"];
        BtnReservations.Content = loc["nav.reservations"];
        BtnSettings.Content = loc["nav.settings"];
    }

    /// <summary>Minimize ke system tray</summary>
    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        // Cari parent Window untuk minimize
        var window = Window.GetWindow(this);
        if (window != null)
            window.WindowState = WindowState.Minimized;
    }

    /// <summary>Drag window saat drag title bar</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null && e.ClickCount == 1)
            window.DragMove();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            Navigate(tag);
    }

    private void Navigate(string page)
    {
        if (_pages.TryGetValue(page, out var control))
            PageContent.Content = control;

        foreach (var kvp in _navButtons)
        {
            kvp.Value.Background = kvp.Key == page
                ? new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))
                : new SolidColorBrush(Colors.Transparent);
            kvp.Value.Foreground = kvp.Key == page
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }
}
