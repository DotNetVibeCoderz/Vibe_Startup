using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace PCHub.Client.Services;

/// <summary>
/// Service untuk mengelola screen lock otomatis saat sesi habis atau user logout.
/// Menggunakan mekanisme full-screen overlay window untuk mengunci layar.
/// </summary>
public class ScreenLockService
{
    private Window? _lockWindow;
    private readonly DispatcherTimer _checkTimer;
    private DateTime? _sessionEndTime;
    private bool _isLocked;
    private bool _isEnabled;

    public event EventHandler? LockEngaged;
    public event EventHandler? LockDisengaged;
    public event EventHandler? SessionExpiring; // 5 menit sebelum habis

    public bool IsLocked => _isLocked;

    public ScreenLockService()
    {
        _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _checkTimer.Tick += CheckSessionTimer_Tick;
    }

    /// <summary>Mulai monitoring sesi dengan waktu akhir tertentu</summary>
    public void StartMonitoring(DateTime sessionEndTime)
    {
        _sessionEndTime = sessionEndTime;
        _isEnabled = true;
        _checkTimer.Start();
    }

    /// <summary>Hentikan monitoring dan unlock jika terkunci</summary>
    public void StopMonitoring()
    {
        _isEnabled = false;
        _checkTimer.Stop();
        _sessionEndTime = null;
        Unlock();
    }

    /// <summary>Lock layar sekarang (force lock)</summary>
    public void Lock()
    {
        if (_isLocked) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _lockWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                WindowState = WindowState.Maximized,
                Topmost = true,
                ShowInTaskbar = false,
                Background = System.Windows.Media.Brushes.Black,
                Cursor = System.Windows.Input.Cursors.None
            };

            // Overlay pesan
            var grid = new System.Windows.Controls.Grid();
            var messageBlock = new System.Windows.Controls.TextBlock
            {
                Text = "🔒 Session Ended\n\nYour time is up!\nPlease contact the counter to extend your session.",
                FontSize = 28,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center
            };
            grid.Children.Add(messageBlock);
            _lockWindow.Content = grid;

            // Blokir semua input
            _lockWindow.KeyDown += (s, e) => e.Handled = true;

            _lockWindow.Show();
            _isLocked = true;
            LockEngaged?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Unlock layar</summary>
    public void Unlock()
    {
        if (!_isLocked || _lockWindow == null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _lockWindow.Close();
            _lockWindow = null;
            _isLocked = false;
            LockDisengaged?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Set sesi berakhir dalam durasi tertentu</summary>
    public void SetSessionDuration(TimeSpan duration)
    {
        _sessionEndTime = DateTime.UtcNow.Add(duration);
        _isEnabled = true;
        if (!_checkTimer.IsEnabled)
            _checkTimer.Start();
    }

    private void CheckSessionTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isEnabled || _sessionEndTime == null) return;

        var remaining = _sessionEndTime.Value - DateTime.UtcNow;

        // 5 menit warning
        if (remaining.TotalMinutes <= 5 && remaining.TotalMinutes > 0 && !_isLocked)
        {
            SessionExpiring?.Invoke(this, EventArgs.Empty);
        }

        // Waktu habis -> lock
        if (remaining.TotalSeconds <= 0 && !_isLocked)
        {
            Lock();
        }
    }
}
