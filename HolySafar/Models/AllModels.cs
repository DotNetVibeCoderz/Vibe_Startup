using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HolySafar.Models;

public enum UserRole { Admin, Agen, Jamaah }
public enum DocumentStatus { Pending, Submitted, Verified, Rejected }
public enum PaymentStatus { Pending, Partial, Paid, Overdue, Cancelled }
public enum DepartureStatus { Scheduled, CheckIn, InTransit, Arrived, InHotel, Returning, Completed, Cancelled }

public class ApplicationUser
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(100)] public string Username { get; set; } = string.Empty;
    [Required, MaxLength(255)] public string PasswordHash { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(20)] public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Jamaah;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    [MaxLength(128)] public string? AuthToken { get; set; }
    [MaxLength(500)] public string? ProfilePhotoUrl { get; set; }
    public Jamaah? JamaahProfile { get; set; }
}

public class Jamaah
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(100)] public string NamaLengkap { get; set; } = string.Empty;
    [MaxLength(30)] public string Nik { get; set; } = string.Empty;
    [MaxLength(30)] public string NoPaspor { get; set; } = string.Empty;
    [MaxLength(30)] public string NoKK { get; set; } = string.Empty;
    [MaxLength(200)] public string TempatLahir { get; set; } = string.Empty;
    public DateTime? TanggalLahir { get; set; }
    [MaxLength(20)] public string JenisKelamin { get; set; } = "Laki-laki";
    [MaxLength(300)] public string Alamat { get; set; } = string.Empty;
    [MaxLength(100)] public string Kota { get; set; } = string.Empty;
    [MaxLength(30)] public string Provinsi { get; set; } = string.Empty;
    [MaxLength(20)] public string KodePos { get; set; } = string.Empty;
    [MaxLength(30)] public string NoTelepon { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(10)] public string GolonganDarah { get; set; } = string.Empty;
    [MaxLength(200)] public string NamaAyahKandung { get; set; } = string.Empty;
    [MaxLength(200)] public string AhliWaris { get; set; } = string.Empty;
    [MaxLength(50)] public string HubunganAhliWaris { get; set; } = string.Empty;
    [MaxLength(300)] public string AlamatAhliWaris { get; set; } = string.Empty;
    public DocumentStatus StatusDokumen { get; set; } = DocumentStatus.Pending;
    public DepartureStatus StatusKeberangkatan { get; set; } = DepartureStatus.Scheduled;
    public bool SudahVaksin { get; set; }
    [MaxLength(100)] public string JenisVaksin { get; set; } = string.Empty;
    public int? UserId { get; set; }
    [ForeignKey(nameof(UserId))] public ApplicationUser? User { get; set; }
    public int? PaketId { get; set; }
    [ForeignKey(nameof(PaketId))] public Paket? Paket { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LastLocationUpdate { get; set; }
}

