using System.Collections.ObjectModel;
using System.Windows.Input;
using PCHub.Client.Services;
using PCHub.Shared.DTOs;

namespace PCHub.Client.ViewModels;

/// <summary>ViewModel untuk halaman Dashboard</summary>
public class DashboardViewModel : BaseViewModel
{
    private readonly ApiService _api;

    public DashboardViewModel(ApiService api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(async _ => await LoadDashboardAsync());
    }

    public ICommand RefreshCommand { get; }

    private int _totalUsers;
    public int TotalUsers { get => _totalUsers; set => SetProperty(ref _totalUsers, value); }

    private int _availablePcs;
    public int AvailablePcs { get => _availablePcs; set => SetProperty(ref _availablePcs, value); }

    private decimal _todayRevenue;
    public decimal TodayRevenue { get => _todayRevenue; set => SetProperty(ref _todayRevenue, value); }

    private int _activeSessions;
    public int ActiveSessions { get => _activeSessions; set => SetProperty(ref _activeSessions, value); }

    private string _activeSessionText = "No active session";
    public string ActiveSessionText { get => _activeSessionText; set => SetProperty(ref _activeSessionText, value); }

    private bool _hasActiveSession;
    public bool HasActiveSession { get => _hasActiveSession; set => SetProperty(ref _hasActiveSession, value); }

    private Guid? _activeBillingId;
    public Guid? ActiveBillingId { get => _activeBillingId; set => SetProperty(ref _activeBillingId, value); }

    public ObservableCollection<PopularGameStat> PopularGames { get; } = [];

    public async Task LoadDashboardAsync()
    {
        IsBusy = true;
        try
        {
            var stats = await _api.GetDashboardStatsAsync();
            if (stats != null)
            {
                TotalUsers = stats.TotalUsers;
                AvailablePcs = stats.AvailablePcs;
                TodayRevenue = stats.TodayRevenue;
                ActiveSessions = stats.ActiveSessions;

                PopularGames.Clear();
                foreach (var g in stats.PopularGames.Take(5))
                    PopularGames.Add(g);
            }

            var billing = await _api.GetActiveBillingAsync(App.UserId);
            if (billing != null)
            {
                ActiveSessionText = $"PC: {billing.PcName} | Started: {billing.StartTime:HH:mm} | Rate: Rp {billing.HourlyRate:N0}/hr";
                HasActiveSession = true;
                ActiveBillingId = billing.Id;
            }
            else
            {
                ActiveSessionText = "No active session";
                HasActiveSession = false;
                ActiveBillingId = null;
            }
        }
        catch { StatusMessage = "Offline mode - cannot connect to server"; }
        finally { IsBusy = false; }
    }

    public async Task StopSessionAsync()
    {
        if (ActiveBillingId == null) return;
        await _api.StopBillingAsync(ActiveBillingId.Value);
        HasActiveSession = false;
        ActiveSessionText = "Session ended";
        ActiveBillingId = null;
    }
}
