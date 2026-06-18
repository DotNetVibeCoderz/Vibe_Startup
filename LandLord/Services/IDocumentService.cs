using LandLord.Models;

namespace LandLord.Services;

/// <summary>
/// Interface untuk manajemen dokumen/lampiran
/// </summary>
public interface IDocumentService
{
    Task<List<Document>> GetByTanahIdAsync(int tanahId);
    Task<List<Document>> GetByBangunanIdAsync(int bangunanId);
    Task<Document> UploadAsync(Document document, Stream fileStream);
    Task<bool> DeleteAsync(int id);
    Task<Document?> GetByIdAsync(int id);
    Task<string> GetFileUrlAsync(int id);
}
