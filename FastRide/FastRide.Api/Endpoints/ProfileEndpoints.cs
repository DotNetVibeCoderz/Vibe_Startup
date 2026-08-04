using FastRide.Api.Security;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Shared.Storage;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>Profile read/update, photo upload and driver document verification.</summary>
public static class ProfileEndpoints
{
    private const long MaxPhotoBytes = 2 * 1024 * 1024;      // 2 MB
    private const long MaxDocumentBytes = 5 * 1024 * 1024;   // 5 MB

    private static readonly string[] AllowedImageTypes =
        ["image/jpeg", "image/jpg", "image/png", "image/webp"];

    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/profile").WithTags("Profile").RequireAuthorization();

        group.MapGet("/{userId:guid}", GetProfile);
        group.MapPut("/{userId:guid}", UpdateProfile);
        group.MapDelete("/{userId:guid}/photo", DeletePhoto);
        group.MapPut("/{userId:guid}/driver", UpdateDriverProfile);

        var documents = api.MapGroup("/drivers/{userId:guid}/documents")
            .WithTags("Driver Documents")
            .RequireAuthorization();

        documents.MapGet("/", ListDocuments);
        documents.MapPost("/", UploadDocument);
        documents.MapPut("/{documentId:guid}/review", ReviewDocument).RequireAuthorization(Policies.AdminOnly);

