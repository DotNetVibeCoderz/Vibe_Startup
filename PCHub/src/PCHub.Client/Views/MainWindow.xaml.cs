using System.Windows;
using PCHub.Client.Services;
using PCHub.Client.Views;

namespace PCHub.Client.Views;

public partial class MainWindow : Window
{
    private LoginPage? _loginPage;
    private ClientShell? _clientShell;

    public MainWindow()
    {
        InitializeComponent();
        ShowLogin();
    }

    public void ShowLogin()
    {
        _loginPage = new LoginPage();
        _loginPage.LoginSucceeded += OnLoginSucceeded;
        MainContent.Content = _loginPage;
        Title = "PCHub Game Center - Login";
    }

    public void ShowClientShell()
    {
        _clientShell = new ClientShell();
        _clientShell.LogoutRequested += OnLogoutRequested;
        MainContent.Content = _clientShell;
        Title = $"PCHub Game Center - {App.Username}";

        // Start services
        App.ResourceMonitor?.Start();

        // Start notification service
        if (App.NotificationService == null && App.AuthToken != null)
        {
            var api = new ApiService(App.ApiBaseUrl);
            api.SetToken(App.AuthToken);
            App.NotificationService = new NotificationPopupService(api);
        }
        App.NotificationService?.StartPolling();

        // Setup screen lock handler
        if (App.ScreenLock != null)
        {
            App.ScreenLock.SessionExpiring += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    App.NotificationService?.ShowLocal(
                        "⚠️ Session Expiring",
                        "Your session will end in 5 minutes. Please extend at the counter.",
                        NotificationType.Warning);
                });
            };
        }
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => ShowClientShell());
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        App.ResourceMonitor?.Stop();
        App.NotificationService?.StopPolling();
        App.ScreenLock?.StopMonitoring();
        App.ScreenLock?.Unlock();

        App.AuthToken = null;
        App.UserId = Guid.Empty;
        App.Username = "";
        Dispatcher.Invoke(() => ShowLogin());
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        App.ResourceMonitor?.Stop();
        App.NotificationService?.StopPolling();
        App.ScreenLock?.Unlock();
        base.OnClosing(e);
    }
}
