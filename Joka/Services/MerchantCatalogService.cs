// Write operations for the merchant portal.
//
// Split of authority, deliberate:
//   price changes  -> queued as ApprovalRequest, admin applies them
//   inventory      -> applied immediately (day-to-day operations)
//   content        -> applied immediately (photos, description)
//   new records    -> queued, because they enter the public catalogue
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Backoffice;
using Joka.Models.Buses;
using Joka.Models.Common;

namespace Joka.Services;

public record CatalogResult(bool Success, string Message);

public class MerchantCatalogService
{
    private readonly AppDbContext _db;

    public MerchantCatalogService(AppDbContext db) => _db = db;

    // ------------------------------------------------------------------
    // Price -> approval queue
    // ------------------------------------------------------------------
    public async Task<CatalogResult> ProposePriceAsync(
        Merchant merchant, string entityType, Guid entityId,
        string label, decimal currentPrice, decimal newPrice, string requestedBy)
    {
        if (newPrice <= 0)
            return new(false, "Harga harus lebih besar dari nol.");

        if (newPrice == currentPrice)
            return new(false, "Harga baru sama dengan harga sekarang.");

        var alreadyQueued = await _db.ApprovalRequests.AnyAsync(a =>
            a.MerchantId == merchant.Id && a.EntityType == entityType &&
            a.EntityId == entityId.ToString() && a.Status == "Pending");

        if (alreadyQueued)
            return new(false, "Sudah ada pengajuan untuk item ini yang menunggu persetujuan.");

        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            MerchantId = merchant.Id,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            ChangeType = "Update",
            Summary = $"Ubah harga {label} dari {Rupiah(currentPrice)} ke {Rupiah(newPrice)}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { Price = newPrice }),
            RequestedBy = requestedBy,
            Status = "Pending"
        });

        await _db.SaveChangesAsync();
        return new(true, "Pengajuan harga dikirim ke admin.");
    }

    // ------------------------------------------------------------------
    // Inventory -> applied directly
    // ------------------------------------------------------------------
    public async Task<CatalogResult> UpdateRoomInventoryAsync(Guid roomId, int total, int available, string actor)
    {
        if (total < 0 || available < 0) return new(false, "Jumlah tidak boleh negatif.");
        if (available > total) return new(false, "Kamar tersedia tidak boleh melebihi total.");

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId);
        if (room is null) return new(false, "Kamar tidak ditemukan.");

        Audit("Room", room.Name, $"TotalRooms {room.TotalRooms}->{total}, Available {room.AvailableRooms}->{available}", actor);

        room.TotalRooms = total;
        room.AvailableRooms = available;
        room.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new(true, "Inventori kamar diperbarui.");
    }

    public async Task<CatalogResult> UpdateScheduleSeatsAsync(Guid scheduleId, int available, string actor)
    {
        if (available < 0) return new(false, "Kursi tidak boleh negatif.");

        var schedule = await _db.BusSchedules.Include(s => s.BusService)
            .FirstOrDefaultAsync(s => s.Id == scheduleId);

        if (schedule is null) return new(false, "Jadwal tidak ditemukan.");

        var capacity = schedule.BusService?.TotalSeats ?? int.MaxValue;
        if (available > capacity)
            return new(false, $"Melebihi kapasitas armada ({capacity} kursi).");

        Audit("BusSchedule", schedule.Id.ToString(), $"AvailableSeats {schedule.AvailableSeats}->{available}", actor);

        schedule.AvailableSeats = available;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new(true, "Sisa kursi diperbarui.");
    }

    public async Task<CatalogResult> UpdateActivityQuotaAsync(Guid activityId, int total, string actor)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity is null) return new(false, "Aktivitas tidak ditemukan.");

        if (total < activity.SoldTickets)
            return new(false, $"Kuota tidak boleh di bawah tiket terjual ({activity.SoldTickets}).");

        Audit("Activity", activity.Name, $"TotalTickets {activity.TotalTickets}->{total}", actor);

        activity.TotalTickets = total;
        activity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new(true, "Kuota tiket diperbarui.");
    }

    // ------------------------------------------------------------------
    // Content -> applied directly
    // ------------------------------------------------------------------
    public async Task<CatalogResult> UpdateRoomContentAsync(
        Guid roomId, string? description, string? imageUrl, List<string> gallery, string actor)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId);
        if (room is null) return new(false, "Kamar tidak ditemukan.");

        if (!string.IsNullOrWhiteSpace(imageUrl)) room.ImageUrl = imageUrl;
        if (gallery.Count > 0) room.ImageUrls = System.Text.Json.JsonSerializer.Serialize(gallery);
        room.UpdatedAt = DateTime.UtcNow;

        Audit("Room", room.Name, $"Konten diperbarui ({gallery.Count} foto)", actor);

        await _db.SaveChangesAsync();
        return new(true, "Konten kamar diperbarui.");
    }

    public async Task<CatalogResult> UpdateActivityContentAsync(
        Guid activityId, string? description, string? imageUrl, List<string> gallery, string actor)
    {
        var activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity is null) return new(false, "Aktivitas tidak ditemukan.");

        if (!string.IsNullOrWhiteSpace(description)) activity.Description = description;
        if (!string.IsNullOrWhiteSpace(imageUrl)) activity.ImageUrl = imageUrl;
        if (gallery.Count > 0) activity.ImageUrls = System.Text.Json.JsonSerializer.Serialize(gallery);
        activity.UpdatedAt = DateTime.UtcNow;

        Audit("Activity", activity.Name, $"Konten diperbarui ({gallery.Count} foto)", actor);

        await _db.SaveChangesAsync();
        return new(true, "Konten aktivitas diperbarui.");
    }

    // ------------------------------------------------------------------
    // New records -> approval queue, because they enter the public catalogue
    // ------------------------------------------------------------------
    public async Task<CatalogResult> ProposeScheduleAsync(
        Merchant merchant, Guid busServiceId, Guid fromTerminalId, Guid toTerminalId,
        DateTime departure, int durationMinutes, decimal price, string requestedBy)
    {
        if (fromTerminalId == toTerminalId)
            return new(false, "Terminal asal dan tujuan tidak boleh sama.");

        if (departure <= DateTime.UtcNow)
            return new(false, "Waktu keberangkatan harus di masa depan.");

        if (durationMinutes <= 0) return new(false, "Durasi harus lebih dari nol.");
        if (price <= 0) return new(false, "Harga harus lebih besar dari nol.");

        var service = await _db.BusServices.AsNoTracking().FirstOrDefaultAsync(b => b.Id == busServiceId);
        var from = await _db.BusTerminals.AsNoTracking().FirstOrDefaultAsync(t => t.Id == fromTerminalId);
        var to = await _db.BusTerminals.AsNoTracking().FirstOrDefaultAsync(t => t.Id == toTerminalId);

        if (service is null || from is null || to is null)
            return new(false, "Armada atau terminal tidak valid.");

        if (service.MerchantId != merchant.Id)
            return new(false, "Armada itu bukan milik partner ini.");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            BusServiceId = busServiceId,
            DepartureTerminalId = fromTerminalId,
            ArrivalTerminalId = toTerminalId,
            DepartureTime = departure,
            DurationMinutes = durationMinutes,
            Price = price
        });

        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            MerchantId = merchant.Id,
            EntityType = "BusSchedule",
            ChangeType = "Create",
            Summary = $"Tambah keberangkatan {service.Name}: {from.City} → {to.City}, " +
                      $"{departure:dd MMM yyyy HH:mm}, {Rupiah(price)}",
            PayloadJson = payload,
            RequestedBy = requestedBy,
            Status = "Pending"
        });

        await _db.SaveChangesAsync();
        return new(true, "Jadwal baru dikirim ke admin untuk disetujui.");
    }

    public async Task<CatalogResult> ProposePackageAsync(
        Merchant merchant, string name, string destination, int days,
        decimal price, List<string> includes, string? imageUrl, string requestedBy)
    {
        if (string.IsNullOrWhiteSpace(name)) return new(false, "Nama paket wajib diisi.");
        if (string.IsNullOrWhiteSpace(destination)) return new(false, "Destinasi wajib diisi.");
        if (days <= 0) return new(false, "Durasi minimal 1 hari.");
        if (price <= 0) return new(false, "Harga harus lebih besar dari nol.");
        if (includes.Count == 0) return new(false, "Isi minimal satu komponen paket.");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            Name = name,
            Destination = destination,
            DurationDays = days,
            Price = price,
            Includes = includes,
            ImageUrl = imageUrl
        });

        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            MerchantId = merchant.Id,
            EntityType = "TravelPackage",
            ChangeType = "Create",
            Summary = $"Paket baru \"{name}\" ke {destination}, {days} hari, {Rupiah(price)}",
            PayloadJson = payload,
            RequestedBy = requestedBy,
            Status = "Pending"
        });

        await _db.SaveChangesAsync();
        return new(true, "Paket dikirim ke admin untuk disetujui.");
    }

    public async Task<CatalogResult> ProposeRoomAsync(
        Merchant merchant, Guid hotelId, string name, string type,
        int capacity, decimal price, int totalRooms, bool breakfast, string requestedBy)
    {
        if (string.IsNullOrWhiteSpace(name)) return new(false, "Nama kamar wajib diisi.");
        if (capacity < 1) return new(false, "Kapasitas minimal 1 tamu.");
        if (price <= 0) return new(false, "Harga harus lebih besar dari nol.");
        if (totalRooms < 1) return new(false, "Jumlah kamar minimal 1.");

        var hotel = await _db.Hotels.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hotelId);
        if (hotel is null) return new(false, "Properti tidak ditemukan.");
        if (hotel.MerchantId != merchant.Id) return new(false, "Properti itu bukan milik partner ini.");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            HotelId = hotelId,
            Name = name,
            Type = type,
            Capacity = capacity,
            Price = price,
            TotalRooms = totalRooms,
            HasBreakfast = breakfast
        });

        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            MerchantId = merchant.Id,
            EntityType = "Room",
            ChangeType = "Create",
            Summary = $"Kamar baru \"{name}\" ({type}) di {hotel.Name}, {totalRooms} unit, {Rupiah(price)}/malam",
            PayloadJson = payload,
            RequestedBy = requestedBy,
            Status = "Pending"
        });

        await _db.SaveChangesAsync();
        return new(true, "Kamar baru dikirim ke admin untuk disetujui.");
    }

    public async Task<CatalogResult> ProposeActivityAsync(
        Merchant merchant, string name, string category, string city, string? location,
        decimal price, int totalTickets, int durationMinutes, string? description, string? imageUrl, string requestedBy)
    {
        if (string.IsNullOrWhiteSpace(name)) return new(false, "Nama aktivitas wajib diisi.");
        if (string.IsNullOrWhiteSpace(city)) return new(false, "Kota wajib diisi.");
        if (price <= 0) return new(false, "Harga harus lebih besar dari nol.");
        if (totalTickets < 1) return new(false, "Kuota tiket minimal 1.");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            Name = name,
            Category = category,
            City = city,
            Location = location,
            Price = price,
            TotalTickets = totalTickets,
            DurationMinutes = durationMinutes,
            Description = description,
            ImageUrl = imageUrl,
            MerchantId = merchant.Id
        });

        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            MerchantId = merchant.Id,
            EntityType = "Activity",
            ChangeType = "Create",
            Summary = $"Aktivitas baru \"{name}\" ({category}) di {city}, {totalTickets} tiket, {Rupiah(price)}",
            PayloadJson = payload,
            RequestedBy = requestedBy,
            Status = "Pending"
        });

        await _db.SaveChangesAsync();
        return new(true, "Aktivitas baru dikirim ke admin untuk disetujui.");
    }

    public async Task<CatalogResult> ProposeDeleteAsync(
        Merchant merchant, string entityType, Guid entityId, string label, string requestedBy)
    {
        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            MerchantId = merchant.Id,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            ChangeType = "Delete",
            Summary = $"Hapus {entityType} \"{label}\" dari katalog",
            PayloadJson = "{}",
            RequestedBy = requestedBy,
            Status = "Pending"
        });

        await _db.SaveChangesAsync();
        return new(true, "Permintaan hapus dikirim ke admin.");
    }

    private void Audit(string entity, string id, string change, string actor) =>
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = entity, EntityId = id, Action = "Update",
            Changes = change, UserId = actor, Timestamp = DateTime.UtcNow
        });

    private static string Rupiah(decimal value) => $"Rp{value:N0}";
}
