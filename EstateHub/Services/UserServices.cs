using Microsoft.EntityFrameworkCore;
using EstateHub.Data;
using EstateHub.Models;

namespace EstateHub.Services;

/// <summary>
/// Service for user operations and profile management
/// </summary>
public class UserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db) => _db = db;

    public async Task<ApplicationUser?> GetByIdAsync(string id)
    {
        return await _db.Users.FindAsync(id);
    }

    public async Task<ApplicationUser?> GetByPhoneAsync(string phone)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
    }

    public async Task<ApplicationUser> CreateUserAsync(ApplicationUser user)
    {
        user.CreatedAt = DateTime.UtcNow;
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(ApplicationUser user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    public async Task<List<ApplicationUser>> GetAgentsAsync()
    {
        return await _db.Users.Where(u => u.Role == "Agent" || u.Role == "Owner").ToListAsync();
    }

    public async Task<List<ApplicationUser>> GetAllUsersAsync(string? role = null)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);
        return await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    public async Task<int> GetUserCountAsync()
    {
        return await _db.Users.CountAsync();
    }

    public async Task DeleteUserAsync(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
    }
}

/// <summary>
/// Service for booking and scheduling
/// </summary>
public class BookingService
{
    private readonly AppDbContext _db;

    public BookingService(AppDbContext db) => _db = db;

    public async Task<Booking> CreateBookingAsync(Booking booking)
    {
        booking.Status = "Pending";
        booking.CreatedAt = DateTime.UtcNow;
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return booking;
    }

    public async Task<List<Booking>> GetUserBookingsAsync(string userId)
    {
        return await _db.Bookings
            .Where(b => b.UserId == userId)
            .Include(b => b.Property)
            .OrderByDescending(b => b.ScheduledDate)
            .ToListAsync();
    }

    public async Task<List<Booking>> GetPropertyBookingsAsync(int propertyId)
    {
        return await _db.Bookings
            .Where(b => b.PropertyId == propertyId)
            .Include(b => b.User)
            .OrderByDescending(b => b.ScheduledDate)
            .ToListAsync();
    }

    public async Task UpdateBookingStatusAsync(int id, string status)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking != null)
        {
            booking.Status = status;
            booking.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}

/// <summary>
/// Service for payment operations
/// </summary>
public class PaymentService
{
    private readonly AppDbContext _db;

    public PaymentService(AppDbContext db) => _db = db;

    public async Task<Payment> CreatePaymentAsync(Payment payment)
    {
        payment.CreatedAt = DateTime.UtcNow;
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return payment;
    }

    public async Task<List<Payment>> GetUserPaymentsAsync(string userId)
    {
        return await _db.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Payment?> UpdatePaymentStatusAsync(long id, string status, string? transactionId = null)
    {
        var payment = await _db.Payments.FindAsync(id);
        if (payment != null)
        {
            payment.Status = status;
            payment.TransactionId = transactionId;
            if (status == "Completed")
                payment.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return payment;
    }

    public async Task<decimal> GetTotalRevenueAsync()
    {
        return await _db.Payments.Where(p => p.Status == "Completed").SumAsync(p => p.Amount);
    }
}

/// <summary>
/// Service for digital contracts
/// </summary>
public class ContractService
{
    private readonly AppDbContext _db;

    public ContractService(AppDbContext db) => _db = db;

    public async Task<Contract> CreateContractAsync(Contract contract)
    {
        contract.CreatedAt = DateTime.UtcNow;
        contract.UpdatedAt = DateTime.UtcNow;
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        return contract;
    }

    public async Task<List<Contract>> GetUserContractsAsync(string userId)
    {
        return await _db.Contracts
            .Where(c => c.BuyerId == userId || c.SellerId == userId)
            .Include(c => c.Property)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Contract?> GetByIdAsync(long id)
    {
        return await _db.Contracts
            .Include(c => c.Property)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task UpdateContractAsync(Contract contract)
    {
        contract.UpdatedAt = DateTime.UtcNow;
        _db.Contracts.Update(contract);
        await _db.SaveChangesAsync();
    }

    public async Task<string> GenerateContractContentAsync(Property property, ApplicationUser buyer, ApplicationUser seller, string contractType)
    {
        // Template-based contract generation (can be enhanced with LLM)
        var template = contractType == "Sale" ?
            GenerateSaleContract(property, buyer, seller) :
            GenerateRentContract(property, buyer, seller);

        return template;
    }

    private string GenerateSaleContract(Property property, ApplicationUser buyer, ApplicationUser seller)
    {
        return $@"# PERJANJIAN JUAL BELI PROPERTI

Pada hari ini, tanggal {DateTime.Now:dd MMMM yyyy}, kami yang bertanda tangan di bawah ini:

## PIHAK PERTAMA (PENJUAL)
- **Nama**: {seller.FullName}
- **Alamat**: {seller.Address}
- **No. Telepon**: {seller.PhoneNumber}

## PIHAK KEDUA (PEMBELI)
- **Nama**: {buyer.FullName}
- **Alamat**: {buyer.Address}
- **No. Telepon**: {buyer.PhoneNumber}

## OBJEK JUAL BELI
- **Judul Properti**: {property.Title}
- **Alamat**: {property.Address}, {property.City}
- **Tipe**: {property.PropertyType}
- **Luas Tanah**: {property.LandArea} m²
- **Luas Bangunan**: {property.BuildingArea} m²
- **Kamar Tidur**: {property.Bedrooms}
- **Kamar Mandi**: {property.Bathrooms}

## HARGA
Harga jual beli disepakati sebesar **Rp {property.Price:N0}** ({Terbilang(property.Price)} Rupiah).

## PEMBAYARAN
1. Uang Muka (DP): Rp {(property.Price * 0.2m):N0}
2. Pelunasan dilakukan selambat-lambatnya 30 hari kerja setelah penandatanganan perjanjian ini.

## KETENTUAN LAIN
- Pajak dan biaya notaris ditanggung oleh PIHAK KEDUA.
- Sertifikat dan dokumen asli diserahkan setelah pelunasan.
- Segala sengketa diselesaikan secara musyawarah.

---

**PIHAK PERTAMA** &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; **PIHAK KEDUA**

<br/><br/><br/>
({seller.FullName}) &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; ({buyer.FullName})
";
    }

    private string GenerateRentContract(Property property, ApplicationUser buyer, ApplicationUser seller)
    {
        return $@"# PERJANJIAN SEWA PROPERTI

## PARA PIHAK
- **PEMILIK**: {seller.FullName} | HP: {seller.PhoneNumber}
- **PENYEWA**: {buyer.FullName} | HP: {buyer.PhoneNumber}

## PROPERTI
{property.Title} - {property.Address}, {property.City}

## BIAYA SEWA
**Rp {property.Price:N0}** per bulan

## KETENTUAN
- Sewa minimal 1 tahun
- Deposit 1 bulan
- Perawatan rutin ditanggung pemilik

---

**PEMILIK** &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; **PENYEWA**
";
    }

    private static string Terbilang(decimal amount)
    {
        // Simplified - in production, use proper number-to-words library
        return amount.ToString("N0");
    }
}
