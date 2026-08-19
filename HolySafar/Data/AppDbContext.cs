using Microsoft.EntityFrameworkCore;
using HolySafar.Models;

namespace HolySafar.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Jamaah> Jamaah => Set<Jamaah>();
    public DbSet<DokumenJamaah> DokumenJamaah => Set<DokumenJamaah>();
    public DbSet<Paket> Paket => Set<Paket>();
    public DbSet<Pembayaran> Pembayaran => Set<Pembayaran>();
    public DbSet<Cicilan> Cicilan => Set<Cicilan>();
    public DbSet<Keberangkatan> Keberangkatan => Set<Keberangkatan>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Pengumuman> Pengumuman => Set<Pengumuman>();
    public DbSet<Notifikasi> Notifikasi => Set<Notifikasi>();
    public DbSet<MateriManasik> MateriManasik => Set<MateriManasik>();
    public DbSet<Kuis> Kuis => Set<Kuis>();
    public DbSet<SOSTrigger> SOSTriggers => Set<SOSTrigger>();
    public DbSet<KontakDarurat> KontakDarurat => Set<KontakDarurat>();
    public DbSet<Produk> Produk => Set<Produk>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatbotMessage> ChatbotMessages => Set<ChatbotMessage>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<ItineraryItem> ItineraryItems => Set<ItineraryItem>();
    public DbSet<ForumTopik> ForumTopik => Set<ForumTopik>();
    public DbSet<ForumBalasan> ForumBalasan => Set<ForumBalasan>();
    public DbSet<Asuransi> Asuransi => Set<Asuransi>();
    public DbSet<PengaturanAplikasi> Pengaturan => Set<PengaturanAplikasi>();
    public DbSet<SiskohatLog> SiskohatLogs => Set<SiskohatLog>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === ChatMessage -> Sender & Receiver ===
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Receiver).WithMany().HasForeignKey(m => m.ReceiverId)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

        // === Order -> OrderItem ===
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);

        // === ChatSession -> ChatbotMessage ===
        modelBuilder.Entity<ChatSession>()
            .HasMany(s => s.Messages).WithOne(m => m.Session).HasForeignKey(m => m.SessionId);

        // === Paket -> ItineraryItem ===
        modelBuilder.Entity<Paket>()
            .HasMany(p => p.Itinerary).WithOne(i => i.Paket).HasForeignKey(i => i.PaketId)
            .OnDelete(DeleteBehavior.Cascade);

        // === ForumTopik -> ForumBalasan ===
        modelBuilder.Entity<ForumTopik>()
            .HasMany(t => t.Balasan).WithOne(b => b.Topik).HasForeignKey(b => b.TopikId)
            .OnDelete(DeleteBehavior.Cascade);

        // === Indexes ===
        modelBuilder.Entity<ApplicationUser>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Jamaah>().HasIndex(j => j.Nik);
        modelBuilder.Entity<Pembayaran>().HasIndex(p => p.Status);
        modelBuilder.Entity<SOSTrigger>().HasIndex(s => s.IsResolved);
        modelBuilder.Entity<PengaturanAplikasi>().HasIndex(p => p.Kunci).IsUnique();
        modelBuilder.Entity<PaymentTransaction>().HasIndex(t => t.KodeTransaksi).IsUnique();
        modelBuilder.Entity<PaymentTransaction>().HasIndex(t => new { t.ReferenceType, t.ReferenceId });
        modelBuilder.Entity<ItineraryItem>().HasIndex(i => new { i.PaketId, i.Hari, i.Urutan });
    }
}
