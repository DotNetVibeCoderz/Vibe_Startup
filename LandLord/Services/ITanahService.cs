using LandLord.Models;

namespace LandLord.Services;

/// <summary>
/// Interface untuk manajemen data tanah
/// </summary>
public interface ITanahService
{
    Task<List<Tanah>> GetAllAsync();
    Task<Tanah?> GetByIdAsync(int id);
    Task<Tanah> CreateAsync(Tanah tanah);
    Task<Tanah> UpdateAsync(Tanah tanah);
    Task<bool> DeleteAsync(int id);
    Task<List<Tanah>> SearchAsync(string keyword);
    Task<List<Tanah>> FilterAsync(string? jenisHak, string? statusPajak, string? kota);
    Task<int> GetTotalCountAsync();
    Task<decimal> GetTotalLuasAsync();
    Task<Dictionary<string, int>> GetDistribusiJenisHakAsync();
    Task<Dictionary<string, int>> GetDistribusiStatusPajakAsync();
}
