using Microsoft.EntityFrameworkCore;
using PCHub.Shared.Data;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Interfaces;
using PCHub.Shared.Models;

namespace PCHub.Shared.Services;

public class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var now = DateTime.UtcNow; var todayStart = now.Date; var monthStart = new DateTime(now.Year, now.Month, 1);
        var totalUsers = await db.Users.CountAsync();
        var activeUsers = await db.Users.CountAsync(u => u.LastLoginAt >= now.AddDays(-7));
        var totalPcs = await db.Pcs.CountAsync();
        var availablePcs = await db.Pcs.CountAsync(p => p.Status == PcStatus.Available);
        var activeSessions = await db.BillingSessions.CountAsync(b => b.Status == BillingStatus.Active);
        var pendingReservations = await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Pending);
        var todayRevenue = await db.BillingSessions.Where(b => b.EndTime >= todayStart && b.PaymentStatus == PaymentStatus.Completed).SumAsync(b => b.TotalCost);
        var monthRevenue = await db.BillingSessions.Where(b => b.EndTime >= monthStart && b.PaymentStatus == PaymentStatus.Completed).SumAsync(b => b.TotalCost);

        // Popular games: query PcSessions dulu, group by GameId, lalu join manual
        var sessions = await db.PcSessions.Include(s => s.Game).ToListAsync();
        var popularGames = sessions
            .Where(s => s.Game != null)
            .GroupBy(s => s.Game!.Name)
            .Select(g => new PopularGameStat(g.Key, g.Count(), g.Sum(s => s.DurationMinutes)))
            .OrderByDescending(g => g.PlayCount)
            .Take(10)
            .ToList();

        // If no PcSession data, fallback ke Games yang isPopular
        if (!popularGames.Any())
        {
            var popGames = await db.Games.Where(g => g.IsPopular).Take(5).ToListAsync();
            popularGames = popGames.Select(g => new PopularGameStat(g.Name, 0, 0)).ToList();
        }

        var revenueChart = new List<DailyRevenue>();
        for (int i = 6; i >= 0; i--)
        {
            var date = now.AddDays(-i).Date;
            var amount = await db.BillingSessions.Where(b => b.EndTime >= date && b.EndTime < date.AddDays(1) && b.PaymentStatus == PaymentStatus.Completed).SumAsync(b => b.TotalCost);
            revenueChart.Add(new DailyRevenue(date, amount));
        }

        return new DashboardStats(totalUsers, activeUsers, totalPcs, availablePcs, todayRevenue, monthRevenue, activeSessions, pendingReservations, popularGames, revenueChart);
    }
}

public class ReservationService(AppDbContext db) : IReservationService
{
    private static ReservationDto Map(Reservation r) => new(r.Id, r.UserId, r.User?.Username ?? "", r.PcId, r.Pc?.Name, r.ReservationDate, r.DurationMinutes, r.GameRequested, r.Status, r.Notes);
    public async Task<List<ReservationDto>> GetUserReservationsAsync(Guid userId) => await db.Reservations.Include(r => r.User).Include(r => r.Pc).Where(r => r.UserId == userId).OrderByDescending(r => r.ReservationDate).Select(r => Map(r)).ToListAsync();
    public async Task<PagedResult<ReservationDto>> GetAllReservationsPagedAsync(PagingRequest rq) { var q = db.Reservations.Include(x => x.User).Include(x => x.Pc).AsQueryable(); var total = await q.CountAsync(); var items = await q.OrderByDescending(x => x.ReservationDate).Skip((rq.Page - 1) * rq.PageSize).Take(rq.PageSize).Select(x => Map(x)).ToListAsync(); return new PagedResult<ReservationDto>(items, total, rq.Page, rq.PageSize); }
    public async Task<ReservationDto> CreateReservationAsync(Guid userId, CreateReservationRequest rq) { var res = new Reservation { UserId = userId, PcId = rq.PcId, ReservationDate = rq.ReservationDate, DurationMinutes = rq.DurationMinutes, GameRequested = rq.GameRequested, Notes = rq.Notes, CreatedAt = DateTime.UtcNow }; db.Reservations.Add(res); await db.SaveChangesAsync(); return Map(await db.Reservations.Include(x => x.User).Include(x => x.Pc).FirstAsync(x => x.Id == res.Id)); }
    public async Task<ReservationDto> UpdateReservationStatusAsync(Guid id, ReservationStatus status) { var r = await db.Reservations.Include(x => x.User).Include(x => x.Pc).FirstAsync(x => x.Id == id) ?? throw new KeyNotFoundException(); r.Status = status; await db.SaveChangesAsync(); return Map(r); }
    public async Task CancelReservationAsync(Guid id) { var r = await db.Reservations.FindAsync(id); if (r != null) { r.Status = ReservationStatus.Cancelled; await db.SaveChangesAsync(); } }
}

