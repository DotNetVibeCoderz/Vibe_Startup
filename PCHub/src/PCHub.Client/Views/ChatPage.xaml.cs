using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCHub.Client.Services;
using PCHub.Shared.DTOs;

namespace PCHub.Client.Views;

public partial class ChatPage : UserControl
{
    private readonly ApiService _api;
    private Guid? _sessionId;
    private bool _isProcessing;

    public ChatPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
        Loaded += async (s, e) => await InitChat();
    }

    private async Task InitChat()
    {
        try
        {
            var sessions = await _api.GetChatSessionsAsync();
            if (sessions.Any())
            {
                var lastSession = sessions.OrderByDescending(s => s.CreatedAt).First();
                _sessionId = lastSession.Id;
                TxtSessionId.Text = $"Session: {lastSession.Id.ToString()[..8]}...";
                TxtSubtitle.Text = $"Chat with Koh Dedi - {lastSession.MessageCount} messages";
            }
            else
            {
                await CreateNewSession();
            }
        }
        catch
        {
            TxtBotInfo.Text = "Koh Dedi (Offline)";
            TxtSubtitle.Text = "Running in offline mode";
        }
    }

    private async Task CreateNewSession()
    {
        try
        {
            var session = await _api.CreateChatSessionAsync();
            if (session != null)
            {
                _sessionId = session.Id;
                TxtSessionId.Text = $"Session: {session.Id.ToString()[..8]}...";
                TxtSubtitle.Text = "New chat session created";
                MessagesPanel.Children.Clear();
            }
        }
        catch
        {
            TxtBotInfo.Text = "Koh Dedi (Offline)";
        }
    }

    private async void SendBtn_Click(object sender, RoutedEventArgs e) => await SendMessage();

    private async void MessageBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
        {
            e.Handled = true;
            await SendMessage();
        }
    }

    private async Task SendMessage()
    {
        var text = MessageBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || _isProcessing) return;

        _isProcessing = true;
        AddBubble("You", text, true);
        MessageBox.Text = "";

        // Loading bubble
        var loadingBubble = AddBubble("Koh Dedi", "Typing...", false);

        try
        {
            if (_sessionId == null)
                await CreateNewSession();

            if (_sessionId != null)
            {
                var response = await _api.SendChatMessageAsync(_sessionId.Value, text);
                MessagesPanel.Children.Remove(loadingBubble);

                if (response != null)
                    AddBubble("Koh Dedi", response.Content, false);
                else
                    AddBubble("Koh Dedi", GetOfflineResponse(text), false);
            }
        }
        catch
        {
            MessagesPanel.Children.Remove(loadingBubble);
            AddBubble("Koh Dedi", GetOfflineResponse(text), false);
        }

        _isProcessing = false;
        ChatScroll.ScrollToBottom();
    }

    private Border AddBubble(string sender, string text, bool isUser)
    {
        var border = new Border
        {
            Background = isUser
                ? (Brush)Application.Current.Resources["ChatUserBgBrush"]
                : (Brush)Application.Current.Resources["ChatBotBgBrush"],
            BorderBrush = isUser
                ? new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8))
                : (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(isUser ? 60 : 8, 4, isUser ? 8 : 60, 4),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 520
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = sender,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = isUser
                ? new SolidColorBrush(Color.FromRgb(0xBF, 0xDB, 0xFE))
                : new SolidColorBrush(Color.FromRgb(0x65, 0x67, 0x6B)),
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Parse simple markdown
        var messageBlock = new TextBlock
        {
            FontSize = 13,
            Foreground = isUser
                ? new SolidColorBrush(Colors.White)
                : (Brush)Application.Current.Resources["TextBrush"],
            TextWrapping = TextWrapping.Wrap
        };

        // Simple text rendering
        messageBlock.Text = text;
        stack.Children.Add(messageBlock);

        // Timestamp
        stack.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm"),
            FontSize = 9,
            Foreground = isUser
                ? new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD))
                : new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        });

        border.Child = stack;
        MessagesPanel.Children.Add(border);
        ChatScroll.ScrollToBottom();
        return border;
    }

    private async void NewChat_Click(object sender, RoutedEventArgs e)
    {
        await CreateNewSession();
    }

    private void ClearChat_Click(object sender, RoutedEventArgs e)
    {
        MessagesPanel.Children.Clear();
        TxtSubtitle.Text = "Chat cleared";
    }

    private string GetOfflineResponse(string msg)
    {
        var lower = msg.ToLower();

        // Price queries
        if (lower.Contains("harga") || lower.Contains("tarif") || lower.Contains("price") || lower.Contains("rate"))
            return "💰 **Tarif PCHub Game Center:**\n\n" +
                   "• Reguler: Rp 6.000 - Rp 12.000/jam\n" +
                   "• 🥈 Silver: Rp 50.000/bulan (diskon 5%)\n" +
                   "• 🥇 Gold: Rp 150.000/bulan (diskon 10%)\n" +
                   "• 💎 Platinum: Rp 350.000/bulan (diskon 15%)\n" +
                   "• 👑 VIP: Rp 750.000/bulan (diskon 20%)\n\n" +
                   "Semua member dapat bonus jam gratis dan loyalty points!";

        // Game queries
        if (lower.Contains("game") || lower.Contains("main"))
            return "🎮 **Game Tersedia di PCHub:**\n\n" +
                   "• 🔫 Valorant, CS2, Overwatch\n" +
                   "• ⚔️ Dota 2, League of Legends, Mobile Legends\n" +
                   "• 🪂 PUBG, Apex Legends, Fortnite\n" +
                   "• 🗡️ Genshin Impact, Elden Ring\n" +
                   "• ⚽ FIFA 24, NBA 2K24\n" +
                   "• 🧱 Minecraft, Roblox\n" +
                   "• 🏎️ Forza, Need for Speed\n\n" +
                   "Cek halaman Game Launcher untuk melihat dan menjalankan game!";

        // PC specs
        if (lower.Contains("pc") || lower.Contains("spek") || lower.Contains("komputer"))
            return "🖥️ **Spesifikasi PC PCHub:**\n\n" +
                   "Tersedia 15 PC Gaming dengan spesifikasi:\n" +
                   "• Ryzen 5 / Intel i7 Gen 12+\n" +
                   "• RTX 3060 / RTX 4060\n" +
                   "• RAM 16GB - 32GB DDR5\n" +
                   "• SSD NVMe 512GB - 1TB\n" +
                   "• Monitor 144Hz - 240Hz\n" +
                   "• Headset Gaming + Webcam\n\n" +
                   "Semua PC dirawat rutin dan siap digunakan!";

        // Hours
        if (lower.Contains("jam") || lower.Contains("buka") || lower.Contains("operasional"))
            return "🏢 **Jam Operasional PCHub:**\n\n" +
                   "🕐 **Buka 24 Jam Non-Stop!**\n" +
                   "Setiap hari, termasuk hari libur nasional.\n\n" +
                   "📅 Reservasi bisa dilakukan melalui Web Admin\n" +
                   "atau langsung datang ke lokasi.\n\n" +
                   "📍 Lokasi dan kontak tersedia di halaman Settings.";

        // Help / support
        if (lower.Contains("help") || lower.Contains("bantu") || lower.Contains("support") || lower.Contains("admin"))
            return "👋 **Butuh bantuan?**\n\n" +
                   "Saya Koh Dedi, asisten virtual PCHub.\n" +
                   "Saya bisa bantu dengan:\n" +
                   "• 💰 Info harga & membership\n" +
                   "• 🎮 Info game yang tersedia\n" +
                   "• 🖥️ Info spek & ketersediaan PC\n" +
                   "• 📅 Info booking & reservasi\n" +
                   "• 🏢 Info jam operasional\n\n" +
                   "Atau hubungi admin kami melalui Web Admin untuk bantuan lebih lanjut.";

        // Greeting
        if (lower.Contains("halo") || lower.Contains("hai") || lower.Contains("hello") || lower.Contains("hi"))
            return $"👋 Halo {App.Username}! Selamat datang di PCHub Game Center.\n\n" +
                   "Saya Koh Dedi, siap membantu Anda. Ada yang bisa saya bantu?\n\n" +
                   "Anda bisa tanya tentang:\n• Harga & tarif\n• Daftar game\n• Spesifikasi PC\n• Jam operasional";

        // Default
        return "🤖 Halo! Saya Koh Dedi, asisten virtual PCHub Game Center.\n\n" +
               "Silakan tanya tentang:\n" +
               "• 💰 Harga & tarif rental\n" +
               "• 🎮 Game yang tersedia\n" +
               "• 🖥️ Spesifikasi PC\n" +
               "• 🏢 Jam operasional\n" +
               "• 📅 Cara booking\n\n" +
               "Atau hubungi admin untuk bantuan lebih lanjut. 😊";
    }
}
