using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using JuraganKost.Data.Models;

namespace JuraganKost.Data.Context;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Kost> Kost => Set<Kost>();
    public DbSet<Kamar> Kamar => Set<Kamar>();
    public DbSet<Penghuni> Penghuni => Set<Penghuni>();
    public DbSet<Kontrak> Kontrak => Set<Kontrak>();
    public DbSet<Tagihan> Tagihan => Set<Tagihan>();
    public DbSet<Pembayaran> Pembayaran => Set<Pembayaran>();
    public DbSet<Komplain> Komplain => Set<Komplain>();
    public DbSet<InventarisItem> Inventaris => Set<InventarisItem>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Review> Review => Set<Review>();
    public DbSet<Notifikasi> Notifikasi => Set<Notifikasi>();
    public DbSet<IoTSensorData> IoTSensorData => Set<IoTSensorData>();
    public DbSet<MarketplaceListing> MarketplaceListing => Set<MarketplaceListing>();
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<ChatMessageDb> ChatMessages => Set<ChatMessageDb>();
    public DbSet<GambarKost> GambarKost => Set<GambarKost>();
    public DbSet<GambarKamar> GambarKamar => Set<GambarKamar>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Kost>(e => { e.HasKey(x => x.Id); e.Property(x => x.Nama).HasMaxLength(200).IsRequired(); e.Property(x => x.Alamat).HasMaxLength(500).IsRequired(); e.HasOne(x => x.Pemilik).WithMany(u => u.KostMilik).HasForeignKey(x => x.PemilikId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<Kamar>(e => { e.HasKey(x => x.Id); e.Property(x => x.NomorKamar).HasMaxLength(50).IsRequired(); e.HasOne(x => x.Kost).WithMany(k => k.Kamar).HasForeignKey(x => x.KostId).OnDelete(DeleteBehavior.Cascade); e.HasIndex(x => new { x.KostId, x.NomorKamar }).IsUnique(); });
        builder.Entity<Penghuni>(e => { e.HasKey(x => x.Id); e.Property(x => x.NamaLengkap).HasMaxLength(200).IsRequired(); e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull); e.HasOne(x => x.Kamar).WithMany(k => k.Penghuni).HasForeignKey(x => x.KamarId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<Kontrak>(e => { e.HasKey(x => x.Id); e.Property(x => x.NomorKontrak).HasMaxLength(100).IsRequired(); e.HasOne(x => x.Penghuni).WithMany(p => p.Kontrak).HasForeignKey(x => x.PenghuniId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Kamar).WithMany(k => k.Kontrak).HasForeignKey(x => x.KamarId).OnDelete(DeleteBehavior.NoAction); });
        builder.Entity<Tagihan>(e => { e.HasKey(x => x.Id); e.Property(x => x.NomorTagihan).HasMaxLength(100).IsRequired(); e.HasOne(x => x.Penghuni).WithMany().HasForeignKey(x => x.PenghuniId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Kamar).WithMany(k => k.Tagihan).HasForeignKey(x => x.KamarId).OnDelete(DeleteBehavior.SetNull); e.HasOne(x => x.Kontrak).WithMany().HasForeignKey(x => x.KontrakId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<Pembayaran>(e => { e.HasKey(x => x.Id); e.Property(x => x.NomorPembayaran).HasMaxLength(100).IsRequired(); e.HasOne(x => x.Penghuni).WithMany(p => p.Pembayaran).HasForeignKey(x => x.PenghuniId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Tagihan).WithMany().HasForeignKey(x => x.TagihanId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<Komplain>(e => { e.HasKey(x => x.Id); e.Property(x => x.NomorKomplain).HasMaxLength(100).IsRequired(); e.HasOne(x => x.Penghuni).WithMany(p => p.Komplain).HasForeignKey(x => x.PenghuniId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Kamar).WithMany().HasForeignKey(x => x.KamarId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<InventarisItem>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.Kost).WithMany(k => k.Inventaris).HasForeignKey(x => x.KostId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Kamar).WithMany().HasForeignKey(x => x.KamarId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<Staff>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.Kost).WithMany(k => k.Staff).HasForeignKey(x => x.KostId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull); });
        builder.Entity<Review>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.Kost).WithMany().HasForeignKey(x => x.KostId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Penghuni).WithMany(p => p.Review).HasForeignKey(x => x.PenghuniId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<Notifikasi>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<IoTSensorData>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.Kamar).WithMany().HasForeignKey(x => x.KamarId).OnDelete(DeleteBehavior.SetNull); e.HasIndex(x => new { x.DeviceId, x.Timestamp }); });
        builder.Entity<MarketplaceListing>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.Kost).WithMany().HasForeignKey(x => x.KostId).OnDelete(DeleteBehavior.Cascade); e.HasIndex(x => x.KostId).IsUnique(); });

        // Image tables
        builder.Entity<GambarKost>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.Kost).WithMany().HasForeignKey(x => x.KostId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<GambarKamar>(e => { e.HasKey(x => x.Id); e.HasOne(x => x.Kamar).WithMany().HasForeignKey(x => x.KamarId).OnDelete(DeleteBehavior.Cascade); });

        // Chat
        builder.Entity<ChatThread>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.SessionId).IsUnique(); e.HasMany(x => x.Messages).WithOne(m => m.Thread).HasForeignKey(m => m.ChatThreadId).OnDelete(DeleteBehavior.Cascade); });
        builder.Entity<ChatMessageDb>(e => { e.HasKey(x => x.Id); e.Property(x => x.Content).HasMaxLength(8000).IsRequired(); e.HasIndex(x => new { x.ChatThreadId, x.Timestamp }); });
    }
}
