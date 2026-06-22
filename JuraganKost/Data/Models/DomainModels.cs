namespace JuraganKost.Data.Models;

/// <summary>
/// Represents a boarding house property (kostan)
/// </summary>
public class Kost
{
    public int Id { get; set; }
    public string Nama { get; set; } = string.Empty;
    public string Alamat { get; set; } = string.Empty;
    public string? Kota { get; set; }
    public string? Provinsi { get; set; }
    public string? KodePos { get; set; }
    public string? Deskripsi { get; set; }
    public string? GambarUrl { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Telepon { get; set; }
    public string? Email { get; set; }
    public KostStatus Status { get; set; } = KostStatus.Aktif;
    public JenisKost Jenis { get; set; } = JenisKost.Campuran;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Relationships
    public ICollection<Kamar> Kamar { get; set; } = new List<Kamar>();
    public ICollection<Staff> Staff { get; set; } = new List<Staff>();
    public ICollection<InventarisItem> Inventaris { get; set; } = new List<InventarisItem>();
    public string? PemilikId { get; set; }
    public ApplicationUser? Pemilik { get; set; }
}

public enum KostStatus { Aktif, Nonaktif, DalamPerbaikan }
public enum JenisKost { Putra, Putri, Campuran }

/// <summary>
/// Represents a room in a kost
/// </summary>
public class Kamar
{
    public int Id { get; set; }
    public string NomorKamar { get; set; } = string.Empty;
    public int KostId { get; set; }
    public Kost? Kost { get; set; }
    public decimal HargaSewa { get; set; }
    public decimal? Deposit { get; set; }
    public int? Luas { get; set; } // m²
    public StatusKamar Status { get; set; } = StatusKamar.Kosong;
    public JenisKamar Jenis { get; set; } = JenisKamar.Standar;
    public int? Kapasitas { get; set; } = 1;
    public string? Fasilitas { get; set; } // JSON array
    public string? GambarUrl { get; set; }
    public string? Deskripsi { get; set; }
    public bool IsTersedia { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Penghuni> Penghuni { get; set; } = new List<Penghuni>();
    public ICollection<Kontrak> Kontrak { get; set; } = new List<Kontrak>();
    public ICollection<Tagihan> Tagihan { get; set; } = new List<Tagihan>();
}

public enum StatusKamar { Kosong, Terisi, Booking, Perbaikan }
public enum JenisKamar { Standar, Premium, VIP, Suite }

/// <summary>
/// Represents a tenant/occupant
/// </summary>
public class Penghuni
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string NamaLengkap { get; set; } = string.Empty;
    public string? NIK { get; set; }
    public string? NoHP { get; set; }
    public string? Email { get; set; }
    public string? Pekerjaan { get; set; }
    public string? KontakDarurat { get; set; }
    public string? HubunganKontakDarurat { get; set; }
    public string? AlamatAsal { get; set; }
    public string? FotoUrl { get; set; }
    public StatusPenghuni Status { get; set; } = StatusPenghuni.Aktif;
    public int? KamarId { get; set; }
    public Kamar? Kamar { get; set; }
    public DateTime TanggalMasuk { get; set; } = DateTime.UtcNow;
    public DateTime? TanggalKeluar { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Kontrak> Kontrak { get; set; } = new List<Kontrak>();
    public ICollection<Pembayaran> Pembayaran { get; set; } = new List<Pembayaran>();
    public ICollection<Komplain> Komplain { get; set; } = new List<Komplain>();
    public ICollection<Review> Review { get; set; } = new List<Review>();
}

public enum StatusPenghuni { Aktif, Keluar, Blacklist }

/// <summary>
/// Rental contract between tenant and kost
/// </summary>
public class Kontrak
{
    public int Id { get; set; }
    public string NomorKontrak { get; set; } = string.Empty;
    public int PenghuniId { get; set; }
    public Penghuni? Penghuni { get; set; }
    public int KamarId { get; set; }
    public Kamar? Kamar { get; set; }
    public DateTime TanggalMulai { get; set; }
    public DateTime TanggalSelesai { get; set; }
    public decimal HargaSewa { get; set; }
    public decimal Deposit { get; set; }
    public decimal? DendaPerHari { get; set; }
    public StatusKontrak Status { get; set; } = StatusKontrak.Aktif;
    public string? DokumenUrl { get; set; }
    public string? Catatan { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum StatusKontrak { Aktif, Selesai, Dibatalkan, Perpanjangan }

/// <summary>
/// Monthly billing for tenants
/// </summary>
public class Tagihan
{
    public int Id { get; set; }
    public string NomorTagihan { get; set; } = string.Empty;
    public int PenghuniId { get; set; }
    public Penghuni? Penghuni { get; set; }
    public int? KamarId { get; set; }
    public Kamar? Kamar { get; set; }
    public int? KontrakId { get; set; }
    public Kontrak? Kontrak { get; set; }
    public JenisTagihan Jenis { get; set; } = JenisTagihan.SewaKamar;
    public decimal Jumlah { get; set; }
    public decimal? Denda { get; set; }
    public decimal? Diskon { get; set; }
    public decimal Total => Jumlah + (Denda ?? 0) - (Diskon ?? 0);
    public StatusTagihan Status { get; set; } = StatusTagihan.BelumDibayar;
    public DateTime JatuhTempo { get; set; }
    public DateTime? TanggalBayar { get; set; }
    public string? Deskripsi { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum JenisTagihan { SewaKamar, Listrik, Air, Internet, Laundry, Catering, Parkir, Lainnya }
public enum StatusTagihan { BelumDibayar, Dibayar, Terlambat, Dibatalkan }

/// <summary>
/// Payment record
/// </summary>
public class Pembayaran
{
    public int Id { get; set; }
    public string NomorPembayaran { get; set; } = string.Empty;
    public int PenghuniId { get; set; }
    public Penghuni? Penghuni { get; set; }
    public int? TagihanId { get; set; }
    public Tagihan? Tagihan { get; set; }
    public decimal Jumlah { get; set; }
    public MetodePembayaran Metode { get; set; } = MetodePembayaran.Transfer;
    public StatusPembayaran Status { get; set; } = StatusPembayaran.Pending;
    public string? BuktiUrl { get; set; }
    public string? Referensi { get; set; }
    public DateTime TanggalBayar { get; set; } = DateTime.UtcNow;
    public string? Catatan { get; set; }
}

public enum MetodePembayaran { Transfer, EWallet, QRIS, VirtualAccount, Tunai }
public enum StatusPembayaran { Pending, Diverifikasi, Ditolak, Refund }

/// <summary>
/// Complaint/service request from tenants
/// </summary>
public class Komplain
{
    public int Id { get; set; }
    public string NomorKomplain { get; set; } = string.Empty;
    public int PenghuniId { get; set; }
    public Penghuni? Penghuni { get; set; }
    public int? KamarId { get; set; }
    public Kamar? Kamar { get; set; }
    public KategoriKomplain Kategori { get; set; }
    public string Judul { get; set; } = string.Empty;
    public string Deskripsi { get; set; } = string.Empty;
    public string? GambarUrl { get; set; }
    public StatusKomplain Status { get; set; } = StatusKomplain.Menunggu;
    public string? Respon { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? SelesaiAt { get; set; }
}

public enum KategoriKomplain { Listrik, Air, Kebersihan, Fasilitas, Keamanan, Kebisingan, Lainnya }
public enum StatusKomplain { Menunggu, Diproses, Selesai, Ditolak }

/// <summary>
/// Inventory items in a kost
/// </summary>
public class InventarisItem
{
    public int Id { get; set; }
    public string Nama { get; set; } = string.Empty;
    public string? Kode { get; set; }
    public int KostId { get; set; }
    public Kost? Kost { get; set; }
    public int? KamarId { get; set; }
    public Kamar? Kamar { get; set; }
    public KategoriInventaris Kategori { get; set; }
    public int Jumlah { get; set; } = 1;
    public StatusInventaris Status { get; set; } = StatusInventaris.Baik;
    public string? Deskripsi { get; set; }
    public decimal? HargaBeli { get; set; }
    public DateTime? TanggalBeli { get; set; }
    public DateTime? TanggalPerawatan { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum KategoriInventaris { Furniture, Elektronik, Peralatan, Lainnya }
public enum StatusInventaris { Baik, RusakRingan, RusakBerat, DalamPerbaikan, Diganti }

/// <summary>
/// Staff/employee of a kost
/// </summary>
public class Staff
{
    public int Id { get; set; }
    public string Nama { get; set; } = string.Empty;
    public int KostId { get; set; }
    public Kost? Kost { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public PosisiStaff Posisi { get; set; } = PosisiStaff.CleaningService;
    public string? NoHP { get; set; }
    public string? Email { get; set; }
    public decimal? Gaji { get; set; }
    public string? JadwalKerja { get; set; } // JSON
    public StatusStaff Status { get; set; } = StatusStaff.Aktif;
    public DateTime TanggalMasuk { get; set; } = DateTime.UtcNow;
    public DateTime? TanggalKeluar { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PosisiStaff { Penjaga, CleaningService, Teknisi, Admin, Lainnya }
public enum StatusStaff { Aktif, Cuti, Keluar }

/// <summary>
/// Review/rating for a kost
/// </summary>
public class Review
{
    public int Id { get; set; }
    public int KostId { get; set; }
    public Kost? Kost { get; set; }
    public int PenghuniId { get; set; }
    public Penghuni? Penghuni { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Komentar { get; set; }
    public string? Emoji { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Notification system
/// </summary>
public class Notifikasi
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Judul { get; set; } = string.Empty;
    public string Pesan { get; set; } = string.Empty;
    public TipeNotifikasi Tipe { get; set; }
    public bool IsDibaca { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DibacaAt { get; set; }
    public string? LinkUrl { get; set; }
}

public enum TipeNotifikasi { Tagihan, Kontrak, Komplain, Pengumuman, Pembayaran, Sistem }

/// <summary>
/// IoT sensor data from kost rooms
/// </summary>
public class IoTSensorData
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int? KamarId { get; set; }
    public Kamar? Kamar { get; set; }
    public JenisSensor Jenis { get; set; }
    public double Nilai { get; set; }
    public string? Satuan { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum JenisSensor { Listrik_kWh, Air_Liter, Suhu_C, Kelembaban_Persen, SmartLock, Gerakan }

/// <summary>
/// Marketplace listing for public kost search
/// </summary>
public class MarketplaceListing
{
    public int Id { get; set; }
    public int KostId { get; set; }
    public Kost? Kost { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? HighlightFitur { get; set; }
    public bool IsPromoted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
