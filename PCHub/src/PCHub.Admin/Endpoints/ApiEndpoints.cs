using Microsoft.EntityFrameworkCore;
using PCHub.Shared.Data;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Interfaces;
using PCHub.Shared.Services;

namespace PCHub.Admin.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // === AUTH ===
        api.MapPost("/auth/login", async (LoginRequest r, IAuthService a) =>
        { var x = await a.LoginAsync(r); return x == null ? Results.Unauthorized() : Results.Ok(x); }).WithTags("Auth");
        api.MapPost("/auth/register", async (RegisterRequest r, IAuthService a) =>
        { var x = await a.RegisterAsync(r); return x == null ? Results.Conflict("Exists") : Results.Ok(x); }).WithTags("Auth");
        api.MapGet("/auth/profile/{userId:guid}", async (Guid userId, IAuthService a) =>
        { var x = await a.GetProfileAsync(userId); return x == null ? Results.NotFound() : Results.Ok(x); }).WithTags("Auth");

        // === DASHBOARD ===
        api.MapGet("/dashboard/stats", async (IDashboardService d) => Results.Ok(await d.GetDashboardStatsAsync())).WithTags("Dashboard");

        // === PCs ===
        api.MapGet("/pcs", async (int? page, int? pageSize, string? search, string? sortBy, bool? sortDesc, IPcService s) =>
            Results.Ok(await s.GetPcsPagedAsync(new PagingRequest(page > 0 ? page.Value : 1, pageSize > 0 ? pageSize.Value : 100, search, sortBy, sortDesc ?? false)))).WithTags("PCs");
        api.MapGet("/pcs/{id:guid}", async (Guid id, IPcService s) => { var x = await s.GetPcByIdAsync(id); return x == null ? Results.NotFound() : Results.Ok(x); }).WithTags("PCs");
        api.MapPost("/pcs", async (PcCreateRequest r, IPcService s) => Results.Created("/api/pcs/", await s.CreatePcAsync(r))).WithTags("PCs");
        api.MapPut("/pcs", async (PcUpdateRequest r, IPcService s) => Results.Ok(await s.UpdatePcAsync(r))).WithTags("PCs");
        api.MapDelete("/pcs/{id:guid}", async (Guid id, IPcService s) => { await s.DeletePcAsync(id); return Results.NoContent(); }).WithTags("PCs");
        api.MapPut("/pcs/{id:guid}/resources", async (Guid id, double cpu, double gpu, double ram, IPcService s) => Results.Ok(await s.UpdatePcResourceAsync(id, cpu, gpu, ram))).WithTags("PCs");

        // === GAMES ===
        api.MapGet("/games", async (int? page, int? pageSize, string? search, IGameService s) =>
            Results.Ok(await s.GetGamesPagedAsync(new PagingRequest(page > 0 ? page.Value : 1, pageSize > 0 ? pageSize.Value : 100, search)))).WithTags("Games");
        api.MapPost("/games", async (GameCreateRequest r, IGameService s) => Results.Created("/api/games/", await s.CreateGameAsync(r))).WithTags("Games");
        api.MapPut("/games", async (GameUpdateRequest r, IGameService s) => Results.Ok(await s.UpdateGameAsync(r))).WithTags("Games");
        api.MapDelete("/games/{id:guid}", async (Guid id, IGameService s) => { await s.DeleteGameAsync(id); return Results.NoContent(); }).WithTags("Games");

        // === BILLING ===
        api.MapGet("/billing", async (int? page, int? pageSize, string? search, bool? sortDesc, IBillingService s) =>
            Results.Ok(await s.GetAllBillingPagedAsync(new PagingRequest(page > 0 ? page.Value : 1, pageSize > 0 ? pageSize.Value : 10, search, null, sortDesc ?? false)))).WithTags("Billing");
        api.MapPost("/billing/start", async (StartBillingRequest r, IBillingService s) => Results.Ok(await s.StartBillingAsync(r))).WithTags("Billing");
        api.MapPost("/billing/stop/{id:guid}", async (Guid id, IBillingService s) => Results.Ok(await s.StopBillingAsync(id))).WithTags("Billing");
        api.MapGet("/billing/active/{userId:guid}", async (Guid userId, IBillingService s) => { var x = await s.GetActiveBillingAsync(userId); return x == null ? Results.NoContent() : Results.Ok(x); }).WithTags("Billing");
        api.MapGet("/billing/history/{userId:guid}", async (Guid userId, IBillingService s) => Results.Ok(await s.GetUserBillingHistoryAsync(userId))).WithTags("Billing");

        // === RESERVATIONS ===
        api.MapGet("/reservations", async (int? page, int? pageSize, IReservationService s) =>
            Results.Ok(await s.GetAllReservationsPagedAsync(new PagingRequest(page > 0 ? page.Value : 1, pageSize > 0 ? pageSize.Value : 50)))).WithTags("Reservations");
        api.MapGet("/reservations/user/{userId:guid}", async (Guid userId, IReservationService s) =>
            Results.Ok(await s.GetUserReservationsAsync(userId))).WithTags("Reservations");
        api.MapPost("/reservations", async (Guid userId, CreateReservationRequest r, IReservationService s) =>
            Results.Ok(await s.CreateReservationAsync(userId, r))).WithTags("Reservations");
        api.MapPut("/reservations/{id:guid}/confirm", async (Guid id, IReservationService s) =>
            Results.Ok(await s.UpdateReservationStatusAsync(id, ReservationStatus.Confirmed))).WithTags("Reservations");
        api.MapPut("/reservations/{id:guid}/cancel", async (Guid id, IReservationService s) =>
        { await s.CancelReservationAsync(id); return Results.NoContent(); }).WithTags("Reservations");

        // === MEMBERSHIPS ===
        api.MapGet("/memberships", async (IMembershipService s) => Results.Ok(await s.GetAllMembershipsAsync())).WithTags("Memberships");
        api.MapPost("/memberships/subscribe", async (Guid userId, SubscribeMembershipRequest r, IMembershipService s) => Results.Ok(await s.SubscribeAsync(userId, r))).WithTags("Memberships");

        // === PROMOS ===
        api.MapGet("/promos", async (int? page, int? pageSize, IPromoService s) =>
            Results.Ok(await s.GetAllPromosPagedAsync(new PagingRequest(page > 0 ? page.Value : 1, pageSize > 0 ? pageSize.Value : 20)))).WithTags("Promos");
        api.MapGet("/promos/validate", async (string code, IPromoService s) => Results.Ok(await s.ValidatePromoCodeAsync(code))).WithTags("Promos");

        // === TOURNAMENTS ===
        api.MapGet("/tournaments", async (int? page, int? pageSize, ITournamentService s) =>
            Results.Ok(await s.GetAllTournamentsPagedAsync(new PagingRequest(page > 0 ? page.Value : 1, pageSize > 0 ? pageSize.Value : 10)))).WithTags("Tournaments");
        api.MapPost("/tournaments/join", async (Guid userId, JoinTournamentRequest r, ITournamentService s) => { await s.JoinTournamentAsync(userId, r); return Results.Ok(); }).WithTags("Tournaments");
        api.MapGet("/tournaments/{id:guid}/bracket", async (Guid id, ITournamentService s) => { var x = await s.GetBracketAsync(id); return x == null ? Results.NotFound() : Results.Ok(x); }).WithTags("Tournaments");

        // === CHAT ===
        api.MapGet("/chat/sessions", async (IChatBotService c) => Results.Ok(await c.GetSessionsAsync())).WithTags("ChatBot");
        api.MapPost("/chat/sessions", async (CreateChatSessionRequest r, IChatBotService c) => Results.Ok(await c.CreateSessionAsync(r.Title))).WithTags("ChatBot");
        api.MapDelete("/chat/sessions/{id:guid}", async (Guid id, IChatBotService c) => { await c.DeleteSessionAsync(id); return Results.NoContent(); }).WithTags("ChatBot");
        api.MapGet("/chat/sessions/{id:guid}/messages", async (Guid id, IChatBotService c) => Results.Ok(await c.GetSessionMessagesAsync(id))).WithTags("ChatBot");
        api.MapPost("/chat/send", async (SendChatMessageRequest r, IChatBotService c) => Results.Ok(await c.SendMessageAsync(r))).WithTags("ChatBot");

        // === NOTIFICATIONS ===
        api.MapGet("/notifications/{userId:guid}", async (Guid userId, INotificationService s) => Results.Ok(await s.GetUserNotificationsAsync(userId))).WithTags("Notifications");
        api.MapPut("/notifications/{id:guid}/read", async (Guid id, INotificationService s) => { await s.MarkAsReadAsync(id); return Results.NoContent(); }).WithTags("Notifications");

        // === PAYMENT, EMAIL, IoT, CACHE, SETTINGS ===
        api.MapPost("/payments/create", async (CreatePaymentRequest r, IPaymentService s) => Results.Ok(await s.CreatePaymentAsync(r))).WithTags("Payments");
        api.MapGet("/payments/status/{transactionId}", async (string transactionId, IPaymentService s) => Results.Ok(await s.CheckPaymentStatusAsync(transactionId))).WithTags("Payments");
        api.MapPost("/email/send", async (EmailRequest r, IEmailService s) => { var ok = await s.SendEmailAsync(r); return ok ? Results.Ok("Sent") : Results.Problem("Failed"); }).WithTags("Email");
        api.MapGet("/iot/devices", async (IoTService iot) => Results.Ok(await iot.GetDevicesAsync())).WithTags("IoT");
        api.MapPost("/iot/command", async (IoTCommandRequest r, IoTService iot) => { var ok = await iot.SendCommandAsync(r); return ok ? Results.Ok() : Results.NotFound(); }).WithTags("IoT");
        api.MapGet("/cache/stats", async (ICacheService c) => Results.Ok(await c.GetStatsAsync())).WithTags("System");

        api.MapPost("/config/update", async (UpdateConfigRequest r, AppDbContext db) =>
        {
            var cfg = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == r.Key);
            if (cfg != null) { cfg.Value = r.Value; cfg.UpdatedAt = DateTime.UtcNow; }
            else db.SystemConfigs.Add(new PCHub.Shared.Models.SystemConfig { Key = r.Key, Value = r.Value, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return Results.Ok();
        }).WithTags("Settings");
    }
}
