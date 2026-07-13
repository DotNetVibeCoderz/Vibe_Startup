using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCHub.Client.Services;
using PCHub.Shared.DTOs;

namespace PCHub.Client.Views;

public partial class DashboardPage : UserControl
{
    private readonly ApiService _api;
    private BillingDto? _activeBilling;
    private DateTime _sessionStart;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public DashboardPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (s, e) => await RefreshStatsAsync();
        Loaded += async (s, e) => await LoadDashboard();
    }

    private async Task LoadDashboard()
    {
        TxtLoading.Visibility = Visibility.Visible;
        try
        {
            await RefreshAllAsync();
            _timer.Start();
        }
        catch
        {
            TxtWelcome.Text = "Offline mode - limited data";
        }
        TxtLoading.Visibility = Visibility.Collapsed;
    }

    private async Task RefreshAllAsync()
    {
        // Get dashboard stats
        var stats = await _api.GetDashboardStatsAsync();
        if (stats != null)
        {
            TxtTotalUsers.Text = stats.TotalUsers.ToString();
            TxtAvailablePcs.Text = stats.AvailablePcs.ToString();
            TxtRevenue.Text = $"Rp {stats.TodayRevenue:N0}";
            TxtSessions.Text = stats.ActiveSessions.ToString();

            // Popular games
            PopularGamesList.ItemsSource = stats.PopularGames.Take(5).Select(g => new
            {
                g.GameName,
                g.PlayCount,
                Display = $"🎮 {g.GameName} - {g.PlayCount} plays"
            });
        }

        // Active promos
        try
        {
            var promos = await _api.GetActivePromosAsync();
            PromosList.ItemsSource = promos.Select(p => new
            {
                p.Name,
                p.PromoCode,
                p.DiscountPercentage,
                Display = $"🏷️ {p.Name} - {p.DiscountPercentage}% OFF" + (p.PromoCode != null ? $" [{p.PromoCode}]" : "")
            });
        }
        catch { PromosList.ItemsSource = null; }

        // Check active billing
        var billing = await _api.GetActiveBillingAsync(App.UserId);
        if (billing != null)
        {
            _activeBilling = billing;
            _sessionStart = billing.StartTime;
            TxtActiveSession.Text = $"PC: {billing.PcName} | Rate: Rp {billing.HourlyRate:N0}/hr";
            ActiveSessionPanel.Visibility = Visibility.Visible;
            BtnStopSession.Tag = billing.Id;
            UpdateSessionDuration();
        }
        else
        {
            TxtActiveSession.Text = "No active session";
            ActiveSessionPanel.Visibility = Visibility.Collapsed;
            _activeBilling = null;
        }

        TxtWelcome.Text = $"Welcome back, {App.Username}! 👋";
    }

    private async Task RefreshStatsAsync()
    {
        try
        {
            var stats = await _api.GetDashboardStatsAsync();
            if (stats != null)
            {
                TxtTotalUsers.Text = stats.TotalUsers.ToString();
                TxtAvailablePcs.Text = stats.AvailablePcs.ToString();
                TxtRevenue.Text = $"Rp {stats.TodayRevenue:N0}";
                TxtSessions.Text = stats.ActiveSessions.ToString();
            }
            UpdateSessionDuration();
        }
        catch { /* Silently fail */ }
    }

    private void UpdateSessionDuration()
    {
        if (_activeBilling != null)
        {
            var elapsed = DateTime.UtcNow - _sessionStart;
            var cost = (decimal)elapsed.TotalHours * _activeBilling.HourlyRate;
            TxtSessionDuration.Text = $"Duration: {elapsed:hh\\:mm\\:ss} | Cost: Rp {cost:N0}";
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
    }

    private async void BtnStopSession_Click(object sender, RoutedEventArgs e)
    {
        if (BtnStopSession.Tag is Guid billingId)
        {
            try
            {
                await _api.StopBillingAsync(billingId);
                _activeBilling = null;
                ActiveSessionPanel.Visibility = Visibility.Collapsed;
                TxtActiveSession.Text = "Session ended";
                TxtSessionDuration.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop session: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