public class DokumenJamaah
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(100)] public string NamaDokumen { get; set; } = string.Empty;
    [MaxLength(50)] public string TipeDokumen { get; set; } = string.Empty;
    [MaxLength(500)] public string FilePath { get; set; } = string.Empty;
    [MaxLength(500)] public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [MaxLength(50)] public string ContentType { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    [MaxLength(500)] public string? CatatanAdmin { get; set; }
    public int JamaahId { get; set; }
    [ForeignKey(nameof(JamaahId))] public Jamaah? Jamaah { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class Paket
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(200)] public string NamaPaket { get; set; } = string.Empty;
    [MaxLength(50)] public string JenisPaket { get; set; } = "Umroh";
    [MaxLength(4000)] public string Deskripsi { get; set; } = string.Empty;
    public decimal Harga { get; set; }
    [MaxLength(500)] public string Fasilitas { get; set; } = string.Empty;
    [MaxLength(200)] public string NamaHotelMekkah { get; set; } = string.Empty;
    [MaxLength(200)] public string NamaHotelMadinah { get; set; } = string.Empty;
    [MaxLength(100)] public string Maskapai { get; set; } = string.Empty;
    [MaxLength(200)] public string RutePenerbangan { get; set; } = string.Empty;
    public int DurasiHari { get; set; } = 9;
    public DateTime? TanggalBerangkat { get; set; }
    public DateTime? TanggalPulang { get; set; }
    [MaxLength(500)] public string BrosurUrl { get; set; } = string.Empty;
    [MaxLength(1000)] public string ItineraryJson { get; set; } = "[]";
    public int Kuota { get; set; } = 50; public int Terisi { get; set; }
    public bool IsActive { get; set; } = true; public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Pembayaran
{
    [Key] public int Id { get; set; }
    public int JamaahId { get; set; }
    [ForeignKey(nameof(JamaahId))] public Jamaah? Jamaah { get; set; }
    public int? PaketId { get; set; }
    [ForeignKey(nameof(PaketId))] public Paket? Paket { get; set; }
    public decimal TotalBiaya { get; set; } public decimal TotalDibayar { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    [MaxLength(100)] public string MetodePembayaran { get; set; } = string.Empty;
    public DateTime? TanggalJatuhTempo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Cicilan
{
    [Key] public int Id { get; set; }
    public int PembayaranId { get; set; }
    [ForeignKey(nameof(PembayaranId))] public Pembayaran? Pembayaran { get; set; }
    public decimal Jumlah { get; set; }
    public DateTime TanggalBayar { get; set; } = DateTime.UtcNow;
    [MaxLength(200)] public string BuktiBayarUrl { get; set; } = string.Empty;
    [MaxLength(100)] public string MetodePembayaran { get; set; } = string.Empty;
    [MaxLength(200)] public string? Catatan { get; set; }
    public bool Dikonfirmasi { get; set; }
}

public class Keberangkatan
{
    [Key] public int Id { get; set; }
    public int? PaketId { get; set; }
    [ForeignKey(nameof(PaketId))] public Paket? Paket { get; set; }
    [MaxLength(50)] public string KodeKeberangkatan { get; set; } = string.Empty;
    [MaxLength(100)] public string Maskapai { get; set; } = string.Empty;
    [MaxLength(50)] public string NoPenerbangan { get; set; } = string.Empty;
    [MaxLength(100)] public string BandaraAsal { get; set; } = string.Empty;
    [MaxLength(100)] public string BandaraTujuan { get; set; } = string.Empty;
    public DateTime? TanggalBerangkat { get; set; }
    public DateTime? TanggalTiba { get; set; }
    public DepartureStatus Status { get; set; } = DepartureStatus.Scheduled;
    [MaxLength(500)] public string Catatan { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatMessage
{
    [Key] public int Id { get; set; }
    public int SenderId { get; set; }
    [ForeignKey(nameof(SenderId))] public ApplicationUser? Sender { get; set; }
    public int? ReceiverId { get; set; }
    [ForeignKey(nameof(ReceiverId))] public ApplicationUser? Receiver { get; set; }
    [MaxLength(4000)] public string Message { get; set; } = string.Empty;
    [MaxLength(500)] public string? AttachmentUrl { get; set; }
    [MaxLength(200)] public string? AttachmentType { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class Pengumuman
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(300)] public string Judul { get; set; } = string.Empty;
    [MaxLength(4000)] public string Isi { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public UserRole? TargetRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notifikasi
{
    [Key] public int Id { get; set; }
    public int? UserId { get; set; }
    [MaxLength(300)] public string Judul { get; set; } = string.Empty;
    [MaxLength(1000)] public string Pesan { get; set; } = string.Empty;
    [MaxLength(50)] public string Tipe { get; set; } = "info";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MateriManasik
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(200)] public string Judul { get; set; } = string.Empty;
    [MaxLength(4000)] public string Konten { get; set; } = string.Empty;
    [MaxLength(500)] public string VideoUrl { get; set; } = string.Empty;
    [MaxLength(500)] public string ThumbnailUrl { get; set; } = string.Empty;
    [MaxLength(100)] public string Kategori { get; set; } = "Umum";
    public int Urutan { get; set; } public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Kuis
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(300)] public string Pertanyaan { get; set; } = string.Empty;
    [MaxLength(200)] public string PilihanA { get; set; } = string.Empty;
    [MaxLength(200)] public string PilihanB { get; set; } = string.Empty;
    [MaxLength(200)] public string PilihanC { get; set; } = string.Empty;
    [MaxLength(200)] public string PilihanD { get; set; } = string.Empty;
    [MaxLength(1)] public string JawabanBenar { get; set; } = "A";
    [MaxLength(1000)] public string Penjelasan { get; set; } = string.Empty;
    [MaxLength(100)] public string Kategori { get; set; } = "Umum";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SOSTrigger
{
    [Key] public int Id { get; set; }
    public int JamaahId { get; set; }
    [ForeignKey(nameof(JamaahId))] public Jamaah? Jamaah { get; set; }
    public double Latitude { get; set; } public double Longitude { get; set; }
    [MaxLength(1000)] public string Pesan { get; set; } = "SOS!";
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    [MaxLength(500)] public string? CatatanResolusi { get; set; }
}

public class KontakDarurat
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(100)] public string Nama { get; set; } = string.Empty;
    [MaxLength(20)] public string Telepon { get; set; } = string.Empty;
    [MaxLength(200)] public string Alamat { get; set; } = string.Empty;
    [MaxLength(100)] public string Peran { get; set; } = string.Empty;
    [MaxLength(200)] public string Catatan { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Produk
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(200)] public string NamaProduk { get; set; } = string.Empty;
    [MaxLength(100)] public string Kategori { get; set; } = "Perlengkapan";
    [MaxLength(4000)] public string Deskripsi { get; set; } = string.Empty;
    public decimal Harga { get; set; } public int Stok { get; set; }
    [MaxLength(500)] public string GambarUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CartItem
{
    [Key] public int Id { get; set; }
    public int UserId { get; set; }
    [ForeignKey(nameof(UserId))] public ApplicationUser? User { get; set; }
    public int ProdukId { get; set; }
    [ForeignKey(nameof(ProdukId))] public Produk? Produk { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    [Key] public int Id { get; set; }
    public int UserId { get; set; }
    [ForeignKey(nameof(UserId))] public ApplicationUser? User { get; set; }
    [MaxLength(50)] public string NoOrder { get; set; } = string.Empty;
    public decimal Total { get; set; }
    [MaxLength(50)] public string StatusOrder { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

public class OrderItem
{
    [Key] public int Id { get; set; }
    public int OrderId { get; set; }
    [ForeignKey(nameof(OrderId))] public Order? Order { get; set; }
    public int ProdukId { get; set; }
    [ForeignKey(nameof(ProdukId))] public Produk? Produk { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal HargaSatuan { get; set; }
}

public class ChatSession
{
    [Key] public int Id { get; set; }
    public int UserId { get; set; }
    [ForeignKey(nameof(UserId))] public ApplicationUser? User { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "New Chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public ICollection<ChatbotMessage> Messages { get; set; } = new List<ChatbotMessage>();
}

public class ChatbotMessage
{
    [Key] public int Id { get; set; }
    public int SessionId { get; set; }
    [ForeignKey(nameof(SessionId))] public ChatSession? Session { get; set; }
    [MaxLength(50)] public string Role { get; set; } = "user";
    [MaxLength(8000)] public string Content { get; set; } = string.Empty;
    [MaxLength(1000)] public string? ImageUrl { get; set; }
    [MaxLength(1000)] public string? AttachmentUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
