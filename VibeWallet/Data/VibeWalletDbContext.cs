using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VibeWallet.Models;

namespace VibeWallet.Data;

public class VibeWalletDbContext : IdentityDbContext<VibeUser, IdentityRole<Guid>, Guid>
{
    public VibeWalletDbContext(DbContextOptions<VibeWalletDbContext> options) : base(options) { }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<BalanceHistory> BalanceHistories => Set<BalanceHistory>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<KycSelfie> KycSelfies => Set<KycSelfie>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<SupportedBank> SupportedBanks => Set<SupportedBank>();
    public DbSet<BankTransfer> BankTransfers => Set<BankTransfer>();
    public DbSet<QrisPayment> QrisPayments => Set<QrisPayment>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<MobileTopUp> MobileTopUps => Set<MobileTopUp>();
    public DbSet<EcommercePayment> EcommercePayments => Set<EcommercePayment>();
    public DbSet<P2PTransfer> P2PTransfers => Set<P2PTransfer>();
    public DbSet<SplitBill> SplitBills => Set<SplitBill>();
    public DbSet<SplitBillParticipant> SplitBillParticipants => Set<SplitBillParticipant>();
    public DbSet<SavedContact> SavedContacts => Set<SavedContact>();
    public DbSet<Cashback> Cashbacks => Set<Cashback>();
    public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<UserVoucher> UserVouchers => Set<UserVoucher>();
    public DbSet<Promo> Promos => Set<Promo>();
    public DbSet<SavingsAccount> SavingsAccounts => Set<SavingsAccount>();
    public DbSet<SavingsTransaction> SavingsTransactions => Set<SavingsTransaction>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<InsuranceProduct> InsuranceProducts => Set<InsuranceProduct>();
    public DbSet<UserInsurance> UserInsurances => Set<UserInsurance>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>();
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();

    public static void ConfigureDatabase(DbContextOptionsBuilder options, IConfiguration config)
    {
        var p = config.GetConnectionString("Provider") ?? "SQLite";
        var cs = p switch { "SQLServer" => config.GetConnectionString("SQLServer"), "MySQL" => config.GetConnectionString("MySQL"), "Postgre" => config.GetConnectionString("Postgre"), _ => config.GetConnectionString("SQLite") };
        switch (p)
        {
            case "SQLServer": options.UseSqlServer(cs); break;
            case "MySQL": options.UseMySql(cs, ServerVersion.AutoDetect(cs)); break;
            case "Postgre": options.UseNpgsql(cs); break;
            default: options.UseSqlite(cs); break;
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<VibeUser>(e => { e.HasIndex(u => u.PhoneNumber).IsUnique(); e.HasIndex(u => u.IdentityNumber).IsUnique().HasFilter("[IdentityNumber] IS NOT NULL"); });
        b.Entity<Wallet>(e => { e.HasIndex(w => w.WalletNumber).IsUnique(); e.HasIndex(w => w.UserId).IsUnique(); e.HasOne(w => w.User).WithOne(u => u.Wallet).HasForeignKey<Wallet>(w => w.UserId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<WalletTransaction>(e => { e.HasIndex(t => t.TransactionRef).IsUnique(); e.HasOne(t => t.Wallet).WithMany(w => w.Transactions).HasForeignKey(t => t.WalletId).OnDelete(DeleteBehavior.Restrict); e.HasOne(t => t.User).WithMany(u => u.Transactions).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<P2PTransfer>(e => { e.HasIndex(t => t.TransferRef).IsUnique(); e.HasOne(t => t.Sender).WithMany().HasForeignKey(t => t.SenderUserId).OnDelete(DeleteBehavior.Restrict); e.HasOne(t => t.Receiver).WithMany().HasForeignKey(t => t.ReceiverUserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<SplitBill>(e => e.HasOne(s => s.Creator).WithMany().HasForeignKey(s => s.CreatorUserId).OnDelete(DeleteBehavior.Restrict));
        b.Entity<SplitBillParticipant>(e => { e.HasOne(p => p.SplitBill).WithMany(s => s.Participants).HasForeignKey(p => p.SplitBillId).OnDelete(DeleteBehavior.Cascade); e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<ChatMessage>(e => { e.HasOne(m => m.ChatSession).WithMany(s => s.Messages).HasForeignKey(m => m.ChatSessionId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<ChatAttachment>(e => e.HasOne(a => a.ChatMessage).WithMany(m => m.Attachments).HasForeignKey(a => a.ChatMessageId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<SavingsAccount>(e => { e.HasIndex(s => s.AccountNumber).IsUnique(); });
        b.Entity<Voucher>(e => e.HasIndex(v => v.VoucherCode).IsUnique());
        b.Entity<SavedContact>(e => { e.HasIndex(c => new { c.UserId, c.WalletNumber }).IsUnique(); });
    }

    public override int SaveChanges() { HandleSoftDelete(); UpdateTimestamps(); return base.SaveChanges(); }
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default) { HandleSoftDelete(); UpdateTimestamps(); return await base.SaveChangesAsync(ct); }
    private void HandleSoftDelete() { foreach (var e in ChangeTracker.Entries().Where(x => x.State == EntityState.Deleted && x.Entity is ISoftDelete)) { e.State = EntityState.Modified; var d = (ISoftDelete)e.Entity; d.IsDeleted = true; d.DeletedAt = DateTime.UtcNow; } }
    private void UpdateTimestamps() { foreach (var e in ChangeTracker.Entries().Where(x => x.State == EntityState.Modified && x.Entity is BaseEntity)) { ((BaseEntity)e.Entity).UpdatedAt = DateTime.UtcNow; } }
}
