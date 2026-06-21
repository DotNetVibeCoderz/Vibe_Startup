using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalBoil.Data;
using RentalBoil.Models;
using RentalBoil.Services;

namespace RentalBoil.Api;

/// <summary>
/// REST API Endpoints - Mengekspos semua akses data via Minimal API.
/// Semua endpoint di-prefix dengan /api dan memerlukan X-Api-Key header.
/// </summary>
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api")
            .WithOpenApi()
            .WithTags("RentalBoil API");

        // ═══════════════════════════════════════════
        // 🚗 VEHICLES
        // ═══════════════════════════════════════════
        var vehicles = api.MapGroup("/vehicles").WithTags("Vehicles");

        // GET /api/vehicles - List with search & filters
        vehicles.MapGet("/", async (
            [FromServices] VehicleService svc,
            [FromQuery] string? search,
            [FromQuery] VehicleType? type,
            [FromQuery] string? brand,
            [FromQuery] int? minCapacity,
            [FromQuery] TransmissionType? transmission,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] string? sortBy,
            [FromQuery] bool sortDesc = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            var (items, total) = await svc.GetVehiclesAsync(search, type, brand, minCapacity, null,
                transmission, null, minPrice, maxPrice, sortBy: sortBy, sortDesc: sortDesc, page: page, pageSize: pageSize);
            return Results.Ok(new { data = items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
        })
        .WithName("GetVehicles")
        .WithDescription("Get list of vehicles with search, filter, sort, and paging");

        // GET /api/vehicles/{id}
        vehicles.MapGet("/{id:int}", async ([FromServices] VehicleService svc, int id) =>
        {
            var v = await svc.GetVehicleByIdAsync(id);
            return v is null ? Results.NotFound(new { error = "Vehicle not found" }) : Results.Ok(v);
        }).WithName("GetVehicleById");

        // POST /api/vehicles
        vehicles.MapPost("/", async ([FromServices] VehicleService svc, Vehicle vehicle) =>
        {
            vehicle.CreatedAt = DateTime.UtcNow;
            var created = await svc.CreateVehicleAsync(vehicle);
            return Results.Created($"/api/vehicles/{created.Id}", created);
        }).WithName("CreateVehicle");

        // PUT /api/vehicles/{id} - full update
        vehicles.MapPut("/{id:int}", async ([FromServices] VehicleService svc, int id, Vehicle vehicle) =>
        {
            vehicle.Id = id;
            var updated = await svc.UpdateVehicleAsync(vehicle);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("UpdateVehicle");

        // DELETE /api/vehicles/{id}
        vehicles.MapDelete("/{id:int}", async ([FromServices] VehicleService svc, int id) =>
        {
            var ok = await svc.DeleteVehicleAsync(id);
            return ok ? Results.Ok(new { deleted = true }) : Results.NotFound();
        }).WithName("DeleteVehicle");

        // ── VEHICLE UPDATE ENDPOINTS (untuk Simulator & IoT) ──

        // PATCH /api/vehicles/{id}/location - Update GPS location only
        vehicles.MapPatch("/{id:int}/location", async (AppDbContext db, int id, [FromBody] VehicleLocationRequest req) =>
        {
            var v = await db.Vehicles.FindAsync(id);
            if (v is null) return Results.NotFound(new { error = "Vehicle not found" });

            v.Latitude = req.Latitude;
            v.Longitude = req.Longitude;
            v.CurrentSpeed = req.Speed;
            v.CurrentHeading = req.Heading;
            v.Location = req.Address ?? v.Location;
            v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                v.Id, v.Name, v.PlateNumber,
                v.Latitude, v.Longitude, v.CurrentSpeed, v.CurrentHeading,
                v.MotionStatus, v.EngineStatus, v.LockStatus,
                v.UpdatedAt
            });
        })
        .WithName("UpdateVehicleLocation")
        .WithDescription("Update GPS location, speed, heading, and address. Used by simulator & tracking devices.");

        // PATCH /api/vehicles/{id}/iot - Update IoT status (lock, engine, motion)
        vehicles.MapPatch("/{id:int}/iot", async (AppDbContext db, int id, [FromBody] VehicleIoTRequest req) =>
        {
            var v = await db.Vehicles.FindAsync(id);
            if (v is null) return Results.NotFound(new { error = "Vehicle not found" });

            if (req.LockStatus.HasValue) v.LockStatus = req.LockStatus.Value;
            if (req.EngineStatus.HasValue) v.EngineStatus = req.EngineStatus.Value;
            if (req.MotionStatus.HasValue) v.MotionStatus = req.MotionStatus.Value;
            v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                v.Id, v.Name, v.PlateNumber,
                v.LockStatus, v.EngineStatus, v.MotionStatus,
                v.UpdatedAt
            });
        })
        .WithName("UpdateVehicleIoT")
        .WithDescription("Update vehicle IoT status: lock, engine, motion. Used by IoT devices & simulator.");

        // PATCH /api/vehicles/{id}/condition - Update kondisi kendaraan
        vehicles.MapPatch("/{id:int}/condition", async (AppDbContext db, int id, [FromBody] VehicleConditionRequest req) =>
        {
            var v = await db.Vehicles.FindAsync(id);
            if (v is null) return Results.NotFound(new { error = "Vehicle not found" });

            if (req.Color != null) v.Color = req.Color;
            if (req.IsAvailable.HasValue) v.IsAvailable = req.IsAvailable.Value;
            if (req.Description != null) v.Description = req.Description;
            if (req.Specifications != null) v.Specifications = req.Specifications;
            v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                v.Id, v.Name, v.PlateNumber,
                v.Color, v.IsAvailable, v.Description,
                v.UpdatedAt
            });
        })
        .WithName("UpdateVehicleCondition")
        .WithDescription("Update vehicle physical condition: color, availability, description, specs.");

        // PATCH /api/vehicles/{id}/pricing - Update harga
        vehicles.MapPatch("/{id:int}/pricing", async (AppDbContext db, int id, [FromBody] VehiclePricingRequest req) =>
        {
            var v = await db.Vehicles.FindAsync(id);
            if (v is null) return Results.NotFound(new { error = "Vehicle not found" });

            if (req.PricePerHour.HasValue) v.PricePerHour = req.PricePerHour.Value;
            if (req.PricePerDay.HasValue) v.PricePerDay = req.PricePerDay.Value;
            if (req.DynamicPriceMultiplier.HasValue) v.DynamicPriceMultiplier = req.DynamicPriceMultiplier.Value;
            if (req.InsuranceAvailable.HasValue) v.InsuranceAvailable = req.InsuranceAvailable.Value;
            if (req.InsuranceCostPerDay.HasValue) v.InsuranceCostPerDay = req.InsuranceCostPerDay.Value;
            v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                v.Id, v.Name, v.PricePerHour, v.PricePerDay,
                v.DynamicPriceMultiplier, v.InsuranceAvailable, v.InsuranceCostPerDay,
                v.UpdatedAt
            });
        })
        .WithName("UpdateVehiclePricing")
        .WithDescription("Update vehicle pricing: per hour, per day, dynamic multiplier, insurance.");

        // POST /api/vehicles/{id}/simulator-update - Full simulator update (GPS + IoT + Speed)
        vehicles.MapPost("/{id:int}/simulator-update", async (AppDbContext db, int id, [FromBody] SimulatorUpdateRequest req) =>
        {
            var v = await db.Vehicles.FindAsync(id);
            if (v is null) return Results.NotFound(new { error = "Vehicle not found" });

            // Perbarui lokasi GPS jika dikirim
            if (req.Latitude.HasValue) v.Latitude = req.Latitude.Value;
            if (req.Longitude.HasValue) v.Longitude = req.Longitude.Value;
            if (req.Speed.HasValue) v.CurrentSpeed = req.Speed.Value;
            if (req.Heading.HasValue) v.CurrentHeading = req.Heading.Value;

            // Perbarui status IoT jika dikirim
            if (!string.IsNullOrWhiteSpace(req.LockStatus))
                v.LockStatus = Enum.Parse<LockStatus>(req.LockStatus);
            if (!string.IsNullOrWhiteSpace(req.EngineStatus))
                v.EngineStatus = Enum.Parse<EngineStatus>(req.EngineStatus);
            if (!string.IsNullOrWhiteSpace(req.MotionStatus))
                v.MotionStatus = Enum.Parse<VehicleMotionStatus>(req.MotionStatus);

            // Perbarui alamat jika dikirim
            if (!string.IsNullOrWhiteSpace(req.Address))
                v.Location = req.Address;

            v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                vehicle = new
                {
                    v.Id, v.Name, v.PlateNumber,
                    v.Latitude, v.Longitude,
                    v.CurrentSpeed, v.CurrentHeading,
                    v.LockStatus, v.EngineStatus, v.MotionStatus,
                    v.Location, v.UpdatedAt
                },
                timestamp = DateTime.UtcNow,
                message = "Simulator update berhasil"
            });
        })
        .WithName("SimulatorUpdateVehicle")
        .WithDescription("Full vehicle update from GPS simulator: location, speed, heading, IoT status, address. Single endpoint for simulators.");

        // POST /api/vehicles/batch/simulator-update - Batch update multiple vehicles
        vehicles.MapPost("/batch/simulator-update", async (AppDbContext db, [FromBody] List<SimulatorUpdateItem> items) =>
        {
            var results = new List<object>();
            foreach (var req in items)
            {
                var v = await db.Vehicles.FindAsync(req.VehicleId);
                if (v is null) { results.Add(new { req.VehicleId, error = "Not found" }); continue; }

                if (req.Latitude.HasValue) v.Latitude = req.Latitude.Value;
                if (req.Longitude.HasValue) v.Longitude = req.Longitude.Value;
                if (req.Speed.HasValue) v.CurrentSpeed = req.Speed.Value;
                if (req.Heading.HasValue) v.CurrentHeading = req.Heading.Value;
                if (!string.IsNullOrWhiteSpace(req.LockStatus))
                    v.LockStatus = Enum.Parse<LockStatus>(req.LockStatus);
                if (!string.IsNullOrWhiteSpace(req.EngineStatus))
                    v.EngineStatus = Enum.Parse<EngineStatus>(req.EngineStatus);
                if (!string.IsNullOrWhiteSpace(req.MotionStatus))
                    v.MotionStatus = Enum.Parse<VehicleMotionStatus>(req.MotionStatus);
                v.UpdatedAt = DateTime.UtcNow;

                results.Add(new { req.VehicleId, v.Name, v.Latitude, v.Longitude, v.CurrentSpeed, v.LockStatus, v.EngineStatus, v.MotionStatus });
            }
            await db.SaveChangesAsync();

            return Results.Ok(new { updated = results.Count, results, timestamp = DateTime.UtcNow });
        })
        .WithName("BatchSimulatorUpdate")
        .WithDescription("Batch update multiple vehicles at once. Used by simulator for efficient bulk updates.");

        // GET /api/vehicles/active-for-simulator - List kendaraan yang perlu disimulasi
        vehicles.MapGet("/active-for-simulator", async (AppDbContext db) =>
        {
            var activeBookingIds = await db.Bookings
                .Where(b => b.Status == BookingStatus.Active)
                .Select(b => b.VehicleId)
                .ToListAsync();

            var vehicles = await db.Vehicles
                .Where(v => activeBookingIds.Contains(v.Id))
                .Select(v => new
                {
                    v.Id, v.Name, v.PlateNumber, v.Type,
                    v.Latitude, v.Longitude,
                    v.CurrentSpeed, v.CurrentHeading,
                    v.LockStatus, v.EngineStatus, v.MotionStatus,
                    v.Location, v.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(new
            {
                count = vehicles.Count,
                vehicles,
                timestamp = DateTime.UtcNow,
                message = "Kendaraan yang sedang aktif dalam booking (perlu simulasi GPS)"
            });
        })
        .WithName("GetActiveVehiclesForSimulator")
        .WithDescription("Get list of vehicles currently in active bookings - for GPS simulator to know which vehicles to track.");

        // GET /api/vehicles/{id}/reviews
        vehicles.MapGet("/{id:int}/reviews", async ([FromServices] ReviewService svc, int id) =>
            Results.Ok(await svc.GetVehicleReviewsAsync(id))).WithName("GetVehicleReviews");

        // GET /api/vehicles/brands
        vehicles.MapGet("/brands", async ([FromServices] VehicleService svc) =>
            Results.Ok(await svc.GetBrandsAsync())).WithName("GetBrands");

        // GET /api/vehicles/stats
        vehicles.MapGet("/stats", async ([FromServices] VehicleService svc) =>
            Results.Ok(await svc.GetVehicleStatsAsync())).WithName("GetVehicleStats");

        // ═══════════════════════════════════════════
        // 📋 BOOKINGS
        // ═══════════════════════════════════════════
        var bookings = api.MapGroup("/bookings").WithTags("Bookings");

        bookings.MapGet("/", async ([FromServices] BookingService svc, [FromQuery] BookingStatus? status, [FromQuery] int page = 1) =>
        {
            var items = await svc.GetAllBookingsAsync(status, page);
            return Results.Ok(new { data = items, page });
        }).WithName("GetBookings");

        bookings.MapGet("/{id:int}", async ([FromServices] BookingService svc, int id) =>
        {
            var b = await svc.GetBookingByIdAsync(id);
            return b is null ? Results.NotFound() : Results.Ok(b);
        }).WithName("GetBookingById");

        bookings.MapPost("/", async ([FromServices] BookingService svc, Booking booking) =>
        {
            var created = await svc.CreateBookingAsync(booking);
            return created is null ? Results.BadRequest(new { error = "Vehicle not available" })
                : Results.Created($"/api/bookings/{created.Id}", created);
        }).WithName("CreateBooking");

        bookings.MapPut("/{id:int}/accept", async ([FromServices] BookingService svc, int id) =>
            Results.Ok(new { accepted = await svc.AcceptBookingAsync(id) })).WithName("AcceptBooking");

        bookings.MapPut("/{id:int}/reject", async ([FromServices] BookingService svc, int id, [FromBody] RejectRequest req) =>
            Results.Ok(new { rejected = await svc.RejectBookingAsync(id, req.Reason) })).WithName("RejectBooking");

        bookings.MapPut("/{id:int}/activate", async ([FromServices] BookingService svc, int id) =>
            Results.Ok(new { activated = await svc.ActivateBookingAsync(id) })).WithName("ActivateBooking");

        bookings.MapPut("/{id:int}/complete", async ([FromServices] BookingService svc, int id) =>
            Results.Ok(new { completed = await svc.CompleteBookingAsync(id) })).WithName("CompleteBooking");

        bookings.MapPut("/{id:int}/cancel", async ([FromServices] BookingService svc, int id) =>
            Results.Ok(new { cancelled = await svc.CancelBookingAsync(id) })).WithName("CancelBooking");

        bookings.MapGet("/customer/{customerId}", async ([FromServices] BookingService svc, string customerId) =>
            Results.Ok(await svc.GetCustomerBookingsAsync(customerId))).WithName("GetCustomerBookings");

        bookings.MapGet("/partner/{partnerId}", async ([FromServices] BookingService svc, string partnerId) =>
            Results.Ok(await svc.GetPartnerBookingsAsync(partnerId))).WithName("GetPartnerBookings");

        bookings.MapGet("/stats/admin", async ([FromServices] BookingService svc) =>
            Results.Ok(await svc.GetAdminStatsAsync())).WithName("GetAdminStats");

        bookings.MapGet("/stats/partner/{partnerId}", async ([FromServices] BookingService svc, string partnerId) =>
            Results.Ok(await svc.GetPartnerStatsAsync(partnerId))).WithName("GetPartnerStats");

        // ═══════════════════════════════════════════
        // 👤 USERS
        // ═══════════════════════════════════════════
        var users = api.MapGroup("/users").WithTags("Users");

        users.MapGet("/", async (AppDbContext db) =>
        {
            var list = await db.Users.Select(u => new
            {
                u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role, u.MembershipTier,
                u.LoyaltyPoints, u.IsSuspended, u.KtpVerified, u.SimVerified, u.RegisteredAt
            }).OrderByDescending(u => u.RegisteredAt).Take(100).ToListAsync();
            return Results.Ok(list);
        }).WithName("GetUsers");

        users.MapGet("/{id}", async (AppDbContext db, string id) =>
        {
            var u = await db.Users.FindAsync(id);
            return u is null ? Results.NotFound() : Results.Ok(new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role, u.MembershipTier, u.LoyaltyPoints, u.KtpVerified, u.SimVerified, u.IsSuspended });
        }).WithName("GetUserById");

        users.MapPut("/{id}/suspend", async (AppDbContext db, string id) =>
        {
            var u = await db.Users.FindAsync(id);
            if (u is null) return Results.NotFound();
            u.IsSuspended = !u.IsSuspended;
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id, u.IsSuspended });
        }).WithName("ToggleSuspendUser");

        // ═══════════════════════════════════════════
        // ⭐ REVIEWS
        // ═══════════════════════════════════════════
        var reviews = api.MapGroup("/reviews").WithTags("Reviews");

        reviews.MapPost("/", async ([FromServices] ReviewService svc, Review review) =>
            Results.Ok(await svc.CreateReviewAsync(review))).WithName("CreateReview");

        reviews.MapGet("/vehicle/{vehicleId:int}", async ([FromServices] ReviewService svc, int vehicleId) =>
            Results.Ok(await svc.GetVehicleReviewsAsync(vehicleId))).WithName("GetReviewsByVehicle");

        // ═══════════════════════════════════════════
        // 💰 PAYMENTS
        // ═══════════════════════════════════════════
        var payments = api.MapGroup("/payments").WithTags("Payments");

        payments.MapPost("/", async ([FromServices] PaymentService svc, [FromBody] CreatePaymentRequest req) =>
            Results.Ok(await svc.CreatePaymentAsync(req.BookingId, req.Method, req.Amount))).WithName("CreatePayment");

        payments.MapPost("/{bookingId:int}/confirm", async ([FromServices] PaymentService svc, int bookingId, [FromBody] ConfirmPaymentRequest req) =>
            Results.Ok(new { confirmed = await svc.ConfirmPaymentAsync(bookingId, req.ExternalTransactionId) })).WithName("ConfirmPayment");

        // ═══════════════════════════════════════════
        // 🎁 PROMOTIONS
        // ═══════════════════════════════════════════
        var promos = api.MapGroup("/promotions").WithTags("Promotions");

        promos.MapGet("/", async ([FromServices] PromotionService svc) =>
            Results.Ok(await svc.GetActivePromotionsAsync())).WithName("GetPromotions");

        promos.MapGet("/validate/{code}", async ([FromServices] PromotionService svc, string code, [FromQuery] string? tier) =>
        {
            var p = await svc.ValidateCouponAsync(code, tier);
            return p is null ? Results.NotFound(new { valid = false, message = "Invalid or expired coupon" }) : Results.Ok(new { valid = true, coupon = p });
        }).WithName("ValidateCoupon");

        // ═══════════════════════════════════════════
        // 🔔 NOTIFICATIONS
        // ═══════════════════════════════════════════
        var notifications = api.MapGroup("/notifications").WithTags("Notifications");

        notifications.MapGet("/{userId}", async ([FromServices] NotificationService svc, string userId, [FromQuery] bool unreadOnly = false) =>
            Results.Ok(await svc.GetUserNotificationsAsync(userId, unreadOnly))).WithName("GetNotifications");

        notifications.MapGet("/{userId}/count", async ([FromServices] NotificationService svc, string userId) =>
            Results.Ok(new { unread = await svc.GetUnreadCountAsync(userId) })).WithName("GetUnreadCount");

        notifications.MapPut("/{id:int}/read", async ([FromServices] NotificationService svc, int id) =>
        { await svc.MarkAsReadAsync(id); return Results.Ok(new { read = true }); }).WithName("MarkNotificationRead");

        // ═══════════════════════════════════════════
        // 🛰️ GPS / IoT
        // ═══════════════════════════════════════════
        var gps = api.MapGroup("/gps").WithTags("GPS & IoT");

        gps.MapGet("/vehicle/{vehicleId:int}", async ([FromServices] GpsSimulatorService svc, int vehicleId) =>
        {
            var status = await svc.GetVehicleGpsStatusAsync(vehicleId);
            return status is null ? Results.NotFound() : Results.Ok(status);
        }).WithName("GetVehicleGpsStatus");

        gps.MapPost("/vehicle/{vehicleId:int}/lock/toggle", async ([FromServices] GpsSimulatorService svc, int vehicleId) =>
            Results.Ok(new { lockStatus = await svc.ToggleLockAsync(vehicleId) })).WithName("ToggleLock");

        gps.MapPost("/vehicle/{vehicleId:int}/engine/toggle", async ([FromServices] GpsSimulatorService svc, int vehicleId) =>
            Results.Ok(new { engineStatus = await svc.ToggleEngineAsync(vehicleId) })).WithName("ToggleEngine");

        gps.MapPost("/vehicle/{vehicleId:int}/track/start", async ([FromServices] GpsSimulatorService svc, int vehicleId) =>
        { await svc.StartTrackingAsync(vehicleId); return Results.Ok(new { tracking = true }); }).WithName("StartTracking");

        gps.MapPost("/vehicle/{vehicleId:int}/track/stop", async ([FromServices] GpsSimulatorService svc, int vehicleId) =>
        { await svc.StopTrackingAsync(vehicleId); return Results.Ok(new { tracking = false }); }).WithName("StopTracking");

        // ═══════════════════════════════════════════
        // 🤖 CHAT BOT
        // ═══════════════════════════════════════════
        var chat = api.MapGroup("/chat").WithTags("Chat Bot");

        chat.MapPost("/send", async ([FromServices] BotService svc, [FromBody] ChatRequest req) =>
        {
            var history = new List<ChatHistory>();
            var response = await svc.ChatAsync(req.Message, history, req.ImageUrl, req.DocumentUrl);
            return Results.Ok(new { message = response, model = svc.CurrentModel });
        }).WithName("ChatSend");

        // ═══════════════════════════════════════════
        // 📊 DASHBOARD STATS
        // ═══════════════════════════════════════════
        var dashboard = api.MapGroup("/dashboard").WithTags("Dashboard");

        dashboard.MapGet("/admin", async ([FromServices] BookingService bookingSvc, [FromServices] VehicleService vehicleSvc) =>
        {
            var bookingStats = await bookingSvc.GetAdminStatsAsync();
            var vehicleStats = await vehicleSvc.GetVehicleStatsAsync();
            return Results.Ok(new { bookings = bookingStats, vehicles = vehicleStats });
        }).WithName("GetAdminDashboard");
    }
}