        return api;
    }

    private static async Task<IResult> GetProfile(Guid userId, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var profile = await LoadProfileAsync(db, userId, ct);
        return profile is null ? NotFound("Pengguna tidak ditemukan.") : Results.Ok(profile);
    }

    /// <summary>
    /// Accepts either a multipart form (with a <c>photo</c> file) or a JSON body carrying a
    /// base64 image. Both paths end up on the configured storage provider.
    /// </summary>
    private static async Task<IResult> UpdateProfile(
        Guid userId, HttpRequest request, HttpContext http,
        FastRideDbContext db, IStorageProvider storage, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound("Pengguna tidak ditemukan.");

        string? newPhotoUrl = null;
        string? newMimeType = null;

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(ct);

            if (!string.IsNullOrWhiteSpace(form["fullName"])) user.FullName = form["fullName"].ToString().Trim();
            if (!string.IsNullOrWhiteSpace(form["phoneNumber"])) user.PhoneNumber = form["phoneNumber"].ToString().Trim();

            var file = form.Files.GetFile("photo");
            if (file is { Length: > 0 })
            {
                if (file.Length > MaxPhotoBytes)
                    return Invalid($"Ukuran foto maksimal {MaxPhotoBytes / 1024 / 1024} MB.");

                if (!AllowedImageTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                    return Invalid("Format foto harus JPEG, PNG atau WebP.");

                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, ct);

                var fileName = storage.GeneratePhotoFileName(userId, Path.GetExtension(file.FileName));
                newPhotoUrl = await storage.UploadAsync(fileName, buffer.ToArray(), file.ContentType, ct);
                newMimeType = file.ContentType;
            }
        }
        else
        {
            var update = await request.ReadFromJsonAsync<UpdateProfileRequest>(ct);
            if (update is null) return Invalid("Body permintaan tidak bisa dibaca.");

            if (!string.IsNullOrWhiteSpace(update.FullName)) user.FullName = update.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(update.PhoneNumber)) user.PhoneNumber = update.PhoneNumber.Trim();

            if (!string.IsNullOrWhiteSpace(update.ProfilePhotoBase64))
            {
                if (!TryDecodeBase64(update.ProfilePhotoBase64, MaxPhotoBytes, out var bytes, out var error))
                    return Invalid(error);

                var mimeType = update.ProfilePhotoMimeType ?? "image/jpeg";
                if (!AllowedImageTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
                    return Invalid("Format foto harus JPEG, PNG atau WebP.");

                var extension = mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? "png"
                    : mimeType.Contains("webp", StringComparison.OrdinalIgnoreCase) ? "webp" : "jpg";

                var fileName = storage.GeneratePhotoFileName(userId, extension);
                newPhotoUrl = await storage.UploadAsync(fileName, bytes, mimeType, ct);
                newMimeType = mimeType;
            }
        }

        if (newPhotoUrl is not null)
        {
            await TryDeleteStoredFile(storage, user.PhotoUrl, ct);
            user.PhotoUrl = newPhotoUrl;
            user.ProfilePhotoMimeType = newMimeType;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new ProfilePhotoResponse(
            user.Id, user.FullName, user.PhoneNumber, user.PhotoUrl, user.ProfilePhotoMimeType, user.UpdatedAt));
    }

    private static async Task<IResult> DeletePhoto(
        Guid userId, HttpContext http, FastRideDbContext db, IStorageProvider storage, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound("Pengguna tidak ditemukan.");

        await TryDeleteStoredFile(storage, user.PhotoUrl, ct);

        // Fall back to the generated avatar rather than leaving an empty slot in the apps.
        user.PhotoUrl = AuthEndpoints.GenerateAvatar(user.FullName);
        user.ProfilePhotoMimeType = "image/svg+xml";
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new ProfilePhotoResponse(
            user.Id, user.FullName, user.PhoneNumber, user.PhotoUrl, user.ProfilePhotoMimeType, user.UpdatedAt));
    }

    private static async Task<IResult> UpdateDriverProfile(
        Guid userId, UpdateDriverProfileRequest request, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound("Profil driver tidak ditemukan.");

        if (!string.IsNullOrWhiteSpace(request.LicenseNumber)) profile.LicenseNumber = request.LicenseNumber.Trim();
        if (!string.IsNullOrWhiteSpace(request.VehicleType)) profile.VehicleType = request.VehicleType.Trim();
        if (!string.IsNullOrWhiteSpace(request.VehiclePlate)) profile.VehiclePlate = request.VehiclePlate.Trim().ToUpperInvariant();
        if (request.VehicleCategory is not null) profile.VehicleCategory = request.VehicleCategory.Value;

        await db.SaveChangesAsync(ct);

        var updated = await LoadProfileAsync(db, userId, ct);
        return Results.Ok(updated);
    }

    // ─────────────────────── driver documents ───────────────────────

    private static async Task<IResult> ListDocuments(Guid userId, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var documents = await db.DriverDocuments
            .AsNoTracking()
            .Where(d => d.DriverProfile.UserId == userId)
            .OrderBy(d => d.Type)
            .Select(d => new DriverDocumentResponse(
                d.Id, d.Type, d.Status, d.FileUrl, d.Notes, d.ExpiresAt, d.UploadedAt, d.ReviewedAt))
            .ToListAsync(ct);

        return Results.Ok(documents);
    }

    private static async Task<IResult> UploadDocument(
        Guid userId, UploadDocumentRequest request, HttpContext http,
        FastRideDbContext db, IStorageProvider storage, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound("Profil driver tidak ditemukan.");

        if (!TryDecodeBase64(request.FileBase64, MaxDocumentBytes, out var bytes, out var error))
            return Invalid(error);

        var mimeType = request.MimeType ?? "image/jpeg";
        var extension = mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ? "pdf"
            : mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";

        var fileName = storage.GenerateDocumentFileName(userId, request.Type.ToString(), extension);
        var url = await storage.UploadAsync(fileName, bytes, mimeType, ct);

        // Re-uploading a document type replaces the previous file and resets the review.
        var document = await db.DriverDocuments
            .FirstOrDefaultAsync(d => d.DriverProfileId == profile.Id && d.Type == request.Type, ct);

        if (document is null)
        {
            document = new DriverDocument { DriverProfileId = profile.Id, Type = request.Type };
            db.DriverDocuments.Add(document);
        }
        else
        {
            await TryDeleteStoredFile(storage, document.FileUrl, ct);
        }

        document.FileUrl = url;
        document.Status = DocumentStatus.Pending;
        document.ExpiresAt = request.ExpiresAt;
        document.UploadedAt = DateTime.UtcNow;
        document.ReviewedAt = null;
        document.ReviewedBy = null;
        document.Notes = null;

        // Any change puts the driver back in the queue until an admin re-approves.
        profile.IsDocumentVerified = false;
        profile.VerifiedAt = null;

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/drivers/{userId}/documents", new DriverDocumentResponse(
            document.Id, document.Type, document.Status, document.FileUrl,
            document.Notes, document.ExpiresAt, document.UploadedAt, document.ReviewedAt));
    }

    private static async Task<IResult> ReviewDocument(
        Guid userId, Guid documentId, ReviewDocumentRequest request,
        HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        var document = await db.DriverDocuments
            .Include(d => d.DriverProfile)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.DriverProfile.UserId == userId, ct);

        if (document is null) return NotFound("Dokumen tidak ditemukan.");

        document.Status = request.Status;
        document.Notes = request.Notes;
        document.ReviewedAt = DateTime.UtcNow;
        document.ReviewedBy = http.User.UserId();

        // The driver is verified only once every required document is approved.
        var required = new[] { DocumentType.DriverLicense, DocumentType.VehicleRegistration, DocumentType.IdentityCard };
        var approved = await db.DriverDocuments
            .Where(d => d.DriverProfileId == document.DriverProfileId &&
                        d.Status == DocumentStatus.Approved &&
                        required.Contains(d.Type))
            .Select(d => d.Type)
            .ToListAsync(ct);

        var nowComplete = required.All(type => approved.Contains(type) || (type == document.Type && request.Status == DocumentStatus.Approved));

        document.DriverProfile.IsDocumentVerified = nowComplete;
        document.DriverProfile.VerifiedAt = nowComplete ? DateTime.UtcNow : null;

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = request.Status == DocumentStatus.Approved ? "Dokumen disetujui" : "Dokumen ditolak",
            Message = request.Status == DocumentStatus.Approved
                ? $"Dokumen {document.Type} kamu sudah disetujui."
                : $"Dokumen {document.Type} ditolak. {request.Notes ?? "Silakan unggah ulang."}",
            Type = NotificationType.System
        });

        await db.SaveChangesAsync(ct);

        return Results.Ok(new DriverDocumentResponse(
            document.Id, document.Type, document.Status, document.FileUrl,
            document.Notes, document.ExpiresAt, document.UploadedAt, document.ReviewedAt));
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>Shared by /profile/{id} and /auth/me so both return exactly the same shape.</summary>
    internal static async Task<UserProfileResponse?> LoadProfileAsync(FastRideDbContext db, Guid userId, CancellationToken ct)
    {
        var record = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role, u.IsVerified, u.IsActive,
                u.CreatedAt, u.PhotoUrl, u.ProfilePhotoMimeType,
                Driver = u.DriverProfile == null
                    ? null
                    : new DriverProfileResponse(
                        u.DriverProfile.LicenseNumber, u.DriverProfile.VehicleType, u.DriverProfile.VehiclePlate,
                        u.DriverProfile.VehicleCategory, u.DriverProfile.Status,
                        u.DriverProfile.Rating, u.DriverProfile.RatingCount,
                        u.DriverProfile.TotalTrips, u.DriverProfile.TotalEarnings,
                        u.DriverProfile.CurrentLatitude, u.DriverProfile.CurrentLongitude,
                        u.DriverProfile.IsDocumentVerified, u.DriverProfile.VerifiedAt),
                RiderStats = u.Role != UserRole.Rider
                    ? null
                    : new RiderStatsResponse(
                        u.RiderOrders.Count(o => o.Status == OrderStatus.Completed),
                        u.RiderOrders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.FinalFare),
                        u.RiderOrders.Where(o => o.DriverRating != null).Average(o => (double?)o.DriverRating) ?? 0)
            })
            .FirstOrDefaultAsync(ct);

        if (record is null) return null;

        return new UserProfileResponse(
            record.Id, record.FullName, record.Email, record.PhoneNumber, record.Role,
            record.IsVerified, record.IsActive, record.CreatedAt,
            record.PhotoUrl, record.ProfilePhotoMimeType,
            record.Driver, record.RiderStats);
    }

    private static async Task TryDeleteStoredFile(IStorageProvider storage, string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // Generated avatars are data: URIs with no file behind them; ResolveFileName returns
        // null for those instead of the old code's attempt to parse them as a path.
        var fileName = storage.ResolveFileName(url);
        if (fileName is null) return;

        try
        {
            await storage.DeleteAsync(fileName, ct);
        }
        catch (Exception)
        {
            // A leftover blob is not worth failing the user's request over.
        }
    }

    private static bool TryDecodeBase64(string payload, long maxBytes, out byte[] bytes, out string error)
    {
        bytes = [];
        error = string.Empty;

        // Accept "data:image/png;base64,AAAA" as well as a bare payload.
        var commaIndex = payload.IndexOf(',');
        var raw = payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex > 0
            ? payload[(commaIndex + 1)..]
            : payload;

        // 4 base64 characters carry 3 bytes; check before allocating.
        if ((long)raw.Length / 4 * 3 > maxBytes)
        {
            error = $"Ukuran berkas maksimal {maxBytes / 1024 / 1024} MB.";
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(raw);
            return true;
        }
        catch (FormatException)
        {
            error = "Data base64 tidak valid.";
            return false;
        }
    }

    private static IResult NotFound(string message) => Results.NotFound(new ApiError("NotFound", message));
    private static IResult Invalid(string message) => Results.BadRequest(new ApiError("Invalid", message));
    private static IResult Forbidden() =>
        Results.Json(new ApiError("Forbidden", "Kamu tidak berhak mengakses data pengguna lain."), statusCode: StatusCodes.Status403Forbidden);
}
