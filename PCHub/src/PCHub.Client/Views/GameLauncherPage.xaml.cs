using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCHub.Client.Services;
using PCHub.Shared.DTOs;

namespace PCHub.Client.Views;

public partial class GameLauncherPage : UserControl
{
    private readonly ApiService _api;
    private List<GameDto> _allGames = [];
    private string _currentGenre = "";
    private string _searchText = "";

    public GameLauncherPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
        SearchBox.Text = "";
        SearchPlaceholder.Visibility = Visibility.Visible;
        SearchBox.GotFocus += (s, e) => SearchPlaceholder.Visibility = Visibility.Collapsed;
        SearchBox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrEmpty(SearchBox.Text))
                SearchPlaceholder.Visibility = Visibility.Visible;
        };
        Loaded += async (s, e) => await LoadGames();
    }

    private async Task LoadGames()
    {
        TxtLoading.Visibility = Visibility.Visible;
        TxtEmpty.Visibility = Visibility.Collapsed;
        try
        {
            _allGames = await _api.GetGamesAsync();
            FilterAndDisplay();
        }
        catch
        {
            TxtLoading.Text = "Offline - cannot load games";
        }
        TxtLoading.Visibility = Visibility.Collapsed;
    }

    private void FilterAndDisplay()
    {
        var filtered = _allGames.AsEnumerable();

        // Filter by genre
        if (!string.IsNullOrEmpty(_currentGenre))
            filtered = filtered.Where(g => g.Genre.ToString().Equals(_currentGenre, StringComparison.OrdinalIgnoreCase));

        // Filter by search
        if (!string.IsNullOrEmpty(_searchText))
            filtered = filtered.Where(g => g.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        var games = filtered.ToList();
        GameCardsPanel.Children.Clear();

        foreach (var game in games)
        {
            var card = CreateGameCard(game);
            GameCardsPanel.Children.Add(card);
        }

        TxtGameCount.Text = $"{games.Count} game(s)";
        TxtEmpty.Visibility = games.Any() ? Visibility.Collapsed : Visibility.Visible;
    }

    private Border CreateGameCard(GameDto game)
    {
        var border = new Border
        {
            Style = (Style)FindResource("NeoCard"),
            Width = 210, Margin = new Thickness(0, 0, 12, 12),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(16)
        };

        var stack = new StackPanel();

        // Game icon
        var iconBorder = new Border
        {
            Width = 60, Height = 60,
            Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };
        iconBorder.Child = new TextBlock
        {
            Text = game.Genre switch
            {
                PCHub.Shared.Enums.GameGenre.FPS => "🔫",
                PCHub.Shared.Enums.GameGenre.MOBA => "⚔️",
                PCHub.Shared.Enums.GameGenre.RPG => "🗡️",
                PCHub.Shared.Enums.GameGenre.Racing => "🏎️",
                PCHub.Shared.Enums.GameGenre.Sport => "⚽",
                PCHub.Shared.Enums.GameGenre.BattleRoyale => "🪂",
                PCHub.Shared.Enums.GameGenre.Strategy => "🧠",
                PCHub.Shared.Enums.GameGenre.Horror => "👻",
                PCHub.Shared.Enums.GameGenre.Adventure => "🗺️",
                _ => "🎮"
            },
            FontSize = 30,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(iconBorder);

        // Game name
        stack.Children.Add(new TextBlock
        {
            Text = game.Name,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Genre badge
        var genreBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xFF)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };
        genreBadge.Child = new TextBlock
        {
            Text = game.Genre.ToString(),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))
        };
        stack.Children.Add(genreBadge);

        // Version
        if (!string.IsNullOrEmpty(game.Version))
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"v{game.Version}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x67, 0x6B)),
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        // Launch / Install status
        if (game.IsInstalled)
        {
            var btn = new Button
            {
                Content = "▶ Launch",
                Style = (Style)FindResource("PrimaryButton"),
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 12,
                Padding = new Thickness(16, 6, 16, 6),
                Tag = game.ExecutablePath
            };
            btn.Click += LaunchGame_Click;
            stack.Children.Add(btn);
        }
        else
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xFB, 0xEB)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            badge.Child = new TextBlock
            {
                Text = "Not installed",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))
            };
            stack.Children.Add(badge);
        }

        border.Child = stack;
        return border;
    }

    private void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch game: {ex.Message}\nPath: {path}",
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        FilterAndDisplay();
    }

    private void FilterGenre_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            _currentGenre = btn.Tag?.ToString() ?? "";

            // Update button styles
            foreach (var child in GenreFilter.Children)
            {
                if (child is Button b)
                {
                    b.Style = b.Tag?.ToString() == _currentGenre
                        ? (Style)FindResource("PrimaryButton")
                        : (Style)FindResource("NeoButton");
                }
            }

            FilterAndDisplay();
        }
    }
}
