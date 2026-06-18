using LandLord.Data;
using LandLord.Models;
using Microsoft.EntityFrameworkCore;

namespace LandLord.Services;

/// <summary>
/// Service untuk manajemen dokumen
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly AppDbContext _context;
    private readonly IStorageService _storageService;

    public DocumentService(AppDbContext context, IStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<List<Document>> GetByTanahIdAsync(int tanahId)
    {
        return await _context.Documents
            .Where(d => d.TanahId == tanahId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<List<Document>> GetByBangunanIdAsync(int bangunanId)
    {
        return await _context.Documents
            .Where(d => d.BangunanId == bangunanId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<Document> UploadAsync(Document document, Stream fileStream)
    {
        // Upload file ke storage
        var storedPath = await _storageService.UploadAsync(document.FileName, fileStream, document.ContentType);
        document.FilePath = storedPath;
        document.UploadedAt = DateTime.UtcNow;

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return false;

        // Hapus dari storage
        await _storageService.DeleteAsync(doc.FilePath);

        _context.Documents.Remove(doc);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        return await _context.Documents.FindAsync(id);
    }

    public async Task<string> GetFileUrlAsync(int id)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null)
            throw new FileNotFoundException("Dokumen tidak ditemukan.");

        return await _storageService.GetPublicUrlAsync(doc.FilePath);
    }
}
