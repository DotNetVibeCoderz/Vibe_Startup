using LandLord.Models;

namespace LandLord.Services;

/// <summary>
/// Interface untuk manajemen data bangunan
/// </summary>
public interface IBangunanService
{
    Task<List<Bangunan>> GetAllAsync();
    Task<Bangunan?> GetByIdAsync(int id);
    Task<Bangunan> CreateAsync(Bangunan bangunan);
    Task<Bangunan> UpdateAsync(Bangunan bangunan);
    Task<bool> DeleteAsync(int id);
    Task<List<Bangunan>> SearchAsync(string keyword);
    Task<List<Bangunan>> FilterAsync(string? jenisBangunan, string? fungsi, string? status);
    Task<int> GetTotalCountAsync();
    Task<Dictionary<string, int>> GetDistribusiJenisAsync();
    Task<Dictionary<string, int>> GetDistribusiFungsiAsync();
}
