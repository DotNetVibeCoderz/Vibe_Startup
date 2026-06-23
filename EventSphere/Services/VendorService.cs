using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

/// <summary>
/// Manajemen vendor: database, kontrak, review
/// </summary>
public class VendorService
{
    private readonly AppDbContext _db;
    public VendorService(AppDbContext db) => _db = db;

    public async Task<List<Vendor>> GetAllAsync(string? category = null)
    {
        var query = _db.Vendors.AsQueryable();
        if (!string.IsNullOrEmpty(category))
            query = query.Where(v => v.Category == category);
        return await query.OrderByDescending(v => v.Rating).ToListAsync();
    }

    public async Task<Vendor?> GetByIdAsync(Guid id) =>
        await _db.Vendors.Include(v => v.Contracts).Include(v => v.Reviews).Include(v => v.Portfolios).FirstOrDefaultAsync(v => v.Id == id);

    public async Task<Vendor> CreateAsync(Vendor vendor) { vendor.Id = Guid.NewGuid(); _db.Vendors.Add(vendor); await _db.SaveChangesAsync(); return vendor; }
    public async Task<bool> UpdateAsync(Vendor v) { var e = await _db.Vendors.FindAsync(v.Id); if (e == null) return false; _db.Entry(e).CurrentValues.SetValues(v); await _db.SaveChangesAsync(); return true; }
    public async Task<bool> DeleteAsync(Guid id) { var v = await _db.Vendors.FindAsync(id); if (v == null) return false; _db.Vendors.Remove(v); await _db.SaveChangesAsync(); return true; }

    // Contracts
    public async Task<List<VendorContract>> GetContractsForEventAsync(Guid eventId) =>
        await _db.VendorContracts.Where(vc => vc.EventId == eventId).Include(vc => vc.Vendor).Include(vc => vc.Invoices).ToListAsync();

    public async Task<VendorContract> CreateContractAsync(VendorContract c) { c.Id = Guid.NewGuid(); _db.VendorContracts.Add(c); await _db.SaveChangesAsync(); return c; }
    public async Task<bool> UpdateContractAsync(VendorContract c) { var e = await _db.VendorContracts.FindAsync(c.Id); if (e == null) return false; _db.Entry(e).CurrentValues.SetValues(c); await _db.SaveChangesAsync(); return true; }

    // Reviews
    public async Task<VendorReview> AddReviewAsync(VendorReview r) { r.Id = Guid.NewGuid(); _db.VendorReviews.Add(r);
        var vendor = await _db.Vendors.FindAsync(r.VendorId);
        if (vendor != null) { vendor.ReviewCount++; vendor.Rating = await _db.VendorReviews.Where(vr => vr.VendorId == r.VendorId).AverageAsync(vr => (decimal?)vr.Rating) ?? r.Rating; }
        await _db.SaveChangesAsync(); return r; }

    // Invoice
    public async Task<List<Invoice>> GetInvoicesForContractAsync(Guid contractId) =>
        await _db.Invoices.Where(i => i.ContractId == contractId).OrderByDescending(i => i.CreatedAt).ToListAsync();

    public async Task<Invoice> CreateInvoiceAsync(Invoice i) { i.Id = Guid.NewGuid(); _db.Invoices.Add(i); await _db.SaveChangesAsync(); return i; }
    public async Task<bool> UpdateInvoiceAsync(Invoice inv) { var e = await _db.Invoices.FindAsync(inv.Id); if (e == null) return false; _db.Entry(e).CurrentValues.SetValues(inv); await _db.SaveChangesAsync(); return true; }
}
