using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using PCHub.Client.Services;

namespace PCHub.Client.Views;

public partial class SettingsPage : UserControl
{
    private readonly ResourceMonitorService? _monitor;

    public SettingsPage()
    {
        InitializeComponent();
        _monitor = App.ResourceMonitor;

        // Load current settings
        ApiUrlBox.Text = App.ApiBaseUrl;
        DarkModeCheck.IsChecked = App.IsDarkMode;
        LanguageBox.SelectedIndex = App.Loc.IsIndonesian ? 0 : 1;

        Loaded += (s, e) =>
        {
            LoadSystemInfo();
            if (_monitor != null)
                _monitor.MetricsUpdated += OnMetricsUpdated;
        };

        Unloaded += (s, e) =>
        {
            if (_monitor != null)
                _monitor.MetricsUpdated -= OnMetricsUpdated;
        };
    }

    private void LoadSystemInfo()
    {
        var info = ResourceMonitorService.GetSystemInfo();
        TxtMachineName.Text = $"Machine: {info.MachineName}";
        TxtOs.Text = $"OS: {info.OSVersion}";
        TxtProcessor.Text = $"CPU Cores: {info.ProcessorCount}";
        TxtMemory.Text = $"Working Set: {info.WorkingSet} MB";
        TxtVersion.Text = $"PCHub Client v1.0.0 | .NET {info.ClrVersion}";
    }

    private void OnMetricsUpdated()
    {
        if (_monitor == null) return;
        Dispatcher.Invoke(() =>
        {
            TxtCpu.Text = $"CPU Usage: {_monitor.CpuUsage:F1}%";
            TxtRam.Text = $"RAM Usage: {_monitor.RamUsagePercent:F1}% (Free: {_monitor.AvailableRamMB:F0} MB)";
        });
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        var url = ApiUrlBox.Text.Trim();
        TxtConnectionStatus.Text = "Testing...";
        TxtConnectionStatus.Foreground = System.Windows.Media.Brushes.Gray;

        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(url + "/dashboard/stats");
            if (response.IsSuccessStatusCode)
            {
                TxtConnectionStatus.Text = "✅ Connected successfully!";
                TxtConnectionStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            }
            else
            {
                TxtConnectionStatus.Text = $"⚠️ Server responded with: {response.StatusCode}";
                TxtConnectionStatus.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
            }
        }
        catch (Exception ex)
        {
            TxtConnectionStatus.Text = $"❌ Connection failed: {ex.Message}";
            TxtConnectionStatus.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
        }
    }

    private void DarkModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        App.IsDarkMode = DarkModeCheck.IsChecked ?? false;
    }

    private void LanguageBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedIndex == 0)
            App.Loc.SetLanguage("id");
        else
            App.Loc.SetLanguage("en");
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        TxtUpdateStatus.Text = "Checking for updates...";
        try
        {
            var updateService = new UpdateService(App.ApiBaseUrl);
            var update = await updateService.CheckForUpdateAsync();
            if (update != null)
            {
                TxtUpdateStatus.Text = $"Update available: v{update.Version}\nReleased: {update.ReleaseDate:dd MMM yyyy}\n{update.ReleaseNotes}";
                var result = MessageBox.Show($"Version {update.Version} is available!\n\n{update.ReleaseNotes}\n\nDownload and install?",
                    "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    await updateService.DownloadAndInstallAsync(update);
                }
            }
            else
            {
                TxtUpdateStatus.Text = "✅ You're running the latest version.";
            }
        }
        catch
        {
            TxtUpdateStatus.Text = "❌ Failed to check for updates. Server may be offline.";
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        App.ApiBaseUrl = ApiUrlBox.Text.Trim();
        App.IsDarkMode = DarkModeCheck.IsChecked ?? false;

        if (LanguageBox.SelectedIndex == 0)
            App.Loc.SetLanguage("id");
        else
            App.Loc.SetLanguage("en");

        App.SaveSettings();

        MessageBox.Show("Settings saved!", "PCHub", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