// ─── Request DTOs ────────────────────────────────
// (dipisahkan ke file tersendiri untuk kebersihan)

public record RejectRequest(string Reason);
public record CreatePaymentRequest(int BookingId, PaymentMethod Method, decimal Amount);
public record ConfirmPaymentRequest(string ExternalTransactionId);
public record ChatRequest(string Message, int SessionId = 0, string? ImageUrl = null, string? DocumentUrl = null);

/// <summary>
/// Request untuk update lokasi kendaraan (dipakai simulator & tracking device)
/// </summary>
public record VehicleLocationRequest(
    double Latitude,
    double Longitude,
    double Speed = 0,
    double Heading = 0,
    string? Address = null
);

/// <summary>
/// Request untuk update status IoT kendaraan (lock, engine, motion)
/// </summary>
public record VehicleIoTRequest(
    LockStatus? LockStatus = null,
    EngineStatus? EngineStatus = null,
    VehicleMotionStatus? MotionStatus = null
);

/// <summary>
/// Request untuk update kondisi fisik kendaraan
/// </summary>
public record VehicleConditionRequest(
    string? Color = null,
    bool? IsAvailable = null,
    string? Description = null,
    string? Specifications = null
);

/// <summary>
/// Request untuk update harga kendaraan
/// </summary>
public record VehiclePricingRequest(
    decimal? PricePerHour = null,
    decimal? PricePerDay = null,
    decimal? DynamicPriceMultiplier = null,
    bool? InsuranceAvailable = null,
    decimal? InsuranceCostPerDay = null
);

/// <summary>
/// Request untuk full simulator update (GPS + IoT + Speed + Address)
/// Dipakai oleh GPS Simulator untuk update semua data kendaraan dalam satu panggilan
/// </summary>
public record SimulatorUpdateRequest(
    double? Latitude = null,
    double? Longitude = null,
    double? Speed = null,
    double? Heading = null,
    string? LockStatus = null,       // "Locked" | "Unlocked"
    string? EngineStatus = null,     // "On" | "Off"
    string? MotionStatus = null,     // "Moving" | "Stopped"
    string? Address = null
);

/// <summary>
/// Item untuk batch simulator update
/// </summary>
public record SimulatorUpdateItem(
    int VehicleId,
    double? Latitude = null,
    double? Longitude = null,
    double? Speed = null,
    double? Heading = null,
    string? LockStatus = null,
    string? EngineStatus = null,
    string? MotionStatus = null
);
