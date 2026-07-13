using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCHub.Client.Services;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;

namespace PCHub.Client.Views;

public partial class BillingPage : UserControl
{
    private readonly ApiService _api;
    private BillingDto? _activeBilling;
    private DateTime _sessionStart;
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private decimal _hourlyRate = 8000;
    private List<PcDto> _availablePcs = [];
    private PcDto? _selectedPc;

    public BillingPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        Loaded += async (s, e) => await LoadBilling();
    }

    private async Task LoadBilling()
    {
        try
        {
            _availablePcs = (await _api.GetPcsAsync()).Where(p => p.Status == PcStatus.Available).ToList();
            if (_availablePcs.Any()) { _selectedPc = _availablePcs.First(); TxtSelectedPc.Text = _selectedPc.Name; }
            _activeBilling = await _api.GetActiveBillingAsync(App.UserId);
            if (_activeBilling != null) { StartTimer(_activeBilling.StartTime, _activeBilling.HourlyRate); App.HasActiveSession = true; }
            await LoadHistory();
        }
        catch { }
    }

    private async Task LoadHistory()
    {
        try
        {
            var history = await _api.GetBillingHistoryAsync(App.UserId);
            TxtHistoryCount.Text = $"{history.Count} sessions";
            if (history.Any()) { BillingHistoryList.ItemsSource = history.OrderByDescending(b => b.StartTime).Take(10); TxtNoHistory.Visibility = Visibility.Collapsed; }
            else TxtNoHistory.Visibility = Visibility.Visible;
        }
        catch { TxtNoHistory.Text = "Cannot load history (offline)"; TxtNoHistory.Visibility = Visibility.Visible; }
    }

    private void StartTimer(DateTime startTime, decimal rate)
    {
        _sessionStart = startTime; _hourlyRate = rate;
        BtnEndSession.Visibility = Visibility.Visible; BtnStartDemo.Visibility = Visibility.Collapsed;
        TxtPcInfo.Text = $"PC session active | Rate: Rp {rate:N0}/hr";
        App.HasActiveSession = true;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.UtcNow - _sessionStart;
        TxtTimer.Text = elapsed.ToString(@"hh\:mm\:ss");
        var cost = (decimal)elapsed.TotalHours * _hourlyRate;
        TxtCost.Text = $"Rp {cost:N0}";
        var remaining = TimeSpan.FromHours(1) - elapsed;
        if (remaining.TotalMinutes <= 5 && remaining.TotalMinutes > 0)
            TxtPcInfo.Text = $"⚠️ Session expiring in {remaining.Minutes}m {remaining.Seconds}s | Rate: Rp {_hourlyRate:N0}/hr";
    }

    private async void BtnEndSession_Click(object sender, RoutedEventArgs e)
    {
        if (_activeBilling != null)
        {
            try
            {
                await _api.StopBillingAsync(_activeBilling.Id);
                _timer.Stop(); TxtTimer.Text = "00:00:00"; TxtCost.Text = "Rp 0"; TxtPcInfo.Text = "Session ended";
                BtnEndSession.Visibility = Visibility.Collapsed; BtnStartDemo.Visibility = Visibility.Visible;
                _activeBilling = null; App.HasActiveSession = false;
                await LoadHistory();
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    private async void BtnStartDemo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedPc == null) { MessageBox.Show("No PC selected.", "Info", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            _activeBilling = await _api.StartBillingAsync(App.UserId, _selectedPc.Id);
            if (_activeBilling != null) { StartTimer(_activeBilling.StartTime, _activeBilling.HourlyRate); App.ScreenLock?.SetSessionDuration(TimeSpan.FromHours(1)); }
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadBilling();

    private async void SelectPc_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_availablePcs.Any()) { await LoadBilling(); if (!_availablePcs.Any()) { MessageBox.Show("No PCs available."); return; } }
        var idx = _selectedPc != null ? _availablePcs.FindIndex(p => p.Id == _selectedPc.Id) : -1;
        _selectedPc = _availablePcs[(idx + 1) % _availablePcs.Count];
        TxtSelectedPc.Text = $"{_selectedPc.Name} (Rp {_selectedPc.HourlyRate:N0}/hr)";
    }

    private void ApplyPromo_Click(object sender, MouseButtonEventArgs e)
    {
        var d = new Window { Title = "Apply Promo Code", Width = 300, Height = 160, WindowStartupLocation = WindowStartupLocation.CenterScreen, WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock { Text = "Enter promo code:", Margin = new Thickness(0, 0, 0, 8) });
        var input = new TextBox { Margin = new Thickness(0, 0, 0, 12) }; sp.Children.Add(input);
        var btn = new Button { Content = "Apply", Style = (Style)FindResource("PrimaryButton") };
        btn.Click += (s, args) => { if (!string.IsNullOrWhiteSpace(input.Text)) TxtPromoStatus.Text = $"Code: {input.Text.Trim()} (pending validation)"; d.Close(); };
        sp.Children.Add(btn); d.Content = sp; d.ShowDialog();
    }

    private void ViewHistory_Click(object sender, MouseButtonEventArgs e) => _ = LoadHistory();
}
