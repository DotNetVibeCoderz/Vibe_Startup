using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Bioskop.Models;

namespace Bioskop.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Studio> Studios => Set<Studio>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Snack> Snacks => Set<Snack>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OrderSnack> OrderSnacks => Set<OrderSnack>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<MovieRating> MovieRatings => Set<MovieRating>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TrafficLog> TrafficLogs => Set<TrafficLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Movie>(e =>
        {
            e.HasIndex(m => m.Title);
            e.HasIndex(m => m.IsNowPlaying);
        });

        builder.Entity<Seat>(e =>
        {
            e.HasOne(s => s.Studio)
                .WithMany(st => st.Seats)
                .HasForeignKey(s => s.StudioId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.StudioId, s.RowLabel, s.ColumnNumber }).IsUnique();
        });

        builder.Entity<Showtime>(e =>
        {
            e.HasOne(s => s.Movie)
                .WithMany(m => m.Showtimes)
                .HasForeignKey(s => s.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.Studio)
                .WithMany(st => st.Showtimes)
                .HasForeignKey(s => s.StudioId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(s => new { s.StudioId, s.StartTime });
        });

        // Order -> Showtime relationship (fix: Showtime.Tickets is ICollection<Ticket> not ICollection<Order>)
        builder.Entity<Order>(e =>
        {
            e.HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(o => o.Showtime)
                .WithMany()
                .HasForeignKey(o => o.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.UserId);
        });

        builder.Entity<Ticket>(e =>
        {
            e.HasOne(t => t.Order)
                .WithMany(o => o.Tickets)
                .HasForeignKey(t => t.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(t => t.Seat)
                .WithMany(s => s.Tickets)
                .HasForeignKey(t => t.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(t => t.Showtime)
                .WithMany(st => st.Tickets)
                .HasForeignKey(t => t.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(t => t.TicketNumber).IsUnique();
            e.HasIndex(t => t.QrCode).IsUnique();
        });

        builder.Entity<Payment>(e =>
        {
            e.HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderSnack>(e =>
        {
            e.HasOne(os => os.Order)
                .WithMany(o => o.OrderSnacks)
                .HasForeignKey(os => os.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(os => os.Snack)
                .WithMany(s => s.OrderSnacks)
                .HasForeignKey(os => os.SnackId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Post>(e =>
        {
            e.HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.CreatedAt).IsDescending();
            e.HasIndex(p => p.IsDeleted);
        });

        builder.Entity<Comment>(e =>
        {
            e.HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Like>(e =>
        {
            e.HasOne(l => l.Post)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.User)
                .WithMany(u => u.Likes)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();
        });

        builder.Entity<MovieRating>(e =>
        {
            e.HasOne(r => r.Movie)
                .WithMany(m => m.Ratings)
                .HasForeignKey(r => r.MovieId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.User)
                .WithMany(u => u.Ratings)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.MovieId, r.UserId }).IsUnique();
        });

        builder.Entity<ChatSession>(e =>
        {
            e.HasOne(cs => cs.User)
                .WithMany()
                .HasForeignKey(cs => cs.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChatMessage>(e =>
        {
            e.HasOne(cm => cm.ChatSession)
                .WithMany(cs => cs.Messages)
                .HasForeignKey(cm => cm.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedAt).IsDescending();
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.Action);
        });

        builder.Entity<TrafficLog>(e =>
        {
            e.HasIndex(t => t.CreatedAt).IsDescending();
            e.HasIndex(t => t.StatusCode);
        });
    }
}
