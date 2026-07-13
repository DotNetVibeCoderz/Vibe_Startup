using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCHub.Client.Services;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;

namespace PCHub.Client.Views;

public partial class ReservationsPage : UserControl
{
    private readonly ApiService _api;
    private List<PcDto> _availablePcs = [];
    private List<GameDto> _gameList = [];

    public ReservationsPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
        Loaded += async (s, e) => await LoadReservations();
    }

    private async Task LoadReservations()
    {
        TxtLoading.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        try
        {
            // Load PCs & games untuk dialog booking
            try { _availablePcs = (await _api.GetPcsAsync()).Where(p => p.Status == PcStatus.Available).ToList(); } catch { }
            try { _gameList = await _api.GetGamesAsync(); } catch { }

            var reservations = await _api.GetReservationsAsync();
            var reservationList = reservations.ToList();

            if (reservationList.Any())
            {
                ReservationsList.ItemsSource = reservationList.Select(r => new
                {
                    Display = $"{r.ReservationDate:dd MMM yyyy HH:mm} | {r.DurationMinutes}min | {r.GameRequested ?? "Any"}",
                    Status = r.Status.ToString()
                });
                TxtBookingCount.Text = $"{reservationList.Count} booking(s)";
                EmptyState.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtBookingCount.Text = "0 bookings";
                EmptyState.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            TxtBookingCount.Text = "Cannot load (offline)";
            EmptyState.Visibility = Visibility.Visible;
        }
        TxtLoading.Visibility = Visibility.Collapsed;
    }

    private void NewBooking_Click(object sender, RoutedEventArgs e)
    {
        ShowBookingDialog();
    }

    private void ShowBookingDialog()
    {
        var dialog = new Window
        {
            Title = "📅 New Booking - PCHub",
            Width = 480, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF5))
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var sp = new StackPanel { Margin = new Thickness(20) };

        // Header
        sp.Children.Add(new TextBlock
        {
            Text = "📅 Book a PC", FontSize = 20, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        // PC Select
        sp.Children.Add(new TextBlock { Text = "Select PC:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var pcCombo = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 13
        };
        pcCombo.Items.Add(new { Name = "Any PC", Id = (Guid?)null });
        foreach (var pc in _availablePcs)
            pcCombo.Items.Add(new { Name = $"{pc.Name} - Rp {pc.HourlyRate:N0}/hr", Id = (Guid?)pc.Id });
        pcCombo.DisplayMemberPath = "Name";
        pcCombo.SelectedIndex = 0;
        sp.Children.Add(pcCombo);

        // Date & Time
        sp.Children.Add(new TextBlock { Text = "Date & Time:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var datePicker = new DatePicker
        {
            SelectedDate = DateTime.Today,
            Margin = new Thickness(0, 0, 0, 12)
        };
        sp.Children.Add(datePicker);

        sp.Children.Add(new TextBlock { Text = "Time:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var hourCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 4), Width = 100 };
        for (int h = 9; h <= 23; h++) hourCombo.Items.Add($"{h:D2}:00");
        hourCombo.SelectedIndex = 4; // 13:00
        sp.Children.Add(hourCombo);

        var minCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), Width = 100 };
        minCombo.Items.Add("00"); minCombo.Items.Add("30");
        minCombo.SelectedIndex = 0;
        sp.Children.Add(minCombo);

        // Duration
        sp.Children.Add(new TextBlock { Text = "Duration:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var durCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), Width = 200 };
        durCombo.Items.Add("30 minutes");
        durCombo.Items.Add("1 hour");
        durCombo.Items.Add("1.5 hours");
        durCombo.Items.Add("2 hours");
        durCombo.Items.Add("3 hours");
        durCombo.Items.Add("4 hours");
        durCombo.SelectedIndex = 1;
        sp.Children.Add(durCombo);

        // Game
        sp.Children.Add(new TextBlock { Text = "Game:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var gameCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), FontSize = 13 };
        gameCombo.Items.Add("Any Game");
        foreach (var g in _gameList) gameCombo.Items.Add(g.Name);
        gameCombo.SelectedIndex = 0;
        sp.Children.Add(gameCombo);

        // Notes
        sp.Children.Add(new TextBlock { Text = "Notes:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var notesBox = new TextBox { Margin = new Thickness(0, 0, 0, 12), MinHeight = 40, TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(notesBox);

        // Error text
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            FontSize = 12, Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed
        };
        sp.Children.Add(errorText);

        // Buttons
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelBtn = new Button
        {
            Content = "Cancel", Width = 100, Height = 36,
            Background = new SolidColorBrush(Colors.White),
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
            BorderThickness = new Thickness(2), FontWeight = FontWeights.SemiBold
        };
        cancelBtn.Click += (s, args) => dialog.Close();
        btnPanel.Children.Add(cancelBtn);

        var submitBtn = new Button
        {
            Content = "📅 Book Now", Width = 120, Height = 36,
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)),
            BorderThickness = new Thickness(2),
            Margin = new Thickness(8, 0, 0, 0),
            FontWeight = FontWeights.SemiBold
        };
        submitBtn.Click += async (s, args) =>
        {
            errorText.Visibility = Visibility.Collapsed;
            try
            {
                // Parse date/time
                var date = datePicker.SelectedDate ?? DateTime.Today;
                var timeParts = (hourCombo.SelectedItem?.ToString() ?? "13:00").Split(':');
                var minParts = (minCombo.SelectedItem?.ToString() ?? "00");
                var reservationDate = new DateTime(date.Year, date.Month, date.Day,
                    int.Parse(timeParts[0]), int.Parse(minParts), 0);

                // Parse duration
                var durText = durCombo.SelectedItem?.ToString() ?? "1 hour";
                var duration = durText switch
                {
                    "30 minutes" => 30, "1 hour" => 60, "1.5 hours" => 90,
                    "2 hours" => 120, "3 hours" => 180, "4 hours" => 240, _ => 60
                };

                // Parse PC ID
                Guid? pcId = null;
                if (pcCombo.SelectedItem != null)
                {
                    try
                    {
                        dynamic selectedPc = pcCombo.SelectedItem;
                        pcId = selectedPc.Id as Guid?;
                    }
                    catch { }
                }

                var game = gameCombo.SelectedIndex > 0 ? gameCombo.SelectedItem?.ToString() : null;
                var notes = string.IsNullOrWhiteSpace(notesBox.Text) ? null : notesBox.Text.Trim();

                // Validate
                if (reservationDate < DateTime.Now)
                {
                    errorText.Text = "Cannot book in the past!";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                // Call API
                var client = new System.Net.Http.HttpClient { BaseAddress = new Uri(App.ApiBaseUrl) };
                var content = new System.Net.Http.StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        pcId, reservationDate, durationMinutes = duration,
                        gameRequested = game, notes
                    }),
                    System.Text.Encoding.UTF8, "application/json");

                // Use the correct API endpoint
                var response = await client.PostAsync(
                    $"/api/reservations?userId={App.UserId}", content);

                if (response.IsSuccessStatusCode)
                {
                    dialog.Close();
                    MessageBox.Show("✅ Booking created successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadReservations();
                }
                else
                {
                    errorText.Text = $"Failed: {(int)response.StatusCode} {response.ReasonPhrase}";
                    errorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                errorText.Text = $"Error: {ex.Message}";
                errorText.Visibility = Visibility.Visible;
            }
        };
        btnPanel.Children.Add(submitBtn);

        sp.Children.Add(btnPanel);
        scroll.Content = sp;
        dialog.Content = scroll;
        dialog.ShowDialog();
    }
}
