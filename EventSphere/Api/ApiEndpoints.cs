using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Api;

/// <summary>
/// REST API Minimal API endpoints untuk semua entity.
/// Akses dengan header X-Api-Key, dokumentasi Swagger di /swagger.
/// </summary>
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1")
            .WithTags("EventSphere API v1");

        // ═══════════════════════════════════════════
        // 🔑 SUMMARY / ROOT
        // ═══════════════════════════════════════════

        api.MapGet("/", () => Results.Ok(new
        {
            name = "EventSphere API",
            version = "v1",
            authentication = "X-Api-Key header required",
            swagger = "/swagger",
            endpoints = new[]
            {
                "GET  /api/v1/events",
                "GET  /api/v1/events/{id}",
                "POST /api/v1/events",
                "PUT  /api/v1/events/{id}",
                "DELETE /api/v1/events/{id}",
                "GET  /api/v1/vendors",
                "GET  /api/v1/vendors/{id}",
                "POST /api/v1/vendors",
                "GET  /api/v1/guests/{eventId}",
                "GET  /api/v1/budget/{eventId}",
                "GET  /api/v1/tasks",
                "GET  /api/v1/tasks/{eventId}",
                "GET  /api/v1/documents/{eventId}",
                "GET  /api/v1/gallery/{eventId}",
                "GET  /api/v1/forum",
                "GET  /api/v1/forum/{id}",
                "GET  /api/v1/users",
                "GET  /api/v1/users/{id}",
                "GET  /api/v1/dashboard/stats",
                "GET  /api/v1/notifications/{userId}",
                "GET  /api/v1/seating/{eventId}",
                "GET  /api/v1/chat/{sessionId}",
                "GET  /api/v1/feedback/{eventId}",
                "GET  /api/v1/contracts/{eventId}",
                "GET  /api/v1/invoices/{contractId}"
            }
        }))
        .ExcludeFromDescription();

        // ═══════════════════════════════════════════
        // 📅 EVENTS
        // ═══════════════════════════════════════════

        var eventsGroup = api.MapGroup("/events").WithTags("Events");

        eventsGroup.MapGet("/", async (AppDbContext db, string? status, int page = 1, int size = 20) =>
        {
            var query = db.Events.Include(e => e.CreatedBy).AsQueryable();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<EventStatus>(status, true, out var s))
                query = query.Where(e => e.Status == s);
            var total = await query.CountAsync();
            var data = await query.OrderByDescending(e => e.EventDate).Skip((page - 1) * size).Take(size).ToListAsync();
            return Results.Ok(new { total, page, size, data });
        })
        .WithName("GetEvents")
        .WithDescription("Daftar event dengan pagination dan filter status.");

        eventsGroup.MapGet("/{id:guid}", async (AppDbContext db, Guid id) =>
        {
            var evt = await db.Events.Include(e => e.CreatedBy).Include(e => e.Organizer).Include(e => e.Client)
                .Include(e => e.Attendees).ThenInclude(a => a.User)
                .Include(e => e.VendorContracts).ThenInclude(vc => vc.Vendor)
                .Include(e => e.BudgetItems).Include(e => e.TaskItems)
                .Include(e => e.TableArrangements).FirstOrDefaultAsync(e => e.Id == id);
            return evt is not null ? Results.Ok(evt) : Results.NotFound(new { error = "Event not found" });
        })
        .WithName("GetEventById");

        eventsGroup.MapPost("/", async (AppDbContext db, Event evt) =>
        {
            evt.Id = Guid.NewGuid(); evt.CreatedAt = DateTime.UtcNow; evt.UpdatedAt = DateTime.UtcNow;
            db.Events.Add(evt); await db.SaveChangesAsync();
            return Results.Created($"/api/v1/events/{evt.Id}", evt);
        })
        .WithName("CreateEvent");

        eventsGroup.MapPut("/{id:guid}", async (AppDbContext db, Guid id, Event updated) =>
        {
            var evt = await db.Events.FindAsync(id);
            if (evt is null) return Results.NotFound();
            evt.Name = updated.Name; evt.Description = updated.Description;
            evt.EventDate = updated.EventDate; evt.EndDate = updated.EndDate;
            evt.Location = updated.Location; evt.Theme = updated.Theme;
            evt.Status = updated.Status; evt.BudgetTotal = updated.BudgetTotal;
            evt.ExpectedGuests = updated.ExpectedGuests; evt.EventType = updated.EventType;
            evt.PrimaryColor = updated.PrimaryColor; evt.SecondaryColor = updated.SecondaryColor;
            evt.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(evt);
        })
        .WithName("UpdateEvent");

        eventsGroup.MapDelete("/{id:guid}", async (AppDbContext db, Guid id) =>
        {
            var evt = await db.Events.FindAsync(id);
            if (evt is null) return Results.NotFound();
            db.Events.Remove(evt); await db.SaveChangesAsync();
            return Results.Ok(new { deleted = true });
        })
        .WithName("DeleteEvent");

        // ═══════════════════════════════════════════
        // 🏢 VENDORS
        // ═══════════════════════════════════════════

        var vendorsGroup = api.MapGroup("/vendors").WithTags("Vendors");

        vendorsGroup.MapGet("/", async (AppDbContext db, string? category, string? search, int page = 1, int size = 20) =>
        {
            var query = db.Vendors.AsQueryable();
            if (!string.IsNullOrEmpty(category))
                query = query.Where(v => v.Category!.ToLower() == category.ToLower());
            if (!string.IsNullOrEmpty(search))
                query = query.Where(v => v.Name.ToLower().Contains(search.ToLower()) || v.Category!.ToLower().Contains(search.ToLower()));
            var total = await query.CountAsync();
            var data = await query.OrderByDescending(v => v.Rating).Skip((page - 1) * size).Take(size).ToListAsync();
            return Results.Ok(new { total, page, size, data });
        })
        .WithName("GetVendors");

        vendorsGroup.MapGet("/{id:guid}", async (AppDbContext db, Guid id) =>
        {
            var v = await db.Vendors.Include(x => x.Contracts).Include(x => x.Reviews).Include(x => x.Portfolios).FirstOrDefaultAsync(x => x.Id == id);
            return v is not null ? Results.Ok(v) : Results.NotFound();
        })
        .WithName("GetVendorById");

        vendorsGroup.MapPost("/", async (AppDbContext db, Vendor v) => { v.Id = Guid.NewGuid(); db.Vendors.Add(v); await db.SaveChangesAsync(); return Results.Created($"/api/v1/vendors/{v.Id}", v); }).WithName("CreateVendor");
        vendorsGroup.MapPut("/{id:guid}", async (AppDbContext db, Guid id, Vendor v) => { var e = await db.Vendors.FindAsync(id); if (e is null) return Results.NotFound(); db.Entry(e).CurrentValues.SetValues(v); await db.SaveChangesAsync(); return Results.Ok(e); }).WithName("UpdateVendor");
        vendorsGroup.MapDelete("/{id:guid}", async (AppDbContext db, Guid id) => { var v = await db.Vendors.FindAsync(id); if (v is null) return Results.NotFound(); db.Vendors.Remove(v); await db.SaveChangesAsync(); return Results.Ok(new { deleted = true }); }).WithName("DeleteVendor");

        // ═══════════════════════════════════════════
        // 👥 GUESTS
        // ═══════════════════════════════════════════

        api.MapGet("/guests/{eventId:guid}", async (AppDbContext db, Guid eventId) =>
        {
            var guests = await db.EventAttendees.Where(a => a.EventId == eventId).Include(a => a.User).Include(a => a.Table).ToListAsync();
            return Results.Ok(new { eventId, total = guests.Count, data = guests });
        })
        .WithTags("Guests").WithName("GetGuestsByEvent");

        // ═══════════════════════════════════════════
        // 💰 BUDGET
        // ═══════════════════════════════════════════

        api.MapGet("/budget/{eventId:guid}", async (AppDbContext db, Guid eventId) =>
        {
            var items = await db.BudgetItems.Where(b => b.EventId == eventId).OrderBy(b => b.SortOrder).ToListAsync();
            return Results.Ok(new { eventId, totalEstimated = items.Sum(i => i.EstimatedCost), totalActual = items.Sum(i => i.ActualCost), remaining = items.Sum(i => i.EstimatedCost) - items.Sum(i => i.ActualCost), data = items });
        })
        .WithTags("Budget").WithName("GetBudgetByEvent");

        // ═══════════════════════════════════════════
        // ✅ TASKS
        // ═══════════════════════════════════════════

        var tasksGroup = api.MapGroup("/tasks").WithTags("Tasks");

        tasksGroup.MapGet("/{eventId:guid}", async (AppDbContext db, Guid eventId) =>
        {
            var tasks = await db.TaskItems.Where(t => t.EventId == eventId).Include(t => t.AssignedTo).OrderBy(t => t.SortOrder).ToListAsync();
            return Results.Ok(new { eventId, total = tasks.Count, done = tasks.Count(t => t.Status == TaskItemStatus.Done), data = tasks });
        })
        .WithName("GetTasksByEvent");

        tasksGroup.MapGet("/", async (AppDbContext db, string? userId, string? status, int page = 1, int size = 50) =>
        {
            var query = db.TaskItems.Include(t => t.Event).Include(t => t.AssignedTo).AsQueryable();
            if (!string.IsNullOrEmpty(userId)) query = query.Where(t => t.AssignedToId == userId);
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<TaskItemStatus>(status, true, out var s)) query = query.Where(t => t.Status == s);
            var total = await query.CountAsync();
            var data = await query.OrderBy(t => t.DueDate).Skip((page - 1) * size).Take(size).ToListAsync();
            return Results.Ok(new { total, page, size, data });
        })
        .WithName("GetAllTasks");

        // ═══════════════════════════════════════════
        // 📁 DOCUMENTS
        // ═══════════════════════════════════════════

        api.MapGet("/documents/{eventId:guid}", async (AppDbContext db, Guid eventId) =>
        {
            var docs = await db.Documents.Where(d => d.EventId == eventId).Include(d => d.UploadedBy).OrderByDescending(d => d.CreatedAt).ToListAsync();
            return Results.Ok(new { eventId, total = docs.Count, data = docs });
        })
        .WithTags("Documents").WithName("GetDocumentsByEvent");

        // ═══════════════════════════════════════════
        // 🖼️ GALLERY
        // ═══════════════════════════════════════════

        api.MapGet("/gallery/{eventId:guid}", async (AppDbContext db, Guid eventId, string? category) =>
        {
            var query = db.MediaItems.Where(m => m.EventId == eventId);
            if (!string.IsNullOrEmpty(category)) query = query.Where(m => m.Category == category);
            var data = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
            return Results.Ok(new { eventId, total = data.Count, data });
        })
        .WithTags("Gallery").WithName("GetGalleryByEvent");

        // ═══════════════════════════════════════════
        // 🪑 SEATING
        // ═══════════════════════════════════════════

        api.MapGet("/seating/{eventId:guid}", async (AppDbContext db, Guid eventId) =>
        {
            var tables = await db.TableArrangements.Where(t => t.EventId == eventId).OrderBy(t => t.SortOrder).ToListAsync();
            var attendees = await db.EventAttendees.Where(a => a.EventId == eventId && a.TableId != null).Include(a => a.User).ToListAsync();
            var result = tables.Select(t => new { t.Id, t.TableName, t.Shape, t.Capacity, t.FilledSeats, t.PositionX, t.PositionY, t.Color, guests = attendees.Where(a => a.TableId == t.Id).Select(a => new { a.Id, name = a.User?.FullName, a.Role, a.RsvpStatus, a.SeatNumber }) });
            return Results.Ok(new { eventId, data = result });
        })
        .WithTags("Seating").WithName("GetSeatingByEvent");

        // ═══════════════════════════════════════════
        // 💬 CHAT
        // ═══════════════════════════════════════════

        api.MapGet("/chat/{sessionId:guid}", async (AppDbContext db, Guid sessionId) =>
        {
            var messages = await db.ChatMessages.Where(m => m.ChatSessionId == sessionId).Include(m => m.Sender).OrderBy(m => m.SentAt).ToListAsync();
            return Results.Ok(new { sessionId, total = messages.Count, data = messages });
        })
        .WithTags("Chat").WithName("GetChatMessages");

        // ═══════════════════════════════════════════
        // 💡 FORUM
        // ═══════════════════════════════════════════

        var forumGroup = api.MapGroup("/forum").WithTags("Forum");

        forumGroup.MapGet("/", async (AppDbContext db, string? category, int page = 1, int size = 20) =>
        {
            var query = db.ForumPosts.Include(p => p.Author).AsQueryable();
            if (!string.IsNullOrEmpty(category)) query = query.Where(p => p.Category == category);
            var total = await query.CountAsync();
            var data = await query.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync();
            return Results.Ok(new { total, page, size, data });
        })
        .WithName("GetForumPosts");

        forumGroup.MapGet("/{id:guid}", async (AppDbContext db, Guid id) =>
        {
            var post = await db.ForumPosts.Include(p => p.Author).Include(p => p.Comments).ThenInclude(c => c.Author).FirstOrDefaultAsync(p => p.Id == id);
            return post is not null ? Results.Ok(post) : Results.NotFound();
        })
        .WithName("GetForumPostById");

        // ═══════════════════════════════════════════
        // 👤 USERS
        // ═══════════════════════════════════════════

        var usersGroup = api.MapGroup("/users").WithTags("Users");

        usersGroup.MapGet("/", async (AppDbContext db, string? role, string? search, bool? active, int page = 1, int size = 20) =>
        {
            var query = db.Users.AsQueryable();
            if (active.HasValue) query = query.Where(u => u.IsActive == active.Value);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => (u.FullName!.ToLower().Contains(search.ToLower())) || (u.Email!.ToLower().Contains(search.ToLower())));
            if (!string.IsNullOrEmpty(role))
            {
                var roleEntity = await db.Roles.FirstOrDefaultAsync(r => r.Name == role);
                if (roleEntity != null)
                {
                    var usersInRole = await db.UserRoles.Where(ur => ur.RoleId == roleEntity.Id).Select(ur => ur.UserId).ToListAsync();
                    query = query.Where(u => usersInRole.Contains(u.Id));
                }
            }
            var total = await query.CountAsync();
            var data = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * size).Take(size).Select(u => new { u.Id, u.FullName, u.Email, u.Company, u.IsActive, u.CreatedAt }).ToListAsync();
            return Results.Ok(new { total, page, size, data });
        })
        .WithName("GetUsers");

        usersGroup.MapGet("/{id}", async (AppDbContext db, string id) =>
        {
            var user = await db.Users.Select(u => new { u.Id, u.FullName, u.Email, u.Company, u.Bio, u.IsActive, u.CreatedAt, u.AvatarUrl }).FirstOrDefaultAsync(u => u.Id == id);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        })
        .WithName("GetUserById");

        // ═══════════════════════════════════════════
        // 📊 DASHBOARD
        // ═══════════════════════════════════════════

        api.MapGet("/dashboard/stats", async (AppDbContext db) =>
        {
            var now = DateTime.UtcNow;
            return Results.Ok(new
            {
                totalEvents = await db.Events.CountAsync(),
                upcomingEvents = await db.Events.CountAsync(e => e.EventDate >= now && e.Status != EventStatus.Cancelled),
                activeEvents = await db.Events.CountAsync(e => e.Status == EventStatus.Confirmed || e.Status == EventStatus.InProgress),
                totalVendors = await db.Vendors.CountAsync(),
                totalUsers = await db.Users.CountAsync(),
                totalRevenue = await db.VendorContracts.SumAsync(vc => vc.Amount),
                avgGuestSatisfaction = await db.Feedbacks.AnyAsync() ? await db.Feedbacks.AverageAsync(f => (double)f.Rating) : 0,
                tasksDone = await db.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Done),
                tasksPending = await db.TaskItems.CountAsync(t => t.Status != TaskItemStatus.Done)
            });
        })
        .WithTags("Dashboard").WithName("GetDashboardStats");

        api.MapGet("/dashboard/events-by-type", async (AppDbContext db) =>
        {
            var breakdown = await db.Events.GroupBy(e => e.EventType ?? "Other").Select(g => new { type = g.Key, count = g.Count() }).ToListAsync();
            return Results.Ok(breakdown);
        })
        .WithTags("Dashboard").WithName("GetEventsByType");

        // ═══════════════════════════════════════════
        // 🔔 NOTIFICATIONS
        // ═══════════════════════════════════════════

        api.MapGet("/notifications/{userId}", async (AppDbContext db, string userId, bool? unreadOnly, int limit = 50) =>
        {
            var query = db.Notifications.Where(n => n.UserId == userId);
            if (unreadOnly == true) query = query.Where(n => !n.IsRead);
            var data = await query.OrderByDescending(n => n.CreatedAt).Take(limit).ToListAsync();
            return Results.Ok(new { userId, total = data.Count, unreadCount = data.Count(n => !n.IsRead), data });
        })
        .WithTags("Notifications").WithName("GetNotifications");

        // ═══════════════════════════════════════════
        // ⭐ FEEDBACK
        // ═══════════════════════════════════════════

        api.MapGet("/feedback/{eventId:guid}", async (AppDbContext db, Guid eventId) =>
        {
            var fb = await db.Feedbacks.Where(f => f.EventId == eventId).Include(f => f.User).OrderByDescending(f => f.CreatedAt).ToListAsync();
            return Results.Ok(new { eventId, total = fb.Count, avgRating = fb.Any() ? fb.Average(f => (double)f.Rating) : 0, data = fb });
        })
        .WithTags("Feedback").WithName("GetFeedbackByEvent");

        // ═══════════════════════════════════════════
        // 📊 VENDOR CONTRACTS & INVOICES
        // ═══════════════════════════════════════════

        api.MapGet("/contracts/{eventId:guid}", async (AppDbContext db, Guid eventId) =>
        {
            var contracts = await db.VendorContracts.Where(vc => vc.EventId == eventId).Include(vc => vc.Vendor).Include(vc => vc.Invoices).ToListAsync();
            return Results.Ok(new { eventId, total = contracts.Count, totalAmount = contracts.Sum(c => c.Amount), totalPaid = contracts.Sum(c => c.PaidAmount), data = contracts });
        })
        .WithTags("Contracts").WithName("GetContractsByEvent");

        api.MapGet("/invoices/{contractId:guid}", async (AppDbContext db, Guid contractId) =>
        {
            var invoices = await db.Invoices.Where(i => i.ContractId == contractId).Include(i => i.PaidBy).OrderByDescending(i => i.CreatedAt).ToListAsync();
            return Results.Ok(new { contractId, total = invoices.Count, totalAmount = invoices.Sum(i => i.TotalAmount), data = invoices });
        })
        .WithTags("Contracts").WithName("GetInvoicesByContract");
    }
}
