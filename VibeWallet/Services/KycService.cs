using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VibeWallet.Data;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of KYC (Know Your Customer) service
/// </summary>
public class KycService : IKycService
{
    private readonly VibeWalletDbContext _context;
    private readonly IStorageService _storageService;
    private readonly KycConfig _kycConfig;
    private readonly ILogger<KycService> _logger;

    public KycService(VibeWalletDbContext context, IStorageService storageService,
        IOptions<KycConfig> kycConfig, ILogger<KycService> logger)
    {
        _context = context;
        _storageService = storageService;
        _kycConfig = kycConfig.Value;
        _logger = logger;
    }

    public async Task<KycDocument> UploadKycDocumentAsync(Guid userId, Stream fileStream,
        string fileName, string contentType)
    {
        // Validate file type
        var extension = Path.GetExtension(fileName).ToLower();
        if (!_kycConfig.AllowedFileTypes.Contains(extension))
            throw new InvalidOperationException($"File type {extension} not allowed");

        // Validate file size
        if (fileStream.Length > _kycConfig.MaxUploadSizeMB * 1024 * 1024)
            throw new InvalidOperationException($"File size exceeds {_kycConfig.MaxUploadSizeMB}MB limit");

        var fileUrl = await _storageService.UploadFileAsync(fileStream, fileName, contentType, "kyc");

        var document = new KycDocument
        {
            UserId = userId,
            DocumentType = IdentityType.KTP,
            FileName = fileName,
            FileUrl = fileUrl,
            ContentType = contentType,
            FileSize = fileStream.Length,
            Status = KycStatus.Submitted
        };

        _context.KycDocuments.Add(document);
        await _context.SaveChangesAsync();

        // Update user's KYC status
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.KycStatus = KycStatus.Submitted;
            user.KycSubmittedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return document;
    }

    public async Task<KycSelfie> UploadSelfieAsync(Guid userId, Stream fileStream,
        string fileName, string contentType)
    {
        var fileUrl = await _storageService.UploadFileAsync(fileStream, fileName, contentType, "kyc");

        var selfie = new KycSelfie
        {
            UserId = userId,
            FileName = fileName,
            FileUrl = fileUrl,
            ContentType = contentType,
            FileSize = fileStream.Length,
            Status = KycStatus.Submitted
        };

        _context.KycSelfies.Add(selfie);
        await _context.SaveChangesAsync();

        return selfie;
    }

    public Task<bool> SubmitKycAsync(Guid userId)
    {
        // All documents are already submitted when uploaded
        return Task.FromResult(true);
    }

    public async Task<KycStatus> GetKycStatusAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.KycStatus ?? KycStatus.NotSubmitted;
    }

    public async Task<bool> VerifyKycAsync(Guid userId, string verifiedBy)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.KycStatus = KycStatus.Verified;
        user.KycVerifiedAt = DateTime.UtcNow;

        // Update documents
        var documents = await _context.KycDocuments
            .Where(d => d.UserId == userId && d.Status == KycStatus.Submitted)
            .ToListAsync();

        foreach (var doc in documents)
        {
            doc.Status = KycStatus.Verified;
            doc.VerifiedAt = DateTime.UtcNow;
            doc.VerifiedBy = verifiedBy;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectKycAsync(Guid userId, string reason, string rejectedBy)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.KycStatus = KycStatus.Rejected;

        var documents = await _context.KycDocuments
            .Where(d => d.UserId == userId && d.Status == KycStatus.Submitted)
            .ToListAsync();

        foreach (var doc in documents)
        {
            doc.Status = KycStatus.Rejected;
            doc.RejectionReason = reason;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<KycDocument>> GetUserKycDocumentsAsync(Guid userId)
    {
        return await _context.KycDocuments
            .Where(d => d.UserId == userId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }
}
