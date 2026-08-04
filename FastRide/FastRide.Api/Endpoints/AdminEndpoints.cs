using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Shared.Storage;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>User management and service health.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/health", Health)
            .WithTags("Health")
            .AllowAnonymous()
            .WithSummary("Liveness plus the providers currently in use");

        var users = api.MapGroup("/admin/users")
            .WithTags("Admin · Users")
            .RequireAuthorization(Policies.AdminOnly);

        users.MapGet("/", ListUsers);
        users.MapPut("/{userId:guid}/active", SetActive);
        users.MapPut("/{userId:guid}/verify", SetVerified);

        api.MapGet("/admin/drivers/pending-verification", PendingVerification)
            .WithTags("Admin · Users")
            .RequireAuthorization(Policies.AdminOnly);

        return api;
    }

    private static async Task<IResult> Health(
        IConfiguration config, IStorageProvider storage, ICacheService cache,
        FastRideDbContext db, CancellationToken ct)
    {
        var database = config["Database:Provider"] ?? "SQLite";

        // Report degraded rather than healthy if the database is unreachable — a health check
        // that only proves the process is running is not worth polling.
        var reachable = await db.Database.CanConnectAsync(ct);

        var response = new HealthResponse(
            reachable ? "healthy" : "degraded",
            DateTime.UtcNow,
            "2.0.0",
            database,
            storage.Name,
            cache.Provider);

        return reachable ? Results.Ok(response) : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> ListUsers(
        int? page, int? limit, string? search, UserRole? role, bool? active,
        FastRideDbContext db, CancellationToken ct)
    {
        var paging = PageRequest.From(page, limit);
        var users = db.Users.AsNoTracking();

        if (role is { } wantedRole) users = users.Where(u => u.Role == wantedRole);
        if (active is { } isActive) users = users.Where(u => u.IsActive == isActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            users = users.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.PhoneNumber.Contains(term));
        }

        var total = await users.CountAsync(ct);
        var data = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Limit)
            .Select(u => new UserProfileResponse(
                u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role,
                u.IsVerified, u.IsActive, u.CreatedAt, u.PhotoUrl, u.ProfilePhotoMimeType,
                u.DriverProfile == null
                    ? null
                    : new DriverProfileResponse(
                        u.DriverProfile.LicenseNumber, u.DriverProfile.VehicleType, u.DriverProfile.VehiclePlate,
                        u.DriverProfile.VehicleCategory, u.DriverProfile.Status,
                        u.DriverProfile.Rating, u.DriverProfile.RatingCount,
                        u.DriverProfile.TotalTrips, u.DriverProfile.TotalEarnings,
                        u.DriverProfile.CurrentLatitude, u.DriverProfile.CurrentLongitude,
                        u.DriverProfile.IsDocumentVerified, u.DriverProfile.VerifiedAt),
                null))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<UserProfileResponse>
        {
            Total = total,
            Page = paging.Page,
            Limit = paging.Limit,
            Data = data
        });
    }

    private static async Task<IResult> SetActive(
        Guid userId, SetUserActiveRequest request, HttpContext http,
        FastRideDbContext db, ICacheService cache, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Results.NotFound(new ApiError("NotFound", "Pengguna tidak ditemukan."));

        // An admin locking themselves out would need database access to recover.
        if (user.Id == http.User.UserId() && !request.IsActive)
            return Results.BadRequest(new ApiError("Invalid", "Kamu tidak bisa menonaktifkan akun sendiri."));

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        // Suspension must end the session immediately, not when the token happens to expire.
        if (!request.IsActive) user.SecurityStamp++;

        if (!request.IsActive && user.Role == UserRole.Driver)
        {
            var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
            if (profile is not null) profile.Status = DriverStatus.Offline;
        }

        db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = request.IsActive ? "Akun diaktifkan kembali" : "Akun dinonaktifkan",
            Message = request.Reason ?? (request.IsActive
                ? "Akun kamu bisa dipakai lagi."
                : "Akun kamu dinonaktifkan. Hubungi dukungan FastRide."),
            Type = NotificationType.System
        });

        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.SecurityStamp(userId), ct);

        return Results.Ok(new MessageResponse(request.IsActive ? "Pengguna diaktifkan." : "Pengguna dinonaktifkan."));
    }

    private static async Task<IResult> SetVerified(Guid userId, FastRideDbContext db, CancellationToken ct)
    {
        var affected = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsVerified, true), ct);

        return affected == 0
            ? Results.NotFound(new ApiError("NotFound", "Pengguna tidak ditemukan."))
            : Results.Ok(new MessageResponse("Pengguna terverifikasi."));
    }

    /// <summary>Drivers waiting for document review — the admin's verification queue.</summary>
    private static async Task<IResult> PendingVerification(FastRideDbContext db, CancellationToken ct)
    {
        var pending = await db.DriverProfiles
            .AsNoTracking()
            .Where(p => !p.IsDocumentVerified)
            .OrderBy(p => p.User.CreatedAt)
            .Take(100)
            .Select(p => new
            {
                p.Id,
                DriverId = p.UserId,
                p.User.FullName,
                p.User.Email,
                p.User.PhotoUrl,
                p.VehicleType,
                p.VehiclePlate,
                JoinedAt = p.User.CreatedAt
            })
            .ToListAsync(ct);

        if (pending.Count == 0) return Results.Ok(Array.Empty<object>());

        // Documents are fetched separately: projecting a collection alongside a paged parent
        // needs APPLY, which SQLite cannot do.
        var profileIds = pending.Select(p => p.Id).ToList();
        var documents = await db.DriverDocuments
            .AsNoTracking()
            .Where(d => profileIds.Contains(d.DriverProfileId))
            .OrderBy(d => d.Type)
            .Select(d => new
            {
                d.DriverProfileId,
                Document = new DriverDocumentResponse(
                    d.Id, d.Type, d.Status, d.FileUrl, d.Notes, d.ExpiresAt, d.UploadedAt, d.ReviewedAt)
            })
            .ToListAsync(ct);

        var byProfile = documents
            .GroupBy(d => d.DriverProfileId)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Document).ToList());

        return Results.Ok(pending.Select(p => new
        {
            p.DriverId,
            p.FullName,
            p.Email,
            p.PhotoUrl,
            p.VehicleType,
            p.VehiclePlate,
            p.JoinedAt,
            Documents = byProfile.GetValueOrDefault(p.Id, [])
        }));
    }
}
