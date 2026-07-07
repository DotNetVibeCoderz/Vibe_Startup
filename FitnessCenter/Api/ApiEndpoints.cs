using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;
using FitnessCenter.Services;
using FitnessCenter.Services.ChatBot;

namespace FitnessCenter.Api;

/// <summary>
/// Minimal API endpoints untuk integrasi eksternal (HR, Payroll, CRM)
/// </summary>
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        // ---- Members ----
        api.MapGet("/members", async (AppDbContext db) =>
            await db.Users.Where(u => u.IsActive).Select(u => new
            { u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role, u.LoyaltyPoints, u.RegisteredAt, u.MembershipExpiryDate })
            .ToListAsync()).WithTags("Members");

        api.MapGet("/members/{id}", async (string id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            return user != null ? Results.Ok(new { user.Id, user.FullName, user.Email, user.PhoneNumber, user.LoyaltyPoints, user.MembershipExpiryDate }) : Results.NotFound();
        }).WithTags("Members");

        // ---- Memberships ----
        api.MapGet("/memberships", async (MembershipService svc) => await svc.GetAllPlansAsync()).WithTags("Memberships");
        api.MapGet("/memberships/{id}", async (int id, MembershipService svc) =>
        {
            var p = await svc.GetPlanByIdAsync(id);
            return p != null ? Results.Ok(p) : Results.NotFound();
        }).WithTags("Memberships");

        // ---- Trainers ----
        api.MapGet("/trainers", async (TrainerService svc) => await svc.GetAllAsync()).WithTags("Trainers");
        api.MapGet("/trainers/{id}", async (int id, TrainerService svc) =>
        {
            var t = await svc.GetByIdAsync(id);
            return t != null ? Results.Ok(t) : Results.NotFound();
        }).WithTags("Trainers");

        // ---- Classes ----
        api.MapGet("/classes", async (ClassService svc) => await svc.GetAllAsync()).WithTags("Classes");
        api.MapGet("/classes/{id}", async (int id, ClassService svc) =>
        {
            var c = await svc.GetByIdAsync(id);
            return c != null ? Results.Ok(c) : Results.NotFound();
        }).WithTags("Classes");
        api.MapGet("/classes/schedule", async (ClassService svc) => await svc.GetScheduleAsync()).WithTags("Classes");

        // ---- Attendance ----
        api.MapGet("/attendance/{userId}", async (string userId, AttendanceService svc) => await svc.GetUserAttendanceAsync(userId)).WithTags("Attendance");
        api.MapPost("/attendance/checkin/{userId}", async (string userId, AttendanceService svc) =>
        {
            var att = await svc.CheckInAsync(userId);
            return Results.Created($"/api/v1/attendance/{att.Id}", att);
        }).WithTags("Attendance");

        // ---- Payments ----
        api.MapGet("/payments", async (PaymentService svc) => await svc.GetAllAsync()).WithTags("Payments");
        api.MapGet("/revenue", async (PaymentService svc) =>
        {
            var total = await svc.GetTotalRevenueAsync();
            var monthly = await svc.GetRevenueByMonthAsync();
            return Results.Ok(new { totalRevenue = total, monthly = monthly });
        }).WithTags("Payments");

        // ---- Feedback ----
        api.MapGet("/feedback", async (FeedbackService svc) => await svc.GetAllAsync()).WithTags("Feedback");
        api.MapPost("/feedback", async (Feedback feedback, FeedbackService svc) =>
        {
            var f = await svc.CreateAsync(feedback);
            return Results.Created($"/api/v1/feedback/{f.Id}", f);
        }).WithTags("Feedback");

        // ---- Events ----
        api.MapGet("/events", async (EventService svc) => await svc.GetPublishedAsync()).WithTags("Events");
        api.MapGet("/events/{id}", async (int id, EventService svc) =>
        {
            var e = await svc.GetByIdAsync(id);
            return e != null ? Results.Ok(e) : Results.NotFound();
        }).WithTags("Events");

        // ---- Forum ----
        api.MapGet("/forum/posts", async (ForumService svc) => await svc.GetAllPostsAsync()).WithTags("Forum");
        api.MapGet("/forum/posts/{id}", async (int id, ForumService svc) =>
        {
            var p = await svc.GetPostByIdAsync(id);
            return p != null ? Results.Ok(p) : Results.NotFound();
        }).WithTags("Forum");

        // ---- Gamification ----
        api.MapGet("/leaderboard", async (GamificationService svc) => await svc.GetLeaderboardAsync(20)).WithTags("Gamification");
        api.MapGet("/achievements/{userId}", async (string userId, GamificationService svc) => await svc.GetUserAchievementsAsync(userId)).WithTags("Gamification");

        // ---- Notifications ----
        api.MapGet("/notifications/{userId}", async (string userId, NotificationService svc) => await svc.GetUserNotificationsAsync(userId)).WithTags("Notifications");

        // ---- Chat Bot ----
        api.MapGet("/chat/info", (ChatBotService svc) =>
        {
            var info = svc.GetChatBotInfo();
            return Results.Ok(new
            {
                info.Name,
                info.Provider,
                info.Model,
                info.Temperature,
                info.MaxTokens,
                info.FunctionsEnabled
            });
        }).WithTags("Chat");

        api.MapGet("/chat/sessions/{userId}", async (string? userId, ChatBotService svc) => await svc.GetSessionsAsync(userId)).WithTags("Chat");
        api.MapPost("/chat/send", async (ChatSendRequest req, ChatBotService svc) =>
        {
            var resp = await svc.SendMessageAsync(req.SessionId, req.Message, req.ImageUrl, req.DocumentUrl);
            return Results.Ok(new
            {
                resp.Id, resp.SessionId, resp.Role, resp.Content, resp.ModelUsed, resp.CreatedAt
            });
        }).WithTags("Chat");

        // ---- Export ----
        api.MapGet("/export/members/csv", async (AppDbContext db, ExportService export) =>
        {
            var data = await db.Users.Select(u => new { u.FullName, u.Email, u.PhoneNumber, u.RegisteredAt }).ToListAsync();
            return Results.File(export.ExportToCsv(data), "text/csv", "members.csv");
        }).WithTags("Export");
        api.MapGet("/export/members/excel", async (AppDbContext db, ExportService export) =>
        {
            var data = await db.Users.Select(u => new { u.FullName, u.Email, u.PhoneNumber, u.RegisteredAt }).ToListAsync();
            return Results.File(export.ExportToExcel(data), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "members.xlsx");
        }).WithTags("Export");

        // ---- Storage ----
        api.MapGet("/storage/info", (StorageService storage) =>
        {
            var info = storage.GetStorageInfo();
            return Results.Ok(new
            {
                provider = info.ProviderName,
                isCloudStorage = info.IsCloudStorage,
                isFileSystem = info.IsFileSystem
            });
        }).WithTags("Storage");

        api.MapPost("/storage/upload", async (HttpRequest request, StorageService storage) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data" });

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null)
                return Results.BadRequest(new { error = "No file provided. Use form field 'file'." });

            var folder = form["folder"].FirstOrDefault() ?? "general";

            using var stream = file.OpenReadStream();
            var url = await storage.UploadAsync(stream, file.FileName, folder, file.ContentType);

            return Results.Ok(new
            {
                url, fileName = file.FileName, size = file.Length,
                contentType = file.ContentType, provider = storage.ActiveProvider
            });
        }).WithTags("Storage").DisableAntiforgery();

        api.MapDelete("/storage/delete", async (string fileUrl, StorageService storage) =>
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return Results.BadRequest(new { error = "fileUrl is required" });

            var deleted = await storage.DeleteAsync(fileUrl);
            return deleted
                ? Results.Ok(new { deleted = true, fileUrl })
                : Results.NotFound(new { deleted = false, fileUrl, message = "File not found or could not be deleted" });
        }).WithTags("Storage");

        // ---- System Health ----
        api.MapGet("/health", (StorageService storage, AppDbContext db, ChatBotService chatBot) =>
        {
            var dbConnected = false;
            try { dbConnected = db.Database.CanConnect(); } catch { }

            return Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                database = dbConnected ? "connected" : "disconnected",
                storage = storage.ActiveProvider,
                chatbot = chatBot.GetChatBotInfo().Provider,
                version = "2.0.0"
            });
        }).WithTags("System");
    }
}

/// <summary>Request model untuk chat API</summary>
public class ChatSendRequest
{
    public int SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? DocumentUrl { get; set; }
}
