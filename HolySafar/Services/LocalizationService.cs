using System.Globalization;

namespace HolySafar.Services;

/// <summary>
/// Multi bahasa Indonesia/Inggris (requirements: Fitur Tambahan — Multi Bahasa).
///
/// Bahasa dipilih lewat cookie budaya ASP.NET Core (endpoint /set-culture), sehingga
/// format tanggal/mata uang ikut berubah. Teks diambil dari kamus di bawah:
/// <c>L["nav.jamaah"]</c>. Kunci yang belum diterjemahkan mengembalikan teks Indonesia
/// (fallback), jadi menambah terjemahan bisa bertahap tanpa merusak halaman.
/// </summary>
public class LocalizationService
{
    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
    public string CurrentLanguage => IsEnglish ? "en" : "id";
    public string CurrentLanguageLabel => IsEnglish ? "EN" : "ID";

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (!_id.TryGetValue(key, out var idText)) return key;
        if (!IsEnglish) return idText;
        return _en.TryGetValue(key, out var enText) ? enText : idText;
    }

    private static readonly Dictionary<string, string> _id = new()
    {
        ["nav.beranda"] = "Beranda",
        ["nav.masterAdmin"] = "Admin Master Data",
        ["nav.monitoring"] = "Admin Monitoring",
        ["nav.agen"] = "Agen",
        ["nav.menu"] = "Menu",
        ["nav.user"] = "User",
        ["nav.jamaah"] = "Jamaah",
        ["nav.paket"] = "Paket",
        ["nav.produk"] = "Produk",
        ["nav.pembayaran"] = "Pembayaran",
        ["nav.transaksi"] = "Transaksi",
        ["nav.keberangkatan"] = "Keberangkatan",
        ["nav.materi"] = "Materi Manasik",
        ["nav.kuis"] = "Kuis",
        ["nav.pengumuman"] = "Pengumuman",
        ["nav.operasional"] = "Operasional",
        ["nav.laporan"] = "Laporan",
        ["nav.sosPanel"] = "SOS Panel",
        ["nav.orders"] = "Orders",
        ["nav.dokumen"] = "Dokumen",
        ["nav.dokumenSaya"] = "Dokumen Saya",
        ["nav.itinerary"] = "Itinerary",
        ["nav.perjalanan"] = "Perjalanan Saya",
        ["nav.tracking"] = "Tracking Proses",
        ["nav.tagihan"] = "Tagihan & Pembayaran",
        ["nav.kelolaPaket"] = "Kelola Paket",
        ["nav.jamaahSaya"] = "Jamaah Saya",
        ["nav.marketplace"] = "Marketplace",
        ["nav.edukasi"] = "Edukasi",
        ["nav.chatbot"] = "Syeikh Jenggot",
        ["nav.gpsTracker"] = "GPS Tracker",
        ["nav.gpsAdmin"] = "GPS Admin",
        ["nav.sos"] = "SOS",
        ["nav.forum"] = "Forum Jamaah",
        ["nav.pesan"] = "Pesan",
        ["nav.pengaturan"] = "Pengaturan",
        ["ui.profil"] = "Profil Saya",
        ["ui.logout"] = "Logout",
        ["ui.login"] = "Login",
        ["ui.bahasa"] = "Bahasa",
        ["ui.tema"] = "Tema",
        ["ui.dashboard"] = "Dashboard",
        ["ui.simpan"] = "Simpan",
        ["ui.batal"] = "Batal",
        ["ui.tutup"] = "Tutup",
        ["ui.hapus"] = "Hapus",
        ["ui.edit"] = "Edit",
        ["ui.tambah"] = "Tambah",
        ["ui.cari"] = "Cari",
        ["ui.bayar"] = "Bayar Sekarang",
        ["ui.belumLogin"] = "Anda belum login"
    };

    private static readonly Dictionary<string, string> _en = new()
    {
        ["nav.beranda"] = "Home",
        ["nav.masterAdmin"] = "Admin Master Data",
        ["nav.monitoring"] = "Admin Monitoring",
        ["nav.agen"] = "Agent",
        ["nav.menu"] = "Menu",
        ["nav.user"] = "Users",
        ["nav.jamaah"] = "Pilgrims",
        ["nav.paket"] = "Packages",
        ["nav.produk"] = "Products",
        ["nav.pembayaran"] = "Payments",
        ["nav.transaksi"] = "Transactions",
        ["nav.keberangkatan"] = "Departures",
        ["nav.materi"] = "Manasik Materials",
        ["nav.kuis"] = "Quiz",
        ["nav.pengumuman"] = "Announcements",
        ["nav.operasional"] = "Operations",
        ["nav.laporan"] = "Reports",
        ["nav.sosPanel"] = "SOS Panel",
        ["nav.orders"] = "Orders",
        ["nav.dokumen"] = "Documents",
        ["nav.dokumenSaya"] = "My Documents",
        ["nav.itinerary"] = "Itinerary",
        ["nav.perjalanan"] = "My Journey",
        ["nav.tracking"] = "Process Tracking",
        ["nav.tagihan"] = "Bills & Payment",
        ["nav.kelolaPaket"] = "Manage Packages",
        ["nav.jamaahSaya"] = "My Pilgrims",
        ["nav.marketplace"] = "Marketplace",
        ["nav.edukasi"] = "Education",
        ["nav.chatbot"] = "Syeikh Jenggot",
        ["nav.gpsTracker"] = "GPS Tracker",
        ["nav.gpsAdmin"] = "GPS Admin",
        ["nav.sos"] = "SOS",
        ["nav.forum"] = "Pilgrim Forum",
        ["nav.pesan"] = "Messages",
        ["nav.pengaturan"] = "Settings",
        ["ui.profil"] = "My Profile",
        ["ui.logout"] = "Logout",
        ["ui.login"] = "Login",
        ["ui.bahasa"] = "Language",
        ["ui.tema"] = "Theme",
        ["ui.dashboard"] = "Dashboard",
        ["ui.simpan"] = "Save",
        ["ui.batal"] = "Cancel",
        ["ui.tutup"] = "Close",
        ["ui.hapus"] = "Delete",
        ["ui.edit"] = "Edit",
        ["ui.tambah"] = "Add",
        ["ui.cari"] = "Search",
        ["ui.bayar"] = "Pay Now",
        ["ui.belumLogin"] = "You are not signed in"
    };
}