public class MembershipService(AppDbContext db) : IMembershipService
{
    public async Task<List<MembershipDto>> GetAllMembershipsAsync() => await db.Memberships.Select(m => new MembershipDto(m.Id, m.Name, m.Tier, m.Description, m.MonthlyPrice, m.DiscountPercentage, m.BonusHours, m.LoyaltyPointsPerMonth, m.IsActive)).ToListAsync();
    public async Task<MembershipDto> CreateMembershipAsync(MembershipCreateRequest rq) { var m = new Membership { Name = rq.Name, Tier = rq.Tier, Description = rq.Description, MonthlyPrice = rq.MonthlyPrice, DiscountPercentage = rq.DiscountPercentage, BonusHours = rq.BonusHours, LoyaltyPointsPerMonth = rq.LoyaltyPointsPerMonth, CreatedAt = DateTime.UtcNow }; db.Memberships.Add(m); await db.SaveChangesAsync(); return new MembershipDto(m.Id, m.Name, m.Tier, m.Description, m.MonthlyPrice, m.DiscountPercentage, m.BonusHours, m.LoyaltyPointsPerMonth, m.IsActive); }
    public async Task<UserMembership> SubscribeAsync(Guid userId, SubscribeMembershipRequest rq) { var m = await db.Memberships.FindAsync(rq.MembershipId) ?? throw new KeyNotFoundException(); var existing = await db.UserMemberships.Where(um => um.UserId == userId && um.IsActive).ToListAsync(); foreach (var e in existing) e.IsActive = false; var um = new UserMembership { UserId = userId, MembershipId = rq.MembershipId, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(rq.DurationMonths), IsActive = true }; db.UserMemberships.Add(um); if (await db.Users.FindAsync(userId) is User u) u.MembershipTier = m.Tier; await db.SaveChangesAsync(); return um; }
    public async Task CheckAndUpdateMembershipStatusAsync() { var expired = await db.UserMemberships.Where(um => um.IsActive && um.EndDate < DateTime.UtcNow).ToListAsync(); foreach (var e in expired) { e.IsActive = false; if (await db.Users.FindAsync(e.UserId) is User u) u.MembershipTier = MembershipTier.Basic; } if (expired.Any()) await db.SaveChangesAsync(); }
}

public class PromoService(AppDbContext db) : IPromoService
{
    public async Task<List<PromoDto>> GetActivePromosAsync() { var now = DateTime.UtcNow; return await db.Promos.Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now).Select(p => new PromoDto(p.Id, p.Name, p.Description, p.PromoCode, p.DiscountPercentage, p.MaxDiscount, p.StartDate, p.EndDate, p.IsActive)).ToListAsync(); }
    public async Task<PagedResult<PromoDto>> GetAllPromosPagedAsync(PagingRequest rq) { var q = db.Promos.AsQueryable(); var total = await q.CountAsync(); var items = await q.OrderByDescending(p => p.EndDate).Skip((rq.Page - 1) * rq.PageSize).Take(rq.PageSize).Select(p => new PromoDto(p.Id, p.Name, p.Description, p.PromoCode, p.DiscountPercentage, p.MaxDiscount, p.StartDate, p.EndDate, p.IsActive)).ToListAsync(); return new PagedResult<PromoDto>(items, total, rq.Page, rq.PageSize); }
    public async Task<PromoDto> CreatePromoAsync(PromoCreateRequest rq) { var p = new Promo { Name = rq.Name, Description = rq.Description, PromoCode = rq.PromoCode, DiscountPercentage = rq.DiscountPercentage, MaxDiscount = rq.MaxDiscount, StartDate = rq.StartDate, EndDate = rq.EndDate, CreatedAt = DateTime.UtcNow }; db.Promos.Add(p); await db.SaveChangesAsync(); return new PromoDto(p.Id, p.Name, p.Description, p.PromoCode, p.DiscountPercentage, p.MaxDiscount, p.StartDate, p.EndDate, p.IsActive); }
    public async Task<bool> ValidatePromoCodeAsync(string code) { var now = DateTime.UtcNow; return await db.Promos.AnyAsync(p => p.PromoCode == code && p.IsActive && p.StartDate <= now && p.EndDate >= now); }
}

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task SendNotificationAsync(SendNotificationRequest rq) { var n = new Notification { UserId = rq.UserId, Title = rq.Title, Message = rq.Message, Type = rq.Type, Channel = rq.Channel, CreatedAt = DateTime.UtcNow, SentAt = DateTime.UtcNow }; db.Notifications.Add(n); await db.SaveChangesAsync(); }
    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId) => await db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(50).Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.IsRead, n.CreatedAt)).ToListAsync();
    public async Task MarkAsReadAsync(Guid notificationId) { var n = await db.Notifications.FindAsync(notificationId); if (n != null) { n.IsRead = true; await db.SaveChangesAsync(); } }
    public async Task BroadcastAsync(string title, string message, NotificationType type) { var n = new Notification { Title = title, Message = message, Type = type, Channel = NotificationChannel.SignalR, CreatedAt = DateTime.UtcNow, SentAt = DateTime.UtcNow }; db.Notifications.Add(n); await db.SaveChangesAsync(); }
}

