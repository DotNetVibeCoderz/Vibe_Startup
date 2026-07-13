using Microsoft.EntityFrameworkCore;
using PCHub.Shared.Data;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Interfaces;
using PCHub.Shared.Models;

namespace PCHub.Shared.Services;

public class PcService : IPcService
{
    private readonly AppDbContext _db;
    public PcService(AppDbContext db) => _db = db;

    public async Task<List<PcDto>> GetAllPcsAsync()
    {
        return await _db.Pcs.Select(p => MapToDto(p)).ToListAsync();
    }

    public async Task<PcDto?> GetPcByIdAsync(Guid id)
    {
        var pc = await _db.Pcs.FindAsync(id);
        return pc == null ? null : MapToDto(pc);
    }

    public async Task<PagedResult<PcDto>> GetPcsPagedAsync(PagingRequest request)
    {
        var query = _db.Pcs.AsQueryable();

        if (!string.IsNullOrEmpty(request.Search))
            query = query.Where(p => p.Name.Contains(request.Search) || p.PcNumber.Contains(request.Search));

        query = request.SortBy switch
        {
            "name" => request.SortDesc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "status" => request.SortDesc ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "rate" => request.SortDesc ? query.OrderByDescending(p => p.HourlyRate) : query.OrderBy(p => p.HourlyRate),
            _ => query.OrderBy(p => p.PcNumber)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((request.Page - 1) * request.PageSize)
                               .Take(request.PageSize)
                               .Select(p => MapToDto(p))
                               .ToListAsync();

        return new PagedResult<PcDto>(items, total, request.Page, request.PageSize);
    }

    public async Task<PcDto> CreatePcAsync(PcCreateRequest request)
    {
        var pc = new Pc
        {
            Name = request.Name,
            PcNumber = request.PcNumber,
            Specifications = request.Specifications,
            HourlyRate = request.HourlyRate,
            Status = PcStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        _db.Pcs.Add(pc);
        await _db.SaveChangesAsync();
        return MapToDto(pc);
    }

    public async Task<PcDto> UpdatePcAsync(PcUpdateRequest request)
    {
        var pc = await _db.Pcs.FindAsync(request.Id)
            ?? throw new KeyNotFoundException("PC not found");

        pc.Name = request.Name;
        pc.PcNumber = request.PcNumber;
        pc.Status = request.Status;
        pc.Specifications = request.Specifications;
        pc.HourlyRate = request.HourlyRate;
        pc.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return MapToDto(pc);
    }

    public async Task DeletePcAsync(Guid id)
    {
        var pc = await _db.Pcs.FindAsync(id);
        if (pc != null)
        {
            _db.Pcs.Remove(pc);
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdatePcStatusAsync(Guid id, PcStatus status)
    {
        var pc = await _db.Pcs.FindAsync(id)
            ?? throw new KeyNotFoundException("PC not found");
        pc.Status = status;
        await _db.SaveChangesAsync();
    }

    public async Task<PcDto> UpdatePcResourceAsync(Guid id, double cpu, double gpu, double ram)
    {
        var pc = await _db.Pcs.FindAsync(id)
            ?? throw new KeyNotFoundException("PC not found");
        pc.CpuUsage = cpu;
        pc.GpuUsage = gpu;
        pc.RamUsage = ram;
        await _db.SaveChangesAsync();
        return MapToDto(pc);
    }

    private static PcDto MapToDto(Pc p) => new(
        Id: p.Id,
        Name: p.Name,
        PcNumber: p.PcNumber,
        Status: p.Status,
        Specifications: p.Specifications,
        HourlyRate: p.HourlyRate,
        CpuUsage: p.CpuUsage,
        GpuUsage: p.GpuUsage,
        RamUsage: p.RamUsage,
        IsActive: p.IsActive
    );
}

public class GameService : IGameService
{
    private readonly AppDbContext _db;
    public GameService(AppDbContext db) => _db = db;

    public async Task<List<GameDto>> GetAllGamesAsync()
    {
        return await _db.Games.Select(g => MapToDto(g)).ToListAsync();
    }

    public async Task<PagedResult<GameDto>> GetGamesPagedAsync(PagingRequest request)
    {
        var query = _db.Games.AsQueryable();

        if (!string.IsNullOrEmpty(request.Search))
            query = query.Where(g => g.Name.Contains(request.Search));

        query = request.SortBy switch
        {
            "name" => request.SortDesc ? query.OrderByDescending(g => g.Name) : query.OrderBy(g => g.Name),
            "genre" => request.SortDesc ? query.OrderByDescending(g => g.Genre) : query.OrderBy(g => g.Genre),
            _ => query.OrderBy(g => g.Name)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((request.Page - 1) * request.PageSize)
                               .Take(request.PageSize)
                               .Select(g => MapToDto(g))
                               .ToListAsync();

        return new PagedResult<GameDto>(items, total, request.Page, request.PageSize);
    }

    public async Task<GameDto> CreateGameAsync(GameCreateRequest request)
    {
        var game = new Game
        {
            Name = request.Name,
            Genre = request.Genre,
            Description = request.Description,
            ExecutablePath = request.ExecutablePath,
            IconUrl = request.IconUrl,
            Version = request.Version,
            CreatedAt = DateTime.UtcNow
        };
        _db.Games.Add(game);
        await _db.SaveChangesAsync();
        return MapToDto(game);
    }

    public async Task<GameDto> UpdateGameAsync(GameUpdateRequest request)
    {
        var game = await _db.Games.FindAsync(request.Id)
            ?? throw new KeyNotFoundException("Game not found");
        game.Name = request.Name;
        game.Genre = request.Genre;
        game.Description = request.Description;
        game.ExecutablePath = request.ExecutablePath;
        game.IconUrl = request.IconUrl;
        game.Version = request.Version;
        game.IsPopular = request.IsPopular;
        await _db.SaveChangesAsync();
        return MapToDto(game);
    }

    public async Task DeleteGameAsync(Guid id)
    {
        var game = await _db.Games.FindAsync(id);
        if (game != null)
        {
            _db.Games.Remove(game);
            await _db.SaveChangesAsync();
        }
    }

    private static GameDto MapToDto(Game g) => new(
        Id: g.Id,
        Name: g.Name,
        Genre: g.Genre,
        Description: g.Description,
        ExecutablePath: g.ExecutablePath,
        IconUrl: g.IconUrl,
        CoverImageUrl: g.CoverImageUrl,
        IsInstalled: g.IsInstalled,
        Version: g.Version,
        IsPopular: g.IsPopular
    );
}

public class BillingService : IBillingService
{
    private readonly AppDbContext _db;
    public BillingService(AppDbContext db) => _db = db;

    public async Task<BillingDto> StartBillingAsync(StartBillingRequest request)
    {
        var pc = await _db.Pcs.FindAsync(request.PcId)
            ?? throw new KeyNotFoundException("PC not found");

        if (pc.Status != PcStatus.Available)
            throw new InvalidOperationException("PC is not available");

        var billing = new BillingSession
        {
            UserId = request.UserId,
            PcId = request.PcId,
            StartTime = DateTime.UtcNow,
            HourlyRate = pc.HourlyRate,
            Status = BillingStatus.Active,
            PaymentMethod = request.PaymentMethod,
            CreatedAt = DateTime.UtcNow
        };

        pc.Status = PcStatus.InUse;
        _db.BillingSessions.Add(billing);
        await _db.SaveChangesAsync();

        return MapToDto(billing);
    }

    public async Task<BillingDto> StopBillingAsync(Guid billingId)
    {
        var billing = await _db.BillingSessions
            .Include(b => b.User)
            .Include(b => b.Pc)
            .FirstOrDefaultAsync(b => b.Id == billingId)
            ?? throw new KeyNotFoundException("Billing not found");

        billing.EndTime = DateTime.UtcNow;
        billing.Status = BillingStatus.Completed;

        var duration = (billing.EndTime.Value - billing.StartTime).TotalHours;
        billing.TotalCost = Math.Max(0, (decimal)duration * billing.HourlyRate);

        // Update PC status
        if (billing.Pc != null)
            billing.Pc.Status = PcStatus.Available;

        await _db.SaveChangesAsync();
        return MapToDto(billing);
    }

    public async Task<BillingDto?> GetActiveBillingAsync(Guid userId)
    {
        var billing = await _db.BillingSessions
            .Include(b => b.User)
            .Include(b => b.Pc)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Status == BillingStatus.Active);
        return billing == null ? null : MapToDto(billing);
    }

    public async Task<List<BillingDto>> GetUserBillingHistoryAsync(Guid userId)
    {
        return await _db.BillingSessions
            .Include(b => b.User)
            .Include(b => b.Pc)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => MapToDto(b))
            .ToListAsync();
    }

    public async Task<PagedResult<BillingDto>> GetAllBillingPagedAsync(PagingRequest request)
    {
        var query = _db.BillingSessions
            .Include(b => b.User)
            .Include(b => b.Pc)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Search))
            query = query.Where(b => b.User!.Username.Contains(request.Search));

        query = request.SortDesc
            ? query.OrderByDescending(b => b.CreatedAt)
            : query.OrderBy(b => b.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((request.Page - 1) * request.PageSize)
                               .Take(request.PageSize)
                               .Select(b => MapToDto(b))
                               .ToListAsync();

        return new PagedResult<BillingDto>(items, total, request.Page, request.PageSize);
    }

    public async Task<decimal> CalculateCostAsync(Guid billingId)
    {
        var billing = await _db.BillingSessions.FindAsync(billingId)
            ?? throw new KeyNotFoundException("Billing not found");

        var endTime = billing.EndTime ?? DateTime.UtcNow;
        var duration = (endTime - billing.StartTime).TotalHours;
        return Math.Max(0, (decimal)duration * billing.HourlyRate);
    }

    private static BillingDto MapToDto(BillingSession b) => new(
        Id: b.Id,
        UserId: b.UserId,
        Username: b.User?.Username ?? "",
        PcId: b.PcId,
        PcName: b.Pc?.Name ?? "",
        StartTime: b.StartTime,
        EndTime: b.EndTime,
        HourlyRate: b.HourlyRate,
        TotalCost: b.TotalCost,
        Status: b.Status,
        PaymentMethod: b.PaymentMethod,
        PaymentStatus: b.PaymentStatus
    );
}
