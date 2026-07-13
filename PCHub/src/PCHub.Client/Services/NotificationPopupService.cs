using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHub.Client.Services;

/// <summary>
/// Service untuk menampilkan pop-up notifikasi di aplikasi WPF.
/// Notifikasi muncul di pojok kanan bawah dan auto-close.
/// </summary>
public class NotificationPopupService
{
    private readonly DispatcherTimer _checkTimer;
    private readonly ApiService _api;
    private readonly List<PopupWindow> _activePopups = [];

    public event Action<string, string>? OnNotificationReceived;

    public NotificationPopupService(ApiService api)
    {
        _api = api;
        _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _checkTimer.Tick += async (s, e) => await CheckNotificationsAsync();
    }

    /// <summary>Mulai polling notifikasi dari server</summary>
    public void StartPolling()
    {
        _checkTimer.Start();
    }

    /// <summary>Hentikan polling</summary>
    public void StopPolling()
    {
        _checkTimer.Stop();
    }

    /// <summary>Tampilkan notifikasi lokal (tanpa server)</summary>
    public void ShowLocal(string title, string message, NotificationType type = NotificationType.Info)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var popup = new PopupWindow(title, message, type);
            popup.Closed += (s, e) => _activePopups.Remove(popup);
            _activePopups.Add(popup);
            popup.Show();
        });

        OnNotificationReceived?.Invoke(title, message);
    }

    private async Task CheckNotificationsAsync()
    {
        if (App.UserId == Guid.Empty) return;
        try
        {
            var notifications = await _api.GetNotificationsAsync(App.UserId);
            foreach (var n in notifications.Where(n => !n.IsRead))
            {
                ShowLocal(n.Title, n.Message,
                    n.Type == PCHub.Shared.Enums.NotificationType.Promo ? NotificationType.Promo :
                    n.Type == PCHub.Shared.Enums.NotificationType.Reminder ? NotificationType.Warning :
                    NotificationType.Info);
            }
        }
        catch { /* Server unavailable */ }
    }
}

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Promo,
    Success
}

/// <summary>Popup window untuk notifikasi</summary>
internal class PopupWindow : Window
{
    private readonly DispatcherTimer _closeTimer;

    public PopupWindow(string title, string message, NotificationType type)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = true;
        Topmost = true;
        Width = 340;
        Height = 100;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Hitung posisi (kanan bawah)
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 20;
        Top = screen.Bottom - Height - 20 - (_activePopupCount * 110);
        _activePopupCount++;

        // Border warna sesuai tipe
        var borderColor = type switch
        {
            NotificationType.Error => Color.FromRgb(0xEF, 0x44, 0x44),
            NotificationType.Warning => Color.FromRgb(0xF5, 0x9E, 0x0B),
            NotificationType.Promo => Color.FromRgb(0x8B, 0x5C, 0xF6),
            NotificationType.Success => Color.FromRgb(0x10, 0xB9, 0x81),
            _ => Color.FromRgb(0x25, 0x63, 0xEB)
        };

        var border = new System.Windows.Controls.Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10)
        };

        var stack = new System.Windows.Controls.StackPanel();
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
            Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x67, 0x6B)),
            TextWrapping = TextWrapping.Wrap
        });

        border.Child = stack;
        Content = border;

        // Auto close dalam 5 detik
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _closeTimer.Tick += (s, e) =>
        {
            _closeTimer.Stop();
            _activePopupCount--;
            Close();
        };
        _closeTimer.Start();

        // Klik untuk close
        MouseDown += (s, e) =>
        {
            _closeTimer.Stop();
            _activePopupCount--;
            Close();
        };
    }

    private static int _activePopupCount;
}