public class TournamentService(AppDbContext db) : ITournamentService
{
    public async Task<List<TournamentDto>> GetActiveTournamentsAsync() { var now = DateTime.UtcNow; return await db.Tournaments.Include(t => t.Game).Include(t => t.Participants).Where(t => t.IsActive && t.EndDate >= now).Select(t => new TournamentDto(t.Id, t.Name, t.Description, t.GameId, t.Game!.Name, t.StartDate, t.EndDate, t.MaxParticipants, t.EntryFee, t.PrizePool, t.IsActive, t.Participants.Count)).ToListAsync(); }
    public async Task<PagedResult<TournamentDto>> GetAllTournamentsPagedAsync(PagingRequest rq) { var q = db.Tournaments.Include(t => t.Game).Include(t => t.Participants).AsQueryable(); var total = await q.CountAsync(); var items = await q.OrderByDescending(t => t.StartDate).Skip((rq.Page - 1) * rq.PageSize).Take(rq.PageSize).Select(t => new TournamentDto(t.Id, t.Name, t.Description, t.GameId, t.Game!.Name, t.StartDate, t.EndDate, t.MaxParticipants, t.EntryFee, t.PrizePool, t.IsActive, t.Participants.Count)).ToListAsync(); return new PagedResult<TournamentDto>(items, total, rq.Page, rq.PageSize); }
    public async Task<TournamentDto> CreateTournamentAsync(TournamentCreateRequest rq) { var t = new Tournament { Name = rq.Name, Description = rq.Description, GameId = rq.GameId, StartDate = rq.StartDate, EndDate = rq.EndDate, MaxParticipants = rq.MaxParticipants, EntryFee = rq.EntryFee, PrizePool = rq.PrizePool, CreatedAt = DateTime.UtcNow }; db.Tournaments.Add(t); await db.SaveChangesAsync(); return new TournamentDto(t.Id, t.Name, t.Description, t.GameId, null, t.StartDate, t.EndDate, t.MaxParticipants, t.EntryFee, t.PrizePool, t.IsActive, 0); }
    public async Task JoinTournamentAsync(Guid userId, JoinTournamentRequest rq) { var t = await db.Tournaments.Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == rq.TournamentId) ?? throw new KeyNotFoundException(); if (t.Participants.Count >= t.MaxParticipants) throw new InvalidOperationException("Full"); db.TournamentParticipants.Add(new TournamentParticipant { TournamentId = rq.TournamentId, UserId = userId, RegisteredAt = DateTime.UtcNow }); await db.SaveChangesAsync(); }
    public async Task<TournamentBracketDto?> GetBracketAsync(Guid tournamentId) { var t = await db.Tournaments.Include(x => x.Participants).ThenInclude(p => p.User).FirstOrDefaultAsync(x => x.Id == tournamentId); if (t == null) return null; var participants = t.Participants.Select(p => p.User?.Username ?? "Unknown").ToList(); if (!participants.Any()) participants.AddRange(["Player 1", "Player 2", "Player 3", "Player 4"]); var bracketService = new TournamentBracketService(); return bracketService.GenerateBracket(t.Name, t.Id, participants, TournamentBracketType.SingleElimination); }
    public Task UpdateMatchResultAsync(Guid matchId, int score1, int score2) { return Task.CompletedTask; }
}
