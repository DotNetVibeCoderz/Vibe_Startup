// Database Context - supports SQLite, SQLServer, MySQL, PostgreSQL
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Joka.Models.Common;
using Joka.Models.Users;
using Joka.Models.Flights;
using Joka.Models.Trains;
using Joka.Models.Buses;
using Joka.Models.Backoffice;
using Joka.Models.Hotels;
using Joka.Models.Payments;
using Joka.Models.Chat;
using Joka.Models.Activities;
using Joka.Models.Support;
using Joka.Models.Transport;

namespace Joka.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Users
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    // Flights
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Airline> Airlines => Set<Airline>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<FlightBooking> FlightBookings => Set<FlightBooking>();

    // Trains
    public DbSet<TrainStation> TrainStations => Set<TrainStation>();
    public DbSet<Train> Trains => Set<Train>();
    public DbSet<TrainSchedule> TrainSchedules => Set<TrainSchedule>();
    public DbSet<TrainBooking> TrainBookings => Set<TrainBooking>();

    // Buses & shuttles
    public DbSet<BusTerminal> BusTerminals => Set<BusTerminal>();
    public DbSet<BusOperator> BusOperators => Set<BusOperator>();
    public DbSet<BusService> BusServices => Set<BusService>();
    public DbSet<BusSchedule> BusSchedules => Set<BusSchedule>();
    public DbSet<BusBooking> BusBookings => Set<BusBooking>();

    // Hotels
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<HotelBooking> HotelBookings => Set<HotelBooking>();
    public DbSet<HotelReview> HotelReviews => Set<HotelReview>();

    // Payments & Related
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PromoVoucher> PromoVouchers => Set<PromoVoucher>();
    public DbSet<UserVoucher> UserVouchers => Set<UserVoucher>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<TravelInsurance> TravelInsurances => Set<TravelInsurance>();
    public DbSet<CarRental> CarRentals => Set<CarRental>();
    public DbSet<CarRentalBooking> CarRentalBookings => Set<CarRentalBooking>();
    public DbSet<TravelPackage> TravelPackages => Set<TravelPackage>();
    public DbSet<TravelPackageBooking> TravelPackageBookings => Set<TravelPackageBooking>();

    // Chat
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();

    // Activities
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityBooking> ActivityBookings => Set<ActivityBooking>();

    // Back office (Admin / Operator / Merchant)
    // Local transport (ojek & airport transfer)
    public DbSet<TransportProvider> TransportProviders => Set<TransportProvider>();
    public DbSet<TransportOption> TransportOptions => Set<TransportOption>();
    public DbSet<TransportBooking> TransportBookings => Set<TransportBooking>();

    // Live agent support
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantSettlement> MerchantSettlements => Set<MerchantSettlement>();
    public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApiIntegration> ApiIntegrations => Set<ApiIntegration>();
    public DbSet<SystemHealthCheck> SystemHealthChecks => Set<SystemHealthCheck>();

    // Config
    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite has no native decimal type and refuses to ORDER BY one, which
        // breaks every "sort by price" query. Mapping money to double keeps the
        // sort in SQL instead of pulling rows into memory to order them.
        // Note: changing this changes the column type, so an existing
        // Data/joka.db must be deleted - this project has no migrations.
        if (Database.IsSqlite())
        {
            var decimalToDouble = new ValueConverter<decimal, double>(v => (double)v, v => (decimal)v);
            var nullableDecimalToDouble = new ValueConverter<decimal?, double?>(v => (double?)v, v => (decimal?)v);

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(entity => entity.GetProperties()))
            {
                if (property.ClrType == typeof(decimal))
                    property.SetValueConverter(decimalToDouble);
                else if (property.ClrType == typeof(decimal?))
                    property.SetValueConverter(nullableDecimalToDouble);
            }
        }

        // Global query filter for soft delete
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Flight>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Hotel>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Airport>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Airline>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Train>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TrainStation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Activity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BusTerminal>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BusOperator>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BusService>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CarRental>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TravelPackage>().HasQueryFilter(e => !e.IsDeleted);

        // Indexes for performance
        modelBuilder.Entity<Flight>().HasIndex(f => new { f.DepartureAirportId, f.ArrivalAirportId, f.DepartureTime });
        modelBuilder.Entity<Hotel>().HasIndex(h => new { h.City, h.IsActive });
        modelBuilder.Entity<TrainSchedule>().HasIndex(t => new { t.DepartureStationId, t.ArrivalStationId, t.DepartureTime });
        modelBuilder.Entity<BusSchedule>().HasIndex(b => new { b.DepartureTerminalId, b.ArrivalTerminalId, b.DepartureTime });
        modelBuilder.Entity<BusBooking>().HasIndex(b => b.BookingCode).IsUnique();
        modelBuilder.Entity<PaymentTransaction>().HasIndex(p => p.TransactionCode).IsUnique();
        modelBuilder.Entity<FlightBooking>().HasIndex(b => b.BookingCode).IsUnique();
        modelBuilder.Entity<HotelBooking>().HasIndex(b => b.BookingCode).IsUnique();
        modelBuilder.Entity<ChatSession>().HasIndex(c => new { c.UserId, c.IsActive });
        modelBuilder.Entity<PromoVoucher>().HasIndex(v => v.Code).IsUnique();
        
        // User email unique
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // Back office
        modelBuilder.Entity<Merchant>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Merchant>().HasIndex(m => m.Code).IsUnique();
        modelBuilder.Entity<MerchantSettlement>().HasIndex(s => s.ReferenceNo).IsUnique();
        modelBuilder.Entity<FraudAlert>().HasIndex(f => new { f.Status, f.Severity });
        modelBuilder.Entity<RefundRequest>().HasIndex(r => new { r.Status, r.BookingCode });
        modelBuilder.Entity<ApprovalRequest>().HasIndex(a => new { a.Status, a.MerchantId });
        modelBuilder.Entity<IncidentReport>().HasIndex(i => new { i.Status, i.Severity });

        // Support: the queue is read by status + age on every operator refresh,
        // and the code has to stay quotable-and-unique.
        modelBuilder.Entity<SupportTicket>().HasIndex(t => t.TicketCode).IsUnique();
        modelBuilder.Entity<SupportTicket>().HasIndex(t => new { t.Status, t.LastMessageAt });
        modelBuilder.Entity<SupportMessage>().HasIndex(m => new { m.SupportTicketId, m.SentAt });

        // Reviews: the moderation queue and the public list both filter on status.
        modelBuilder.Entity<HotelReview>().HasIndex(r => new { r.HotelId, r.Status });

        // Local transport. Soft-deletable catalog entities get the same filter
        // as the rest of the catalog; bookings deliberately do not.
        modelBuilder.Entity<TransportProvider>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TransportOption>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TransportOption>().HasIndex(o => new { o.City, o.ServiceType, o.IsActive });
        modelBuilder.Entity<TransportBooking>().HasIndex(b => b.BookingCode).IsUnique();
    }
}
