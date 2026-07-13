using System.Globalization;
using System.Text.Json;

namespace PCHub.Client.Services;

/// <summary>
/// Service untuk multi-language support (Bahasa Indonesia & English).
/// Menyimpan dan memuat string resources dari JSON.
/// </summary>
public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private Dictionary<string, string> _strings = [];
    private string _currentLanguage = "id";

    public string CurrentLanguage => _currentLanguage;
    public bool IsIndonesian => _currentLanguage == "id";

    public event Action? LanguageChanged;

    private LocalizationService()
    {
        LoadLanguage(_currentLanguage);
    }

    /// <summary>Ganti bahasa (id/en)</summary>
    public void SetLanguage(string lang)
    {
        if (_currentLanguage == lang) return;
        _currentLanguage = lang;
        LoadLanguage(lang);
        LanguageChanged?.Invoke();
    }

    /// <summary>Dapatkan string berdasarkan key</summary>
    public string Get(string key, string defaultValue = "")
    {
        return _strings.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>Indexer: Loc["key"]</summary>
    public string this[string key] => Get(key, key);

    private void LoadLanguage(string lang)
    {
        _strings = lang switch
        {
            "id" => IndonesianStrings(),
            "en" => EnglishStrings(),
            _ => EnglishStrings()
        };
    }

    private static Dictionary<string, string> IndonesianStrings() => new()
    {
        ["app.title"] = "PCHub Game Center",
        ["app.subtitle"] = "GAME CENTER",
        ["nav.dashboard"] = "📊 Dashboard",
        ["nav.games"] = "🎮 Game Launcher",
        ["nav.billing"] = "💰 Billing",
        ["nav.chat"] = "💬 Chat Support",
        ["nav.reservations"] = "📅 Booking Saya",
        ["nav.settings"] = "⚙️ Pengaturan",
        ["nav.logout"] = "🚪 Keluar",
        ["login.title"] = "PCHub Game Center",
        ["login.subtitle"] = "Masuk ke akun Anda",
        ["login.username"] = "Username",
        ["login.password"] = "Password",
        ["login.button"] = "Masuk",
        ["login.demo"] = "Demo: admin / Admin123!",
        ["login.error"] = "Username atau password salah.",
        ["login.connecting"] = "Menghubungkan...",
        ["dashboard.totalUsers"] = "Total Pengguna",
        ["dashboard.availablePcs"] = "PC Tersedia",
        ["dashboard.todayRevenue"] = "Pendapatan Hari Ini",
        ["dashboard.activeSessions"] = "Sesi Aktif",
        ["dashboard.yourSession"] = "Sesi Aktif Anda",
        ["dashboard.noSession"] = "Tidak ada sesi aktif",
        ["dashboard.stopSession"] = "⏹ Akhiri Sesi",
        ["dashboard.popularGames"] = "Game Populer",
        ["billing.timer"] = "Timer Sesi Aktif",
        ["billing.cost"] = "Biaya",
        ["billing.noSession"] = "Tidak ada sesi aktif",
        ["billing.endSession"] = "⏹ Akhiri Sesi",
        ["billing.startDemo"] = "▶ Mulai Demo Sesi",
        ["billing.history"] = "Riwayat Billing",
        ["billing.pcInfo"] = "Sesi PC aktif | Tarif: Rp {0}/jam",
        ["chat.title"] = "Chat Support",
        ["chat.placeholder"] = "Ketik pesan...",
        ["chat.send"] = "Kirim",
        ["chat.you"] = "Anda",
        ["chat.bot"] = "Koh Dedi",
        ["games.title"] = "Game Launcher",
        ["games.launch"] = "▶ Mainkan",
        ["games.notInstalled"] = "Belum terinstall",
        ["reservations.title"] = "Booking Saya",
        ["reservations.new"] = "+ Booking Baru",
        ["reservations.empty"] = "Belum ada booking.",
        ["reservations.comingSoon"] = "Fitur booking lengkap segera hadir!\nGunakan web admin untuk membuat booking.",
        ["settings.title"] = "Pengaturan",
        ["settings.server"] = "Koneksi Server",
        ["settings.apiUrl"] = "API Base URL",
        ["settings.appearance"] = "Tampilan",
        ["settings.darkMode"] = "Mode Gelap",
        ["settings.language"] = "Bahasa",
        ["settings.systemInfo"] = "Info Sistem",
        ["settings.save"] = "💾 Simpan Pengaturan",
        ["settings.saved"] = "Pengaturan disimpan!",
        ["settings.version"] = "PCHub Client v1.0.0",
        ["lock.title"] = "🔒 Sesi Berakhir",
        ["lock.message"] = "Waktu Anda habis!\nSilakan hubungi konter untuk memperpanjang sesi.",
        ["session.expiring"] = "Sesi akan berakhir dalam 5 menit!",
        ["common.ok"] = "OK",
        ["common.cancel"] = "Batal",
        ["common.yes"] = "Ya",
        ["common.no"] = "Tidak",
        ["common.loading"] = "Memuat...",
        ["common.error"] = "Error",
        ["common.info"] = "Info",
        ["common.offline"] = "Mode offline - tidak dapat terhubung ke server",
    };

    private static Dictionary<string, string> EnglishStrings() => new()
    {
        ["app.title"] = "PCHub Game Center",
        ["app.subtitle"] = "GAME CENTER",
        ["nav.dashboard"] = "📊 Dashboard",
        ["nav.games"] = "🎮 Game Launcher",
        ["nav.billing"] = "💰 Billing",
        ["nav.chat"] = "💬 Chat Support",
        ["nav.reservations"] = "📅 My Bookings",
        ["nav.settings"] = "⚙️ Settings",
        ["nav.logout"] = "🚪 Logout",
        ["login.title"] = "PCHub Game Center",
        ["login.subtitle"] = "Login to your account",
        ["login.username"] = "Username",
        ["login.password"] = "Password",
        ["login.button"] = "Login",
        ["login.demo"] = "Demo: admin / Admin123!",
        ["login.error"] = "Invalid username or password.",
        ["login.connecting"] = "Connecting...",
        ["dashboard.totalUsers"] = "Total Users",
        ["dashboard.availablePcs"] = "Available PCs",
        ["dashboard.todayRevenue"] = "Today Revenue",
        ["dashboard.activeSessions"] = "Active Sessions",
        ["dashboard.yourSession"] = "Your Active Session",
        ["dashboard.noSession"] = "No active session",
        ["dashboard.stopSession"] = "⏹ Stop Session",
        ["dashboard.popularGames"] = "Popular Games",
        ["billing.timer"] = "Active Session Timer",
        ["billing.cost"] = "Cost",
        ["billing.noSession"] = "No active session",
        ["billing.endSession"] = "⏹ End Session",
        ["billing.startDemo"] = "▶ Start Demo Session",
        ["billing.history"] = "Billing History",
        ["billing.pcInfo"] = "PC session active | Rate: Rp {0}/hr",
        ["chat.title"] = "Chat Support",
        ["chat.placeholder"] = "Type a message...",
        ["chat.send"] = "Send",
        ["chat.you"] = "You",
        ["chat.bot"] = "Koh Dedi",
        ["games.title"] = "Game Launcher",
        ["games.launch"] = "▶ Launch",
        ["games.notInstalled"] = "Not installed",
        ["reservations.title"] = "My Bookings",
        ["reservations.new"] = "+ New Booking",
        ["reservations.empty"] = "No bookings yet.",
        ["reservations.comingSoon"] = "Full booking feature coming soon!\nUse web admin to create bookings.",
        ["settings.title"] = "Settings",
        ["settings.server"] = "Server Connection",
        ["settings.apiUrl"] = "API Base URL",
        ["settings.appearance"] = "Appearance",
        ["settings.darkMode"] = "Dark Mode",
        ["settings.language"] = "Language",
        ["settings.systemInfo"] = "System Info",
        ["settings.save"] = "💾 Save Settings",
        ["settings.saved"] = "Settings saved!",
        ["settings.version"] = "PCHub Client v1.0.0",
        ["lock.title"] = "🔒 Session Ended",
        ["lock.message"] = "Your time is up!\nPlease contact the counter to extend your session.",
        ["session.expiring"] = "Session will expire in 5 minutes!",
        ["common.ok"] = "OK",
        ["common.cancel"] = "Cancel",
        ["common.yes"] = "Yes",
        ["common.no"] = "No",
        ["common.loading"] = "Loading...",
        ["common.error"] = "Error",
        ["common.info"] = "Info",
        ["common.offline"] = "Offline mode - cannot connect to server",
    };
}
