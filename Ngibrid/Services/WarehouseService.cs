using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Warehouse & inventory management service
/// </summary>
public class WarehouseService
{
    private readonly NgibridDbContext _db;
    private readonly AuditService _audit;

    public WarehouseService(NgibridDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<Warehouse>> GetAllWarehousesAsync() =>
        await _db.Warehouses.Where(w => !w.IsDeleted).ToListAsync();

    public async Task<Warehouse?> GetWarehouseAsync(long id) =>
        await _db.Warehouses.Include(w => w.Sections).FirstOrDefaultAsync(w => w.Id == id);

    public async Task<InventoryItem> AddInventoryAsync(InventoryItem item)
    {
        item.Sku ??= $"SKU-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ADD_INVENTORY", "InventoryItem", item.Id, notes: $"Added {item.Name} x{item.Quantity}");
        return item;
    }

    public async Task<InventoryMovement> RecordMovementAsync(long itemId, string type, int qty, string? notes = null)
    {
        var item = await _db.InventoryItems.FindAsync(itemId) 
            ?? throw new KeyNotFoundException();
        
        var balanceAfter = type == "IN" ? item.Quantity + qty : item.Quantity - qty;
        item.Quantity = balanceAfter;
        
        var movement = new InventoryMovement
        {
            InventoryItemId = itemId,
            MovementType = type,
            Quantity = qty,
            BalanceAfter = balanceAfter,
            Notes = notes
        };
        _db.InventoryMovements.Add(movement);
        await _db.SaveChangesAsync();
        return movement;
    }

    /// <summary>
    /// Search inventory by name/SKU/RFID/barcode. Ordered before Take so the 100-row cap
    /// returns a stable, meaningful page rather than an arbitrary slice.
    /// </summary>
    public async Task<List<InventoryItem>> SearchInventoryAsync(string? query = null, long? warehouseId = null,
        int take = 100)
    {
        var items = _db.InventoryItems
            .Include(i => i.Section)
            .Where(i => !i.IsDeleted);

        if (warehouseId.HasValue)
            items = items.Where(i => i.WarehouseId == warehouseId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            items = items.Where(i =>
                i.Name.Contains(term) ||
                i.Sku.Contains(term) ||
                (i.RfidTag != null && i.RfidTag.Contains(term)) ||
                (i.Barcode != null && i.Barcode.Contains(term)));
        }

        return await items
            .OrderBy(i => i.Sku)
            .Take(take)
            .ToListAsync();
    }

    public async Task UpdateSensorReadingAsync(long itemId, double temp, double humidity)
    {
        var item = await _db.InventoryItems.FindAsync(itemId);
        if (item != null)
        {
            item.CurrentTemperature = temp;
            item.CurrentHumidity = humidity;
            item.LastSensorReading = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Packaging optimization - recommend best box size
    /// </summary>
    public (string BoxType, double WastedVolume) RecommendBox(double length, double width, double height)
    {
        var volume = length * width * height;
        // Standard box sizes (in cm)
        var boxes = new (string Name, double L, double W, double H, double Vol)[]
        {
            ("Small (20x15x10)", 20, 15, 10, 3000),
            ("Medium (30x25x20)", 30, 25, 20, 15000),
            ("Large (40x35x30)", 40, 35, 30, 42000),
            ("X-Large (50x45x40)", 50, 45, 40, 90000),
            ("Jumbo (60x55x50)", 60, 55, 50, 165000)
        };

        foreach (var box in boxes)
        {
            if (length <= box.L && width <= box.W && height <= box.H)
            {
                var wasted = ((box.Vol - volume) / box.Vol) * 100;
                return (box.Name, Math.Round(wasted, 1));
            }
        }
        return ("Custom Size Required", 0);
    }
}
